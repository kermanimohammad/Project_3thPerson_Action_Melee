using UnityEngine;

[System.Serializable]
public class EnemySpawnEntry
{
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Count Range")]
    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 1;

    public GameObject EnemyPrefab => enemyPrefab;
    public int MinCount => minCount;
    public int MaxCount => maxCount;

    public int GetRandomCount()
    {
        int clampedMin = Mathf.Max(0, minCount);
        int clampedMax = Mathf.Max(clampedMin, maxCount);
        return Random.Range(clampedMin, clampedMax + 1);
    }
}