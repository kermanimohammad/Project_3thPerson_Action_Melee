using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies the same saved values as the MainMenu Settings sliders (PlayerPrefs keys) to the running game.
/// Use when BattleArea loads so graphics/audio match stored settings before the Settings UI is enabled.
/// Logic mirrors <see cref="GraphicsQualitySliderController"/>, <see cref="VSyncSliderController"/>, etc.
/// </summary>
public static class GameSettingsRuntime
{
    private const string PrefsGraphicsQuality = "Settings_GraphicsQualityIndex";
    private const string PrefsVSync = "Settings_VSync";
    private const string PrefsTextureQuality = "Settings_TextureQualityIndex";
    private const string PrefsShadowQuality = "Settings_ShadowQualityIndex";
    private const string PrefsAntiAliasing = "Settings_AntiAliasingIndex";

    /// <summary>Defaults + audio mixer + quality / vsync / texture / shadow / MSAA from PlayerPrefs.</summary>
    public static void ApplyAllSavedSettingsToEngine()
    {
        MainMenuSettingsKeys.EnsureDefaultsWritten();
        GameAudioSettings.ApplyAllVolumesFromPlayerPrefs();

        // Order: overall quality first (can reset dependent settings), then overrides, VSync last.
        ApplyGraphicsQualityFromPrefs();
        ApplyTextureQualityFromPrefs();
        ApplyShadowQualityFromPrefs();
        ApplyAntiAliasingFromPrefs();
        ApplyVSyncFromPrefs();
    }

    private static void ApplyVSyncFromPrefs()
    {
        int stored = PlayerPrefs.GetInt(PrefsVSync, -1);
        if (stored != 0 && stored != 1)
            return;
        QualitySettings.vSyncCount = stored == 1 ? 1 : 0;
    }

    private static void ApplyGraphicsQualityFromPrefs()
    {
        int stored = PlayerPrefs.GetInt(PrefsGraphicsQuality, -1);
        if (stored < 0 || stored > 3)
            return;

        int[] map = BuildGraphicsQualityLevelMap();
        int unityLevel = map[stored];
        QualitySettings.SetQualityLevel(unityLevel, true);
    }

    private static int[] BuildGraphicsQualityLevelMap()
    {
        const string lowName = "Low";
        const string mediumName = "Medium";
        const string highName = "High";
        const string ultraName = "Ultra";

        var map = new int[4];
        map[0] = FindQualityLevelIndex(lowName);
        map[1] = FindQualityLevelIndex(mediumName);
        map[2] = FindQualityLevelIndex(highName);
        map[3] = FindQualityLevelIndex(ultraName);

        int available = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        for (int i = 0; i < 4; i++)
        {
            int idx = map[i];
            if (idx < 0 || idx >= available)
                map[i] = Mathf.Clamp(i, 0, Mathf.Max(0, available - 1));
        }

        return map;
    }

    private static int FindQualityLevelIndex(string name)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
            return -1;
        if (string.IsNullOrWhiteSpace(name))
            return -1;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (string.Equals(QualitySettings.names[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static void ApplyTextureQualityFromPrefs()
    {
        int stored = PlayerPrefs.GetInt(PrefsTextureQuality, -1);
        if (stored < 0 || stored > 3)
            return;
        int masterLimit = Mathf.Clamp(3 - stored, 0, 3);
        QualitySettings.globalTextureMipmapLimit = masterLimit;
    }

    private static void ApplyShadowQualityFromPrefs()
    {
        int stored = PlayerPrefs.GetInt(PrefsShadowQuality, -1);
        if (stored < 0 || stored > 3)
            return;
        QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)stored;
    }

    private static void ApplyAntiAliasingFromPrefs()
    {
        int stored = PlayerPrefs.GetInt(PrefsAntiAliasing, -1);
        if (stored < 0 || stored > 3)
            return;

        int samples = stored switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            3 => 8,
            _ => 4
        };

        var urp = GetActiveUrpAsset();
        if (urp != null)
            urp.msaaSampleCount = samples;
        else
            QualitySettings.antiAliasing = samples == 1 ? 0 : samples;
    }

    private static UniversalRenderPipelineAsset GetActiveUrpAsset()
    {
        if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset q)
            return q;
        return GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
    }
}
