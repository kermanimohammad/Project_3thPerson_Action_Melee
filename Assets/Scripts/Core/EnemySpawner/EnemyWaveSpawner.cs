using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class EnemyWaveSpawner : MonoBehaviour
{
    [Header("Wave Setup")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private AutoWaveSettings autoWaveSettings;

    [Header("Spawn Sources")]
    [SerializeField] private Transform[] autoSpawnPoints;
    [SerializeField] private EnemyGroupAI autoGroupAIPrefab;
    [SerializeField] private WeightedEnemyPrefab[] autoEnemyPool;

    [Header("Runtime Parent")]
    [SerializeField] private Transform runtimeParent;

    private int currentWaveIndex = -1;
    private int remainingGroupsInCurrentWave;
    private bool started;
    private bool waitingForNextWave;

    public event Action<int, int> OnWaveStarted;        
    public event Action<int, int> OnKillCountChanged;   
    public event Action<int> OnWaveCleared;       

    private int totalEnemiesInCurrentWave;
    private int killedEnemiesInCurrentWave;

    private readonly List<EnemyGroupRuntime> activeGroups = new();
    private List<Transform> currentWaveSpawnOrder = new();

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

    private IEnumerator SpawnNextWaveRoutine()
    {
        currentWaveIndex++;

        if (currentWaveIndex >= autoWaveSettings.TotalWaves)
        {
            Debug.Log("All auto-generated waves completed.");
            yield break;
        }

        yield return StartCoroutine(SpawnAutoWaveRoutine(currentWaveIndex));
    }

    private IEnumerator SpawnAutoWaveRoutine(int waveIndex)
    {
        Debug.Log($"Starting Auto Wave {waveIndex + 1}");

        activeGroups.Clear();
        remainingGroupsInCurrentWave = 0;
        waitingForNextWave = false;

        totalEnemiesInCurrentWave = 0;
        killedEnemiesInCurrentWave = 0;

        OnWaveStarted?.Invoke(waveIndex + 1, autoWaveSettings.TotalWaves);
        OnKillCountChanged?.Invoke(killedEnemiesInCurrentWave, totalEnemiesInCurrentWave);

        currentWaveSpawnOrder = BuildShuffledSpawnPointList();
        int groupCount = GetAutoGroupCountForWave(waveIndex);

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            EnemyGroupRuntime runtime = SpawnAutoGroup(waveIndex, groupIndex);
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
            StartCoroutine(SpawnNextWaveRoutine());
        }
    }

    private EnemyGroupRuntime SpawnAutoGroup(int waveIndex, int groupIndex)
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

        EnemyGroupAI groupAIInstance = null;
        if (autoGroupAIPrefab != null)
        {
            groupAIInstance = Instantiate(
                autoGroupAIPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                runtimeParent
            );
        }

        string groupName = $"Wave_{waveIndex + 1}_Group_{groupIndex + 1}";
        EnemyGroupRuntime runtime = new EnemyGroupRuntime(groupName, groupAIInstance);

        int enemyCount = GetAutoEnemyCountForGroup(waveIndex);

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
        Debug.Log($"Wave {currentWaveIndex + 1} cleared.");
        OnWaveCleared?.Invoke(currentWaveIndex + 1);

        yield return new WaitForSeconds(autoWaveSettings.DelayAfterWaveCleared);
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
        OnKillCountChanged?.Invoke(killedEnemiesInCurrentWave, totalEnemiesInCurrentWave);
    }
}