using UnityEngine;

/// <summary>
/// Runtime read access to persisted menu settings (other scenes can use PlayerPrefs keys via this helper).
/// </summary>
public static class GameSettings
{
    public static float MouseSensitivity01 =>
        PlayerPrefs.GetFloat(MainMenuSettingsKeys.MouseSensitivity, MainMenuSettingsKeys.DefaultLinearVolume);

    public static float GamepadSensitivity01 =>
        PlayerPrefs.GetFloat(MainMenuSettingsKeys.GamepadSensitivity, MainMenuSettingsKeys.DefaultLinearVolume);
}
