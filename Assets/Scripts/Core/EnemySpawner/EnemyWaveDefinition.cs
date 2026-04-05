using UnityEngine;

[System.Serializable]
public class EnemyWaveDefinition
{
    [SerializeField] private string waveName = "Wave";
    [SerializeField] private float delayAfterWaveCleared = 3f;
    [SerializeField] private EnemyGroupDefinition[] groups;

    public string WaveName => waveName;
    public float DelayAfterWaveCleared => delayAfterWaveCleared;
    public EnemyGroupDefinition[] Groups => groups;
}