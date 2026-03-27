using UnityEngine;

/// <summary>
/// PlayerPrefs keys and defaults for MainMenu settings (Audio / Controls).
/// </summary>
public static class MainMenuSettingsKeys
{
    public const string MenuItemsVolume = "Settings_MenuItemsVolume";
    public const string MasterVolume = "Settings_MasterVolume";
    public const string MusicVolume = "Settings_MusicVolume";
    public const string SfxVolume = "Settings_SfxVolume";
    public const string DialogueVolume = "Settings_DialogueVolume";

    public const string MouseSensitivity = "Settings_MouseSensitivity";
    public const string GamepadSensitivity = "Settings_GamepadSensitivity";

    public const float DefaultMenuItemsVolume = 0.07f;
    public const float DefaultLinearVolume = 0.542f;

    /// <summary>
    /// Writes missing keys so first launch persists defaults (MenuItems = 0.07, others match scene defaults).
    /// </summary>
    public static void EnsureDefaultsWritten()
    {
        bool wrote = false;
        wrote |= WriteDefaultFloat(MenuItemsVolume, DefaultMenuItemsVolume);
        wrote |= WriteDefaultFloat(MasterVolume, DefaultLinearVolume);
        wrote |= WriteDefaultFloat(MusicVolume, DefaultLinearVolume);
        wrote |= WriteDefaultFloat(SfxVolume, DefaultLinearVolume);
        wrote |= WriteDefaultFloat(DialogueVolume, DefaultLinearVolume);
        wrote |= WriteDefaultFloat(MouseSensitivity, DefaultLinearVolume);
        wrote |= WriteDefaultFloat(GamepadSensitivity, DefaultLinearVolume);
        if (wrote)
            PlayerPrefs.Save();
    }

    static bool WriteDefaultFloat(string key, float defaultValue)
    {
        if (PlayerPrefs.HasKey(key)) return false;
        PlayerPrefs.SetFloat(key, defaultValue);
        return true;
    }
}
