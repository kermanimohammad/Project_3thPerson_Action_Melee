using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// - Exit overlay: Escape / gamepad B cancels (same as No).
/// - Settings: Escape / gamepad B returns to main menu and selects SettingBTN.
/// - Multiplayer: Escape / gamepad B returns to main menu and selects MultiplayerBTN.
/// - SelectHero: Escape / gamepad B returns to main menu (same as Back).
/// </summary>
public class SelectHeroBackInput : MonoBehaviour
{
    [SerializeField] private MainMenuPanelSwitcher panelSwitcher;
    [SerializeField] private GameObject selectHeroPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject exitConfirmationPanel;

    private void Awake()
    {
        if (selectHeroPanel == null)
        {
            var go = GameObject.Find("SelectHero");
            if (go != null) selectHeroPanel = go;
        }

        if (settingsPanel == null)
        {
            var go = GameObject.Find("Settings");
            if (go != null) settingsPanel = go;
        }

        if (multiplayerPanel == null)
        {
            var go = GameObject.Find("Multiplayer");
            if (go != null) multiplayerPanel = go;
        }

        if (exitConfirmationPanel == null)
        {
            var go = GameObject.Find("Exit");
            if (go != null) exitConfirmationPanel = go;
        }
    }

    private void Update()
    {
        if (panelSwitcher == null) return;

        if (InputRebindSession.IsActive)
            return;

        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepadBackPressed = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame; // B on Xbox / Circle on PS

        if (exitConfirmationPanel != null && exitConfirmationPanel.activeInHierarchy)
        {
            if (escapePressed || gamepadBackPressed) panelSwitcher.OnExitCancelled();
            return;
        }

        if (settingsPanel != null && settingsPanel.activeInHierarchy)
        {
            if (escapePressed || gamepadBackPressed) panelSwitcher.OnBackFromSettingsClicked();
            return;
        }

        if (multiplayerPanel != null && multiplayerPanel.activeInHierarchy)
        {
            if (escapePressed || gamepadBackPressed) panelSwitcher.OnBackFromMultiplayerClicked();
            return;
        }

        if (selectHeroPanel != null && selectHeroPanel.activeInHierarchy)
        {
            if (escapePressed || gamepadBackPressed) panelSwitcher.OnBackClicked();
        }
    }
}

