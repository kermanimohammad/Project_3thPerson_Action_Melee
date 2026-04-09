using UnityEngine;

[System.Serializable]
public class AutoWaveSettings
{
    [Header("Wave Count")]
    [Tooltip("Exact number of waves before the run ends. Use 0 for endless waves (Continue always starts the next wave after the victory screen).")]
    [SerializeField] private int totalWaves = 0;

    [Header("Group Formula")]
    [SerializeField] private int baseGroupsPerWave = 2;
    [SerializeField] private int addGroupEveryNWaves = 2;
    [SerializeField] private int maxGroupsPerWave = 4;

    [Header("Enemy Count Formula")]
    [SerializeField] private int baseEnemiesPerGroup = 2;
    [SerializeField] private float enemyGrowthPerWave = 1f;
    [SerializeField] private int enemyCountVariance = 1;

    [Header("Spawn")]
    [Tooltip("Seconds shown before each wave spawns (recovery time). 0 skips countdown.")]
    [SerializeField, Min(0)] private int preWaveCountdownSeconds = 5;
    [SerializeField] private float delayAfterWaveCleared = 3f;
    [SerializeField] private float spawnScatterRadius = 1.5f;

    [Header("Recovery between wave sets (e.g. heal at trees)")]
    [Tooltip("After every N cleared waves, insert a long countdown before the next wave (player can use trees). 0 = disabled.")]
    [SerializeField] private int recoveryBreakEveryNWaves = 5;
    [Tooltip("Duration in seconds of that recovery countdown (e.g. 60).")]
    [SerializeField, Min(0)] private int recoveryBreakDurationSeconds = 60;

    /// <summary>True when <see cref="totalWaves"/> is 0 — there is no final wave; the loop continues until the player uses Main Menu.</summary>
    public bool InfiniteWaves => totalWaves <= 0;

    /// <summary>Valid only when <see cref="InfiniteWaves"/> is false.</summary>
    public int FiniteTotalWaves => Mathf.Max(1, totalWaves);

    /// <summary>0 in UI means endless (no fixed total in HUD copy). Otherwise equals <see cref="FiniteTotalWaves"/>.</summary>
    public int TotalWavesForUi => InfiniteWaves ? 0 : FiniteTotalWaves;

    public int BaseGroupsPerWave => Mathf.Max(1, baseGroupsPerWave);
    public int AddGroupEveryNWaves => Mathf.Max(1, addGroupEveryNWaves);
    public int MaxGroupsPerWave => Mathf.Max(1, maxGroupsPerWave);

    public int BaseEnemiesPerGroup => Mathf.Max(1, baseEnemiesPerGroup);
    public float EnemyGrowthPerWave => enemyGrowthPerWave;
    public int EnemyCountVariance => Mathf.Max(0, enemyCountVariance);

    public int PreWaveCountdownSeconds => Mathf.Max(0, preWaveCountdownSeconds);

    public float DelayAfterWaveCleared => Mathf.Max(0f, delayAfterWaveCleared);
    public float SpawnScatterRadius => Mathf.Max(0f, spawnScatterRadius);

    public int RecoveryBreakEveryNWaves => Mathf.Max(0, recoveryBreakEveryNWaves);
    public int RecoveryBreakDurationSeconds => Mathf.Max(0, recoveryBreakDurationSeconds);

    /// <summary>
    /// True before spawning wave 6, 11, … when every = 5 (not before waves 1–5).
    /// </summary>
    public bool ShouldInsertRecoveryBreakBeforeWave(int upcomingOneBasedWaveNumber)
    {
        int n = RecoveryBreakEveryNWaves;
        if (n <= 0 || upcomingOneBasedWaveNumber <= 0)
            return false;
        if (upcomingOneBasedWaveNumber <= n)
            return false;
        return (upcomingOneBasedWaveNumber - 1) % n == 0;
    }
}
