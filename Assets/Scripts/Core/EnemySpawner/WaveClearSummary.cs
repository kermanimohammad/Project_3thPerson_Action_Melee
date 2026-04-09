/// <summary>
/// Fired when a wave is cleared so UI can show kills and whether another wave follows.
/// </summary>
public readonly struct WaveClearSummary
{
    public int ClearedWaveNumber { get; }
    public int EnemiesKilled { get; }
    public int EnemiesTotalInWave { get; }
    public int TotalWaves { get; }
    /// <summary>False after the last wave is cleared (next step is all-complete, not another wave).</summary>
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
