using TMPro;
using UnityEngine;

public class WaveHUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text killedEnemiesText;

    [Header("Text Format")]
    [SerializeField] private string wavePrefix = "Wave ";
    [SerializeField] private string waveSuffix = " is coming!";
    [SerializeField] private string killPrefix = "Killed Enemies: ";

    private void OnEnable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnWaveStarted += HandleWaveStarted;
        waveSpawner.OnKillCountChanged += HandleKillCountChanged;
        waveSpawner.OnWaveCleared += HandleWaveCleared;
    }

    private void OnDisable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnWaveStarted -= HandleWaveStarted;
        waveSpawner.OnKillCountChanged -= HandleKillCountChanged;
        waveSpawner.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandleWaveStarted(int currentWave, int totalWaves)
    {
        if (waveText != null)
            waveText.text = $"{wavePrefix}{currentWave}{waveSuffix}";
    }

    private void HandleKillCountChanged(int killed, int total)
    {
        if (killedEnemiesText != null)
            killedEnemiesText.text = $"{killPrefix}{killed}/{total}";
    }

    private void HandleWaveCleared(int clearedWave)
    {
        if (waveText != null)
            waveText.text = $"Wave {clearedWave} cleared!";
    }
}