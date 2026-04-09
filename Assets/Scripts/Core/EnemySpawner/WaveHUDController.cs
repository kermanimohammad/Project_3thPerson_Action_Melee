using TMPro;
using UnityEngine;

/// <summary>
/// Drives the main persistent wave title: countdown ("is coming"), combat (number only), and intermission ("is done").
/// </summary>
public class WaveHUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text killedEnemiesText;

    [Header("Main title — pre-wave countdown")]
    [SerializeField] private string waveComingFormat = "Wave {0} is coming!";
    [Tooltip("Used when the spawner has a finite total (not endless). Args: current wave, total waves.")]
    [SerializeField] private string waveComingWithTotalFormat = "Wave {0} of {1} is coming!";

    [Header("Main title — combat (wave active)")]
    [SerializeField] private string waveActiveFormat = "Wave {0}";
    [SerializeField] private string waveActiveWithTotalFormat = "Wave {0} of {1}";

    [Header("Main title — wave cleared (before Continue / next countdown)")]
    [SerializeField] private string waveDoneFormat = "Wave {0} is done.";
    [SerializeField] private string waveDoneWithTotalFormat = "Wave {0} of {1} is done.";

    [Header("Main title — recovery break (between wave sets)")]
    [Tooltip("Args: seconds remaining, upcoming wave number.")]
    [SerializeField] private string recoveryHudFormat = "Recovery: {0}s — wave {1} next (use trees)";

    [Header("Kill counter (optional)")]
    [SerializeField] private string killPrefix = "Killed Enemies: ";

    private int _recoveryUpcomingWave;

    private void OnEnable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnPreWaveCountdownStarted += HandlePreWaveCountdownStarted;
        waveSpawner.OnRecoveryBreakStarted += HandleRecoveryBreakStarted;
        waveSpawner.OnRecoveryBreakTick += HandleRecoveryBreakTick;
        waveSpawner.OnWaveStarted += HandleWaveStarted;
        waveSpawner.OnKillCountChanged += HandleKillCountChanged;
        waveSpawner.OnWaveCleared += HandleWaveCleared;
    }

    private void OnDisable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnPreWaveCountdownStarted -= HandlePreWaveCountdownStarted;
        waveSpawner.OnRecoveryBreakStarted -= HandleRecoveryBreakStarted;
        waveSpawner.OnRecoveryBreakTick -= HandleRecoveryBreakTick;
        waveSpawner.OnWaveStarted -= HandleWaveStarted;
        waveSpawner.OnKillCountChanged -= HandleKillCountChanged;
        waveSpawner.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandlePreWaveCountdownStarted(int waveNumber, int totalWaves)
    {
        if (waveText == null)
            return;

        waveText.text = totalWaves > 0
            ? string.Format(waveComingWithTotalFormat, waveNumber, totalWaves)
            : string.Format(waveComingFormat, waveNumber);
    }

    private void HandleRecoveryBreakStarted(int upcomingWave, int totalSeconds)
    {
        if (waveText == null)
            return;

        _recoveryUpcomingWave = upcomingWave;
        if (totalSeconds <= 0)
            return;

        waveText.text = string.Format(recoveryHudFormat, totalSeconds, upcomingWave);
    }

    private void HandleRecoveryBreakTick(int secondsLeft)
    {
        if (waveText == null)
            return;

        if (secondsLeft <= 0)
            return;

        waveText.text = string.Format(recoveryHudFormat, secondsLeft, _recoveryUpcomingWave);
    }

    private void HandleWaveStarted(int currentWave, int totalWaves)
    {
        if (waveText == null)
            return;

        waveText.text = totalWaves > 0
            ? string.Format(waveActiveWithTotalFormat, currentWave, totalWaves)
            : string.Format(waveActiveFormat, currentWave);
    }

    private void HandleKillCountChanged(int killed, int total)
    {
        if (killedEnemiesText != null)
            killedEnemiesText.text = $"{killPrefix}{killed}/{total}";
    }

    private void HandleWaveCleared(WaveClearSummary summary)
    {
        if (waveText == null)
            return;

        int total = summary.TotalWaves;
        waveText.text = total > 0
            ? string.Format(waveDoneWithTotalFormat, summary.ClearedWaveNumber, total)
            : string.Format(waveDoneFormat, summary.ClearedWaveNumber);
    }
}
