using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Switches between UI panels inside MainMenu scene (MainMenu / SelectHero / Settings / Multiplayer / Exit overlay).
/// </summary>
public class MainMenuPanelSwitcher : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;   // GameObject named "MainMenu"
    [SerializeField] private GameObject selectHeroPanel;  // GameObject named "SelectHero"
    [SerializeField] private GameObject settingsPanel;    // GameObject named "Settings"
    [SerializeField] private GameObject multiplayerPanel; // GameObject named "Multiplayer"

    [Header("Exit confirmation")]
    [SerializeField] private GameObject exitConfirmationRoot; // GameObject named "Exit"
    [Tooltip("NoBTN — used as default keyboard/gamepad selection when the dialog opens.")]
    [SerializeField] private GameObject exitDefaultSelectedNoButton;
    [Tooltip("Main menu ExitBTN — re-selected after closing the exit dialog (No / Escape).")]
    [SerializeField] private GameObject mainMenuExitButton;
    [Tooltip("Main menu SettingBTN — re-selected after closing Settings (Back / Escape).")]
    [SerializeField] private GameObject mainMenuSettingsButton;
    [Tooltip("Main menu MultiplayerBTN — re-selected after closing Multiplayer.")]
    [SerializeField] private GameObject mainMenuMultiplayerButton;
    [Tooltip("Multiplayer HostBTN — default selected when Multiplayer opens.")]
    [SerializeField] private GameObject multiplayerDefaultHostButton;

    private void Reset()
    {
        // Best-effort auto-find in case user uses Reset in Inspector.
        if (mainMenuPanel == null)
        {
            var go = GameObject.Find("MainMenu");
            if (go != null) mainMenuPanel = go;
        }

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

        if (exitConfirmationRoot == null)
        {
            var go = GameObject.Find("Exit");
            if (go != null) exitConfirmationRoot = go;
        }

        if (mainMenuExitButton == null)
        {
            var root = GameObject.Find("MainMenu");
            if (root != null)
            {
                var t = root.transform.Find("ExitBTN");
                if (t != null) mainMenuExitButton = t.gameObject;
            }
        }

        if (mainMenuSettingsButton == null)
        {
            var root = GameObject.Find("MainMenu");
            if (root != null)
            {
                var t = root.transform.Find("SettingBTN");
                if (t != null) mainMenuSettingsButton = t.gameObject;
            }
        }

        if (mainMenuMultiplayerButton == null)
        {
            var root = GameObject.Find("MainMenu");
            if (root != null)
            {
                var t = root.transform.Find("MultiplayerBTN");
                if (t != null) mainMenuMultiplayerButton = t.gameObject;
            }
        }

        if (multiplayerDefaultHostButton == null && multiplayerPanel != null)
        {
            var t = multiplayerPanel.transform.Find("SubmitPanel/HostBTN");
            if (t != null) multiplayerDefaultHostButton = t.gameObject;
        }
    }

    private void HideExitConfirmation()
    {
        if (exitConfirmationRoot != null) exitConfirmationRoot.SetActive(false);
    }

    public void ShowMainMenu()
    {
        HideExitConfirmation();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (selectHeroPanel != null) selectHeroPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(false);
    }

    public void ShowSelectHero()
    {
        HideExitConfirmation();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (selectHeroPanel != null) selectHeroPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(false);
    }

    public void ShowSettings()
    {
        HideExitConfirmation();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (selectHeroPanel != null) selectHeroPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(false);
    }

    public void ShowMultiplayer()
    {
        HideExitConfirmation();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (selectHeroPanel != null) selectHeroPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(true);
    }

    /// <summary>
    /// Hook this to "Single Player" button OnClick.
    /// </summary>
    public void OnSinglePlayerClicked()
    {
        ShowSelectHero();
    }

    /// <summary>
    /// Hook this to "Settings" button OnClick.
    /// </summary>
    public void OnSettingsClicked()
    {
        ShowSettings();
    }

    /// <summary>
    /// Hook this to "Multiplayer" button OnClick.
    /// </summary>
    public void OnMultiplayerClicked()
    {
        ShowMultiplayer();
        StartCoroutine(SelectMultiplayerHostNextFrame());
    }

    /// <summary>
    /// Hook this to "Back" button on the SelectHero panel.
    /// </summary>
    public void OnBackClicked()
    {
        ShowMainMenu();
    }

    /// <summary>
    /// Hook this to "Back" on the Settings panel (not SelectHero). Restores main menu with Settings button selected.
    /// </summary>
    public void OnBackFromSettingsClicked()
    {
        ShowMainMenu();
        StartCoroutine(SelectMainMenuSettingsButtonNextFrame());
    }

    /// <summary>
    /// Hook this to "Back" on the Multiplayer panel. Restores main menu with Multiplayer button selected.
    /// </summary>
    public void OnBackFromMultiplayerClicked()
    {
        ShowMainMenu();
        StartCoroutine(SelectMainMenuMultiplayerButtonNextFrame());
    }

    /// <summary>
    /// Hook this to main menu "Exit" button OnClick. Shows overlay; main menu stays underneath.
    /// </summary>
    public void OnExitClicked()
    {
        if (exitConfirmationRoot != null) exitConfirmationRoot.SetActive(true);
        StartCoroutine(SelectExitNoNextFrame());
    }

    /// <summary>
    /// Hook this to YesBTN OnClick.
    /// </summary>
    public void OnExitConfirmedQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Hook this to NoBTN OnClick.
    /// </summary>
    public void OnExitCancelled()
    {
        HideExitConfirmation();
        StartCoroutine(SelectMainMenuExitNextFrame());
    }

    private IEnumerator SelectMainMenuExitNextFrame()
    {
        yield return null;
        if (mainMenuExitButton == null || !mainMenuExitButton.activeInHierarchy) yield break;
        if (mainMenuPanel != null && !mainMenuPanel.activeInHierarchy) yield break;

        EventSystem es = EventSystem.current;
        if (es == null) yield break;

        var btn = mainMenuExitButton.GetComponent<Button>();
        GameObject toSelect = btn != null ? btn.gameObject : mainMenuExitButton;
        UIButtonHoverSfx.PrepareProgrammaticSelect(toSelect);
        es.SetSelectedGameObject(toSelect);
    }

    private IEnumerator SelectMainMenuSettingsButtonNextFrame()
    {
        yield return null;
        if (mainMenuSettingsButton == null || !mainMenuSettingsButton.activeInHierarchy) yield break;
        if (mainMenuPanel != null && !mainMenuPanel.activeInHierarchy) yield break;

        EventSystem es = EventSystem.current;
        if (es == null) yield break;

        var btn = mainMenuSettingsButton.GetComponent<Button>();
        GameObject toSelect = btn != null ? btn.gameObject : mainMenuSettingsButton;
        UIButtonHoverSfx.PrepareProgrammaticSelect(toSelect);
        es.SetSelectedGameObject(toSelect);
    }

    private IEnumerator SelectMainMenuMultiplayerButtonNextFrame()
    {
        yield return null;
        if (mainMenuMultiplayerButton == null || !mainMenuMultiplayerButton.activeInHierarchy) yield break;
        if (mainMenuPanel != null && !mainMenuPanel.activeInHierarchy) yield break;

        EventSystem es = EventSystem.current;
        if (es == null) yield break;

        var btn = mainMenuMultiplayerButton.GetComponent<Button>();
        GameObject toSelect = btn != null ? btn.gameObject : mainMenuMultiplayerButton;
        UIButtonHoverSfx.PrepareProgrammaticSelect(toSelect);
        es.SetSelectedGameObject(toSelect);
    }

    private IEnumerator SelectMultiplayerHostNextFrame()
    {
        yield return null;
        if (multiplayerDefaultHostButton == null || !multiplayerDefaultHostButton.activeInHierarchy) yield break;

        EventSystem es = EventSystem.current;
        if (es == null) yield break;

        var btn = multiplayerDefaultHostButton.GetComponent<Button>();
        GameObject toSelect = btn != null ? btn.gameObject : multiplayerDefaultHostButton;
        es.SetSelectedGameObject(toSelect);
    }

    private IEnumerator SelectExitNoNextFrame()
    {
        yield return null;
        if (exitDefaultSelectedNoButton == null || !exitDefaultSelectedNoButton.activeInHierarchy) yield break;

        EventSystem es = EventSystem.current;
        if (es == null) yield break;

        var btn = exitDefaultSelectedNoButton.GetComponent<Button>();
        GameObject toSelect = btn != null ? btn.gameObject : exitDefaultSelectedNoButton;
        UIButtonHoverSfx.PrepareProgrammaticSelect(toSelect);
        es.SetSelectedGameObject(toSelect);
    }
}
