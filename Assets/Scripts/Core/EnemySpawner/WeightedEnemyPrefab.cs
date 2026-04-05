using UnityEngine;

[System.Serializable]
public class WeightedEnemyPrefab
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int weight = 1;

    public GameObject EnemyPrefab => enemyPrefab;
    public int Weight => Mathf.Max(1, weight);
}