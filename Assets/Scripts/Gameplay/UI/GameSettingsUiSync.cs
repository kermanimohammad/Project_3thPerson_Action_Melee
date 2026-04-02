using UnityEngine;

/// <summary>
/// Forces Settings UI controls to match <see cref="PlayerPrefs"/> after engine state was applied (BattleArea).
/// </summary>
public static class GameSettingsUiSync
{
    public static void SyncSettingsUiFromPlayerPrefs(GameObject settingsRoot)
    {
        if (settingsRoot == null)
            return;

        foreach (var s in settingsRoot.GetComponentsInChildren<MixerVolumeSlider>(true))
            s.RefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<MenuItemsVolumeSlider>(true))
            s.RefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<FloatSettingSlider>(true))
            s.ForceRefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<GraphicsQualitySliderController>(true))
            s.RefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<TextureQualitySliderController>(true))
            s.RefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<ShadowQualitySliderController>(true))
            s.RefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<AntiAliasingSliderController>(true))
            s.RefreshFromPlayerPrefs();

        foreach (var s in settingsRoot.GetComponentsInChildren<VSyncSliderController>(true))
            s.RefreshFromPlayerPrefs();
    }
}
