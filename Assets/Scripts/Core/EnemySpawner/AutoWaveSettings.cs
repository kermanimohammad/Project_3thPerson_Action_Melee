using UnityEngine;

[System.Serializable]
public class AutoWaveSettings
{
    [Header("Wave Count")]
    [SerializeField] private int totalWaves = 5;

    [Header("Group Formula")]
    [SerializeField] private int baseGroupsPerWave = 2;
    [SerializeField] private int addGroupEveryNWaves = 2;
    [SerializeField] private int maxGroupsPerWave = 4;

    [Header("Enemy Count Formula")]
    [SerializeField] private int baseEnemiesPerGroup = 2;
    [SerializeField] private float enemyGrowthPerWave = 1f;
    [SerializeField] private int enemyCountVariance = 1;

    [Header("Spawn")]
    [SerializeField] private float delayAfterWaveCleared = 3f;
    [SerializeField] private float spawnScatterRadius = 1.5f;

    public int TotalWaves => Mathf.Max(1, totalWaves);
    public int BaseGroupsPerWave => Mathf.Max(1, baseGroupsPerWave);
    public int AddGroupEveryNWaves => Mathf.Max(1, addGroupEveryNWaves);
    public int MaxGroupsPerWave => Mathf.Max(1, maxGroupsPerWave);

    public int BaseEnemiesPerGroup => Mathf.Max(1, baseEnemiesPerGroup);
    public float EnemyGrowthPerWave => enemyGrowthPerWave;
    public int EnemyCountVariance => Mathf.Max(0, enemyCountVariance);

    public float DelayAfterWaveCleared => Mathf.Max(0f, delayAfterWaveCleared);
    public float SpawnScatterRadius => Mathf.Max(0f, spawnScatterRadius);
}