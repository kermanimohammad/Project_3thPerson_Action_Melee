using UnityEngine;

/// <summary>
/// Same persistence path as <see cref="MainMenuSettingsCoordinator.SaveAllSettings"/>:
/// writes all settings sliders (audio + float prefs) to <see cref="PlayerPrefs"/> and saves.
/// </summary>
public static class GameSettingsPersistence
{
    public static void SaveAllSettingsToPlayerPrefs()
    {
        foreach (var s in Object.FindObjectsByType<MenuItemsVolumeSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.SaveToPlayerPrefs();
        foreach (var s in Object.FindObjectsByType<MixerVolumeSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.SaveToPlayerPrefs();
        foreach (var s in Object.FindObjectsByType<FloatSettingSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.SaveToPlayerPrefs();
        PlayerPrefs.Save();
    }
}
