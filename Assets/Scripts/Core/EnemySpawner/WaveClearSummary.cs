/// <summary>
/// Fired when a wave is cleared so UI can show kills and whether another wave follows.
/// </summary>
public readonly struct WaveClearSummary
{
    public int ClearedWaveNumber { get; }
    public int EnemiesKilled { get; }
    public int EnemiesTotalInWave { get; }
    /// <summary>0 when the run is endless; otherwise the configured finite total (for UI).</summary>
    public int TotalWaves { get; }
    /// <summary>False after the last wave of a finite run is cleared.</summary>
    public bool MoreWavesRemainAfterThisClear { get; }

    public WaveClearSummary(int clearedWaveNumber, int enemiesKilled, int enemiesTotalInWave, int totalWaves, bool moreWavesRemainAfterThisClear)
    {
        ClearedWaveNumber = clearedWaveNumber;
        EnemiesKilled = enemiesKilled;
        EnemiesTotalInWave = enemiesTotalInWave;
        TotalWaves = totalWaves;
        MoreWavesRemainAfterThisClear = moreWavesRemainAfterThisClear;
    }
}
