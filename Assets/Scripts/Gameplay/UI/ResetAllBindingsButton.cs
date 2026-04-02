using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Hook to the "Reset All" UI button.
/// Clears all saved Input System binding overrides and refreshes UI binding rows.
/// </summary>
public class ResetAllBindingsButton : MonoBehaviour
{
    [Tooltip("If true, all InputRebindButton + FloatSettingSlider in the scene will refresh immediately after reset.")]
    [SerializeField] private bool refreshUi = true;

    public void OnResetAllClicked()
    {
        // 1) Reset binding overrides for every InputActionAsset used by InputRebindButton rows.
        var rows = FindObjectsOfType<InputRebindButton>(true);
        var uniqueAssets = new HashSet<InputActionAsset>();
        for (int i = 0; i < rows.Length; i++)
        {
            var asset = rows[i].GetActionsAsset();
            if (asset != null)
                uniqueAssets.Add(asset);
        }

        foreach (var asset in uniqueAssets)
            InputRebindPersistence.Clear(asset);

        InputBindingRuntimeSync.ApplySavedBindingsToAllRegistered();

        // 2) Reset Look sensitivity sliders (Mouse + Gamepad) to defaults.
        PlayerPrefs.SetFloat(MainMenuSettingsKeys.MouseSensitivity, MainMenuSettingsKeys.DefaultLinearVolume);
        PlayerPrefs.SetFloat(MainMenuSettingsKeys.GamepadSensitivity, MainMenuSettingsKeys.DefaultLinearVolume);
        PlayerPrefs.Save();

        // 3) Refresh UI.
        if (!refreshUi)
            return;

        for (int i = 0; i < rows.Length; i++)
            rows[i].ForceRefresh();

        var sliders = FindObjectsOfType<FloatSettingSlider>(true);
        for (int i = 0; i < sliders.Length; i++)
            sliders[i].ForceRefreshFromPlayerPrefs();
    }
}

