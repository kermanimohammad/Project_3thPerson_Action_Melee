using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class EnemyWaveSpawner : MonoBehaviour
{
    [Header("Wave Setup")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private AutoWaveSettings autoWaveSettings;
    [Tooltip("If true, after a wave is cleared the next wave does not start until ConfirmContinueToNextWave() (e.g. UI button).")]
    [SerializeField] private bool waitForPlayerContinueBetweenWaves = true;

    [Header("Spawn Sources")]
    [SerializeField] private Transform[] autoSpawnPoints;
    [SerializeField] private EnemyGroupAI autoGroupAIPrefab;
    [SerializeField] private WeightedEnemyPrefab[] autoEnemyPool;

    [Header("Squad (3-person)")]
    [SerializeField, Min(1)] private int squadSize = 3;

    [Header("Runtime Parent")]
    [SerializeField] private Transform runtimeParent;

    private int currentWaveIndex = -1;
    private int remainingGroupsInCurrentWave;
    private bool started;
    private bool waitingForNextWave;

    public event Action<int, int> OnWaveStarted;
    public event Action<int, int> OnKillCountChanged;
    public event Action<WaveClearSummary> OnWaveCleared;
    /// <summary>About to show pre-wave countdown for this 1-based wave index.</summary>
    public event Action<int, int> OnPreWaveCountdownStarted;
    /// <summary>Seconds left in pre-wave countdown (5..1), then 0 when finished.</summary>
    public event Action<int> OnPreWaveCountdownTick;
    /// <summary>Long recovery between sets of waves (upcoming 1-based wave index, total seconds).</summary>
    public event Action<int, int> OnRecoveryBreakStarted;
    /// <summary>Seconds left in recovery break, then 0 when finished.</summary>
    public event Action<int> OnRecoveryBreakTick;
    public event Action OnAllWavesCompleted;

    private int totalEnemiesInCurrentWave;
    private int killedEnemiesInCurrentWave;
    private int totalEnemiesKilledOverall;
    private int wavesClearedOverall;

    private readonly List<EnemyGroupRuntime> activeGroups = new();
    private List<Transform> currentWaveSpawnOrder = new();

    private bool _waitingForContinueBetweenWaves;
    private bool _continueToNextWaveRequested;
    private bool _stopped;

    private void Start()
    {
        if (playOnStart)
            StartSpawning();
    }

    public void StartSpawning()
    {
        if (started)
            return;

        started = true;
        StartCoroutine(SpawnNextWaveRoutine());
    }

    /// <summary>Stops all spawning and prevents future waves from starting (e.g. player died).</summary>
    public void StopSpawning()
    {
        if (_stopped)
            return;
        _stopped = true;
        _waitingForContinueBetweenWaves = false;
        _continueToNextWaveRequested = false;
        waitingForNextWave = false;
        StopAllCoroutines();
    }

    /// <summary>Call from a UI continue button after a wave clear. Ignored if the spawner is not waiting.</summary>
    public void ConfirmContinueToNextWave()
    {
        if (_stopped)
            return;
        if (!_waitingForContinueBetweenWaves)
            return;
        _continueToNextWaveRequested = true;
    }

    public bool IsWaitingForPlayerContinue => _waitingForContinueBetweenWaves;
    public int TotalEnemiesKilledOverall => totalEnemiesKilledOverall;
    public int WavesClearedOverall => wavesClearedOverall;

    private IEnumerator SpawnNextWaveRoutine()
    {
        if (_stopped)
            yield break;
        currentWaveIndex++;

        if (!autoWaveSettings.InfiniteWaves && currentWaveIndex >= autoWaveSettings.FiniteTotalWaves)
        {
            Debug.Log("Finite wave run completed.");
            OnAllWavesCompleted?.Invoke();
            yield break;
        }

        int upcomingWave = currentWaveIndex + 1;
        if (autoWaveSettings.ShouldInsertRecoveryBreakBeforeWave(upcomingWave))
            yield return RecoveryBreakCoroutine(upcomingWave);

        yield return PreWaveCountdownCoroutine();
        yield return SpawnAutoWaveRoutine(currentWaveIndex);
    }

    private IEnumerator PreWaveCountdownCoroutine()
    {
        if (_stopped)
            yield break;
        int waveDisplay = currentWaveIndex + 1;
        int total = autoWaveSettings.TotalWavesForUi;
        OnPreWaveCountdownStarted?.Invoke(waveDisplay, total);

        int sec = autoWaveSettings.PreWaveCountdownSeconds;
        if (sec <= 0)
        {
            OnPreWaveCountdownTick?.Invoke(0);
            yield break;
        }

        for (int t = sec; t > 0; t--)
        {
            OnPreWaveCountdownTick?.Invoke(t);
            yield return new WaitForSeconds(1f);
        }

        OnPreWaveCountdownTick?.Invoke(0);
    }

    private IEnumerator RecoveryBreakCoroutine(int upcomingOneBasedWave)
    {
        if (_stopped)
            yield break;
        int total = autoWaveSettings.RecoveryBreakDurationSeconds;
        OnRecoveryBreakStarted?.Invoke(upcomingOneBasedWave, total);

        if (total <= 0)
        {
            OnRecoveryBreakTick?.Invoke(0);
            yield break;
        }

        for (int t = total; t > 0; t--)
        {
            OnRecoveryBreakTick?.Invoke(t);
            yield return new WaitForSeconds(1f);
        }

        OnRecoveryBreakTick?.Invoke(0);
    }

    private IEnumerator SpawnAutoWaveRoutine(int waveIndex)
    {
        if (_stopped)
            yield break;
        Debug.Log($"Starting Auto Wave {waveIndex + 1}");

        activeGroups.Clear();
        remainingGroupsInCurrentWave = 0;
        waitingForNextWave = false;

        totalEnemiesInCurrentWave = 0;
        killedEnemiesInCurrentWave = 0;

        OnWaveStarted?.Invoke(waveIndex + 1, autoWaveSettings.TotalWavesForUi);
        OnKillCountChanged?.Invoke(killedEnemiesInCurrentWave, totalEnemiesInCurrentWave);

        currentWaveSpawnOrder = BuildShuffledSpawnPointList();
        // Wave spawns: one 3-person squad per spawn point (A/B/C).
        int desiredGroupCount = GetAutoGroupCountForWave(waveIndex);
        int availableSpawnPoints = currentWaveSpawnOrder != null ? currentWaveSpawnOrder.Count : 0;
        int groupCount = Mathf.Min(desiredGroupCount, availableSpawnPoints);

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            EnemyGroupRuntime runtime = SpawnAutoSquad(waveIndex, groupIndex);
            if (runtime != null)
            {
                runtime.OnGroupCleared += HandleGroupCleared;
                activeGroups.Add(runtime);
                remainingGroupsInCurrentWave++;
            }
        }

        if (remainingGroupsInCurrentWave == 0)
        {
            Debug.Log($"Wave {waveIndex + 1} had no valid groups. Advancing after delay.");
            yield return new WaitForSeconds(autoWaveSettings.DelayAfterWaveCleared);
            if (!_stopped)
                StartCoroutine(SpawnNextWaveRoutine());
        }
    }

    private EnemyGroupRuntime SpawnAutoSquad(int waveIndex, int groupIndex)
    {
        if (autoSpawnPoints == null || autoSpawnPoints.Length == 0)
        {
            Debug.LogWarning("No auto spawn points assigned.");
            return null;
        }

        Transform spawnPoint = null;

        if (currentWaveSpawnOrder != null && currentWaveSpawnOrder.Count > 0)
            spawnPoint = currentWaveSpawnOrder[groupIndex % currentWaveSpawnOrder.Count];

        if (spawnPoint == null)
        {
            Debug.LogWarning($"Spawn point at index {groupIndex % autoSpawnPoints.Length} is null.");
            return null;
        }

        // Create a per-squad coordinator so members can delegate objectives (player/doors/stone).
        var coordinatorGo = new GameObject($"Wave_{waveIndex + 1}_Squad_{groupIndex + 1}");
        coordinatorGo.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        if (runtimeParent != null)
            coordinatorGo.transform.SetParent(runtimeParent, true);
        var coordinator = coordinatorGo.AddComponent<EnemySquadCoordinator>();
        // Let coordinator auto-resolve doors/stone if not wired; no scene edits required.
        coordinator.ConfigureObjectives(null, null);

        EnemyGroupAI groupAIInstance = null;
        if (autoGroupAIPrefab != null)
        {
            groupAIInstance = Instantiate(autoGroupAIPrefab, spawnPoint.position, spawnPoint.rotation, runtimeParent);
        }

        string groupName = $"Wave_{waveIndex + 1}_Squad_{groupIndex + 1}";
        EnemyGroupRuntime runtime = new EnemyGroupRuntime(groupName, groupAIInstance);

        int enemyCount = GetAutoEnemyCountForGroup(waveIndex) <= 7 ? GetAutoEnemyCountForGroup(waveIndex) : 7;

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = PickWeightedEnemyPrefab();
            if (prefab == null)
                continue;

            Vector3 spawnPosition = GetAutoSpawnPosition(spawnPoint.position);
            Quaternion spawnRotation = spawnPoint.rotation;

            GameObject enemyObj = Instantiate(
                prefab,
                spawnPosition,
                spawnRotation,
                runtimeParent
            );

            // Silence missing AnimationEvent receivers on imported enemy clips (e.g. "foot_sound", "TrailOn").
            // Events are dispatched on the same GameObject as the Animator — put the receiver there.
            var anim = enemyObj.GetComponentInChildren<Animator>(includeInactive: true);
            GameObject eventHost = anim != null ? anim.gameObject : enemyObj;
            if (eventHost.GetComponent<AnimationEventNoopReceiver>() == null)
                eventHost.AddComponent<AnimationEventNoopReceiver>();

            // Disable the old single-target AI if present (we want squad behaviour).
            var meleeAi = enemyObj.GetComponent<MeleeEnemyAI>();
            if (meleeAi != null)
                meleeAi.enabled = false;

            // Squad AI must be authored on the enemy prefab so it can be tuned in the Inspector.
            var memberAi = enemyObj.GetComponent<EnemyGroupMemberAI>();
            if (memberAi == null)
            {
                Debug.LogError(
                    $"{nameof(EnemyWaveSpawner)}: Spawned enemy '{enemyObj.name}' has no {nameof(EnemyGroupMemberAI)}. " +
                    $"Add {nameof(EnemyGroupMemberAI)} to the enemy prefab so you can tune separation/roles in Inspector.",
                    enemyObj);
                continue;
            }

            // 2 engage player, 1 objective (doors/stone).
            bool objectiveMember = (i == enemyCount - 1);
            bool engageMember = !objectiveMember;
            memberAi.InitializeForWaveSquad(coordinator, engageMember, objectiveMember);

            EnemySpawnedMember spawnedMember = enemyObj.GetComponent<EnemySpawnedMember>();
            if (spawnedMember == null)
                spawnedMember = enemyObj.AddComponent<EnemySpawnedMember>();

            runtime.RegisterMember(spawnedMember);

            spawnedMember.OnMemberDied += HandleSpawnedMemberDied;
            totalEnemiesInCurrentWave++;
            OnKillCountChanged?.Invoke(killedEnemiesInCurrentWave, totalEnemiesInCurrentWave);

            //EnemyAIBase enemyAI = enemyObj.GetComponent<EnemyAIBase>();
            //if (enemyAI != null)
            //    enemyAI.InitializeGroup(groupAIInstance);
        }

        Debug.Log($"Spawned {groupName} with {runtime.AliveCount} enemies.");
        return runtime;
    }

    private void HandleGroupCleared(EnemyGroupRuntime clearedGroup)
    {
        clearedGroup.OnGroupCleared -= HandleGroupCleared;
        activeGroups.Remove(clearedGroup);
        remainingGroupsInCurrentWave--;

        if (clearedGroup.GroupAIInstance != null)
            Destroy(clearedGroup.GroupAIInstance.gameObject);

        Debug.Log($"Group cleared: {clearedGroup.GroupName}. Remaining groups in wave: {remainingGroupsInCurrentWave}");

        if (remainingGroupsInCurrentWave <= 0 && !waitingForNextWave)
        {
            waitingForNextWave = true;
            StartCoroutine(HandleWaveClearedRoutine());
        }
    }

    private IEnumerator HandleWaveClearedRoutine()
    {
        if (_stopped)
            yield break;
        int clearedDisplay = currentWaveIndex + 1;
        Debug.Log($"Wave {clearedDisplay} cleared.");
        wavesClearedOverall = Mathf.Max(wavesClearedOverall, clearedDisplay);

        bool moreRemain = autoWaveSettings.InfiniteWaves || clearedDisplay < autoWaveSettings.FiniteTotalWaves;
        var summary = new WaveClearSummary(
            clearedDisplay,
            killedEnemiesInCurrentWave,
            totalEnemiesInCurrentWave,
            autoWaveSettings.TotalWavesForUi,
            moreRemain);

        OnWaveCleared?.Invoke(summary);

        if (autoWaveSettings.DelayAfterWaveCleared > 0f)
            yield return new WaitForSeconds(autoWaveSettings.DelayAfterWaveCleared);

        if (waitForPlayerContinueBetweenWaves)
        {
            _waitingForContinueBetweenWaves = true;
            _continueToNextWaveRequested = false;
            yield return new WaitUntil(() => _continueToNextWaveRequested);
            _waitingForContinueBetweenWaves = false;
            _continueToNextWaveRequested = false;
        }

        if (!_stopped)
            StartCoroutine(SpawnNextWaveRoutine());
    }

    private int GetAutoGroupCountForWave(int waveIndex)
    {
        return Mathf.Clamp(
            autoWaveSettings.BaseGroupsPerWave +
            Mathf.FloorToInt((float)waveIndex / autoWaveSettings.AddGroupEveryNWaves),
            1,
            autoWaveSettings.MaxGroupsPerWave
        );
    }

    private int GetAutoEnemyCountForGroup(int waveIndex)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(autoWaveSettings.BaseEnemiesPerGroup + waveIndex * autoWaveSettings.EnemyGrowthPerWave) +
            UnityEngine.Random.Range(-autoWaveSettings.EnemyCountVariance, autoWaveSettings.EnemyCountVariance + 1)
        );
    }

    private GameObject PickWeightedEnemyPrefab()
    {
        if (autoEnemyPool == null || autoEnemyPool.Length == 0)
            return null;

        int totalWeight = 0;

        foreach (WeightedEnemyPrefab option in autoEnemyPool)
        {
            if (option != null && option.EnemyPrefab != null)
                totalWeight += option.Weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);

        foreach (WeightedEnemyPrefab option in autoEnemyPool)
        {
            if (option == null || option.EnemyPrefab == null)
                continue;

            roll -= option.Weight;
            if (roll < 0)
                return option.EnemyPrefab;
        }

        return null;
    }

    private Vector3 GetAutoSpawnPosition(Vector3 center)
    {
        if (autoWaveSettings.SpawnScatterRadius <= 0f)
            return center;

        Vector2 random2D = UnityEngine.Random.insideUnitCircle * autoWaveSettings.SpawnScatterRadius;
        return center + new Vector3(random2D.x, 0f, random2D.y);
    }


    private List<Transform> BuildShuffledSpawnPointList()
    {
        List<Transform> shuffled = new();

        if (autoSpawnPoints == null)
            return shuffled;

        foreach (Transform point in autoSpawnPoints)
        {
            if (point != null)
                shuffled.Add(point);
        }

        for (int i = 0; i < shuffled.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[randomIndex]) = (shuffled[randomIndex], shuffled[i]);
        }

        return shuffled;
    }

    private void HandleSpawnedMemberDied(EnemySpawnedMember member)
    {
        if (member == null)
            return;

        member.OnMemberDied -= HandleSpawnedMemberDied;

        killedEnemiesInCurrentWave++;
        totalEnemiesKilledOverall++;
        OnKillCountChanged?.Invoke(killedEnemiesInCurrentWave, totalEnemiesInCurrentWave);
    }
}