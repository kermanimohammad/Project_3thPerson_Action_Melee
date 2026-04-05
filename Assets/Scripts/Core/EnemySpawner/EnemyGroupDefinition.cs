using UnityEngine;

[System.Serializable]
public class EnemyGroupDefinition
{
    [SerializeField] private string groupName = "Group";
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private EnemyGroupAI groupAIPrefab;
    [SerializeField] private float spawnScatterRadius = 0f;
    [SerializeField] private EnemySpawnEntry[] enemies;

    public string GroupName => groupName;
    public Transform SpawnPoint => spawnPoint;
    public EnemyGroupAI GroupAIPrefab => groupAIPrefab;
    public float SpawnScatterRadius => spawnScatterRadius;
    public EnemySpawnEntry[] Enemies => enemies;
}