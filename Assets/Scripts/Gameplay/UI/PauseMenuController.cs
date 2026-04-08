using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// BattleArea pause: Esc / Gamepad Start toggles timescale and shows Menu canvas.
/// Auto-bootstraps itself so no scene edits are required.
/// </summary>
public sealed class PauseMenuController : MonoBehaviour
{
    private const string SettingsUiConfigResourceName = "BattleAreaSettingsUI";
    private const string BattleAreaSceneName = "BattleArea";

    [Header("Scene wiring (optional)")]
    [Tooltip("If empty, will auto-find a GameObject named 'Menu' in the active scene.")]
    [SerializeField] private GameObject menuRoot;

    [Tooltip("If empty, will auto-find a GameObject named 'Canvas-HUD' in the active scene (kept visible during pause).")]
    [SerializeField] private GameObject hudRoot;

    [Tooltip("When paused, this UI element will be selected first (searched under Menu, inactive included).")]
    [SerializeField] private string defaultSelectedName = "ContinueBTN";

    [Tooltip("Button in BattleArea pause Menu that opens settings (searched under Menu).")]
    [SerializeField] private string settingsButtonName = "SettingsBTN";

    [Tooltip("Back buttons on Settings UI (all matches under Settings root; default BackBTN). BattleArea wires these at runtime.")]
    [SerializeField] private string settingsBackButtonName = "BackBTN";

    [Tooltip("Apply buttons (same as MainMenu ApplyBTN → SaveAllSettings). Wired at runtime in BattleArea.")]
    [SerializeField] private string settingsApplyButtonName = "ApplyBTN";

    [Tooltip("Scene that contains the Settings panel UI (reused from MainMenu).")]
    [SerializeField] private string settingsSceneName = "MainMenu";

    [Tooltip("Root GameObject name of the Settings panel in that scene.")]
    [SerializeField] private string settingsPanelName = "Settings";

    [Tooltip("Optional: loads a Settings UI prefab from Resources (path without extension). Example: UI/Settings")]
    [SerializeField] private string settingsPrefabResourcesPath = "";

    [Header("Quit to Main Menu")]
    [Tooltip("Button under pause Menu that opens the quit confirmation panel (default name in BattleArea).")]
    [SerializeField] private string quitToMainMenuMenuButtonName = "Quit to Main Menu BTN";

    [Tooltip("Root GameObject of the confirmation panel (scene instance or prefab root name).")]
    [SerializeField] private string quitConfirmPanelName = "Quit to Main Menu";

    [Tooltip("Loads panel from Resources if not found in the scene. Path without extension.")]
    [SerializeField] private string quitConfirmPanelResourcesPath = "Quit to Main Menu";

    [Tooltip("Scene loaded when the user confirms quit.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private BattleAreaSettingsUIConfig _settingsUiConfig;

    [Header("Settings sections to show (BattleArea)")]
    [SerializeField] private bool showGraphicsSettings = true;
    [SerializeField] private bool showAudioSettings = true;
    [SerializeField] private bool showControlsSettings = true;

    [Header("Behaviour")]
    [SerializeField] private bool pauseAudioListener = true;
    [SerializeField] private bool unlockCursorOnPause = true;

    private InputAction _pauseAction;
    private bool _paused;
    private System.Action<InputAction.CallbackContext> _onPausePerformed;
    private Button _continueButton;
    private Button _settingsButton;
    private Button _quitToMainMenuMenuButton;

    private GameObject _quitConfirmPanel;
    private bool _quitPanelOpen;
    private bool _quitLoadStarted;

    private bool _settingsOpen;
    private bool _settingsLoading;
    private Scene _settingsScene;
    private GameObject _settingsPanel;
    private Coroutine _settingsLoadRoutine;

    private AudioSource _battleMusicSource;
    /// <summary>True when battle music was explicitly <see cref="AudioSource.Pause"/>d because <see cref="pauseAudioListener"/> is off.</summary>
    private bool _battleMusicPausedExplicitlyForMenu;
    private AudioSource _pauseMenuMusicSource;

    /// <summary>Fallback if <see cref="BattleAreaSettingsUIConfig.battleGameplayMusicClip"/> is not set (file must live under a Resources folder).</summary>
    private const string BattleGameplayMusicResourcesPath = "Audio/Music/Battle/Emerald Bastion";

    /// <summary>Fallback if <see cref="BattleAreaSettingsUIConfig.pauseMenuMusicClip"/> is not set (file must live under a Resources folder).</summary>
    private const string PauseMenuMusicResourcesPath = "Audio/Music/Menu/Citadel at Dusk (BattleMenu)";

    private float _prevTimeScale = 1f;
    private CursorLockMode _prevCursorLock;
    private bool _prevCursorVisible;
    private bool _prevAudioPaused;

    /// <summary>Animators switched to UnscaledTime while paused so UI clips keep playing at timeScale 0.</summary>
    private readonly Dictionary<Animator, AnimatorUpdateMode> _animatorModesBeforePause = new();

    /// <summary>Last EventSystem selection under the active pause/settings UI; used when clicking empty space.</summary>
    private GameObject _lastSelectableUnderMenuScope;
    private readonly List<RaycastResult> _selectionRaycastScratch = new List<RaycastResult>(24);

    private static PauseMenuController _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("PauseMenuController (Bootstrap)");
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenuController>();
        go.AddComponent<UIButtonHoverSfx>();
    }

    private void Awake()
    {
        _instance = this;

        _settingsUiConfig = Resources.Load<BattleAreaSettingsUIConfig>(SettingsUiConfigResourceName);

        _pauseAction = new InputAction(
            name: "Pause",
            type: InputActionType.Button,
            binding: "<Keyboard>/escape");
        _pauseAction.AddBinding("<Gamepad>/start");
        _pauseAction.AddBinding("<Gamepad>/buttonEast");
        _onPausePerformed = OnPausePerformed;
        _pauseAction.performed += _onPausePerformed;
    }

    private void OnEnable()
    {
        _pauseAction?.Enable();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Entering Play Mode directly on BattleArea: sceneLoaded has already fired before we subscribed.
        if (IsBattleAreaActive())
            OnBattleAreaActivated();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _pauseAction?.Disable();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        if (_pauseAction != null)
        {
            if (_onPausePerformed != null)
                _pauseAction.performed -= _onPausePerformed;
            _pauseAction.Dispose();
            _pauseAction = null;
        }
    }

    /// <summary>Re-apply <see cref="AnimatorUpdateMode.UnscaledTime"/> after a Settings tab panel enables (Unity may reset update mode).</summary>
    public static void RefreshUiAnimatorsAfterSettingsTabChange()
    {
        var inst = _instance;
        if (inst == null || !inst._paused || !inst._settingsOpen || inst._settingsPanel == null)
            return;
        inst.RegisterUiAnimatorsUnscaledUnder(inst._settingsPanel);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_paused)
        {
            // Safety: if we leave BattleArea while paused, unpause so we don't freeze the next scene.
            // IMPORTANT: Do NOT unpause on additive loads (e.g. temporarily loading MainMenu to clone Settings UI).
            if (!IsBattleAreaActive() && mode == LoadSceneMode.Single)
                SetPaused(false);
        }

        // Only when BattleArea is the scene that loaded (not additive MainMenu for Settings clone).
        if (scene.name == BattleAreaSceneName)
            OnBattleAreaActivated();
        else if (mode == LoadSceneMode.Single)
        {
            StopBattleMusic();
            StopPauseMenuMusic();
        }
    }

    /// <summary>
    /// Same startup work as MainMenu <see cref="MainMenuSettingsCoordinator"/> (mixer + prefs volumes + quality)
    /// via <see cref="GameSettingsRuntime"/>, plus wiring and persisted input overrides after other Starts register assets.
    /// </summary>
    private void OnBattleAreaActivated()
    {
        GameSettingsRuntime.ApplyAllSavedSettingsToEngine();
        TryAutoWire();
        if (!_paused)
            PlayBattleMusicIfConfigured();
        StartCoroutine(ApplyPersistedInputBindingsNextFrame());
    }

    private IEnumerator ApplyPersistedInputBindingsNextFrame()
    {
        yield return null;
        InputBindingRuntimeSync.ApplySavedBindingsToAllRegistered();
    }

    private void Update()
    {
        if (!IsBattleAreaActive())
            return;

        // Menu object might appear after load (instantiation); keep trying.
        if (menuRoot == null)
            TryAutoWire();

        // Some gameplay scripts may re-lock/hide the cursor every frame.
        // While paused, we must keep it unlocked + visible for UI interaction.
        if (_paused && unlockCursorOnPause)
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsBattleAreaActive())
            return;

        TryAutoWire();

        if (_quitPanelOpen)
        {
            CloseQuitConfirmationPanel();
            return;
        }

        if (_settingsOpen)
        {
            CloseSettings();
            return;
        }

        // B / east face: only close Settings (handled above). Do not toggle pause from gameplay.
        if (IsGamepadEastFaceCancelButton(ctx.control))
            return;

        SetPaused(!_paused);
    }

    private static bool IsGamepadEastFaceCancelButton(InputControl control)
    {
        if (control == null)
            return false;
        var path = control.path;
        return path != null && path.IndexOf("buttonEast", System.StringComparison.Ordinal) >= 0;
    }

    private void SetPaused(bool paused)
    {
        if (_paused == paused)
            return;

        _paused = paused;

        if (paused)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (pauseAudioListener)
            {
                _prevAudioPaused = AudioListener.pause;
                AudioListener.pause = true;
            }

            if (unlockCursorOnPause)
            {
                _prevCursorLock = Cursor.lockState;
                _prevCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (menuRoot != null)
                menuRoot.SetActive(true);

            RegisterUiAnimatorsUnscaledUnder(menuRoot);
            FocusDefaultOrFirstSelectable();
            // Battle music: always pause explicitly (safer than relying on AudioListener.pause,
            // since other systems/sources may ignore listener pause).
            PauseBattleMusicForMenuExplicit();
            PlayPauseMenuMusicIfConfigured();
        }
        else
        {
            if (_quitPanelOpen)
                CloseQuitConfirmationPanel();

            if (_settingsOpen)
                CloseSettings();

            RestoreUiAnimatorUpdateModes();

            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;

            if (pauseAudioListener)
                AudioListener.pause = _prevAudioPaused;

            if (unlockCursorOnPause)
            {
                Cursor.lockState = _prevCursorLock;
                Cursor.visible = _prevCursorVisible;
            }

            if (menuRoot != null)
                menuRoot.SetActive(false);

            _lastSelectableUnderMenuScope = null;
            StopPauseMenuMusic();
            // Always resume explicitly if we paused it.
            ResumeBattleMusicAfterMenuExplicit();
        }
    }

    private void PauseBattleMusicForMenuExplicit()
    {
        EnsureBattleMusicSourceInitialized();
        if (_battleMusicSource == null || _battleMusicSource.clip == null)
            return;
        if (!_battleMusicSource.isPlaying)
            return;
        _battleMusicSource.Pause();
        _battleMusicPausedExplicitlyForMenu = true;
    }

    private void ResumeBattleMusicAfterMenuExplicit()
    {
        if (_battleMusicSource == null)
            return;
        if (!_battleMusicPausedExplicitlyForMenu)
            return;
        _battleMusicSource.UnPause();
        _battleMusicPausedExplicitlyForMenu = false;
    }

    private void EnsureBattleMusicSourceInitialized()
    {
        if (_battleMusicSource != null)
            return;

        AudioClip clip = null;
        bool loop = true;
        if (_settingsUiConfig != null)
        {
            clip = _settingsUiConfig.battleGameplayMusicClip;
            loop = _settingsUiConfig.loopBattleGameplayMusic;
        }

        if (clip == null)
            clip = Resources.Load<AudioClip>(BattleGameplayMusicResourcesPath);

        if (clip == null)
            return;

        _battleMusicSource = gameObject.AddComponent<AudioSource>();
        _battleMusicSource.playOnAwake = false;
        _battleMusicSource.loop = loop;
        _battleMusicSource.volume = 1f;
        _battleMusicSource.spatialBlend = 0f;
        _battleMusicSource.clip = clip;
        _battleMusicSource.ignoreListenerPause = false;
        GameAudioSettings.EnsureMixerRegistered();
        _battleMusicSource.outputAudioMixerGroup = GameAudioSettings.FindMixerGroup("Music");
    }

    /// <summary>Starts battle music if it is not already playing (does not restart from 0).</summary>
    private void PlayBattleMusicIfConfigured()
    {
        if (!IsBattleAreaActive())
            return;

        EnsureBattleMusicSourceInitialized();
        if (_battleMusicSource == null || _battleMusicSource.clip == null)
            return;

        if (_battleMusicPausedExplicitlyForMenu)
            return;

        if (_battleMusicSource.isPlaying)
            return;

        _battleMusicSource.Play();
    }

    private void StopBattleMusic()
    {
        if (_battleMusicSource == null)
            return;
        _battleMusicSource.Stop();
        _battleMusicPausedExplicitlyForMenu = false;
    }

    private void EnsurePauseMenuMusicSourceInitialized()
    {
        if (_pauseMenuMusicSource != null)
            return;

        AudioClip clip = null;
        bool loop = true;
        if (_settingsUiConfig != null)
        {
            clip = _settingsUiConfig.pauseMenuMusicClip;
            loop = _settingsUiConfig.loopPauseMenuMusic;
        }

        if (clip == null)
            clip = Resources.Load<AudioClip>(PauseMenuMusicResourcesPath);

        if (clip == null)
            return;

        _pauseMenuMusicSource = gameObject.AddComponent<AudioSource>();
        _pauseMenuMusicSource.playOnAwake = false;
        _pauseMenuMusicSource.loop = loop;
        _pauseMenuMusicSource.volume = 1f;
        _pauseMenuMusicSource.spatialBlend = 0f;
        _pauseMenuMusicSource.clip = clip;
        _pauseMenuMusicSource.ignoreListenerPause = true;
        GameAudioSettings.EnsureMixerRegistered();
        _pauseMenuMusicSource.outputAudioMixerGroup = GameAudioSettings.FindMixerGroup("Music");
    }

    private void PlayPauseMenuMusicIfConfigured()
    {
        if (!IsBattleAreaActive())
            return;

        EnsurePauseMenuMusicSourceInitialized();
        if (_pauseMenuMusicSource == null || _pauseMenuMusicSource.clip == null)
            return;

        _pauseMenuMusicSource.Stop();
        _pauseMenuMusicSource.Play();
    }

    private void StopPauseMenuMusic()
    {
        if (_pauseMenuMusicSource == null)
            return;
        _pauseMenuMusicSource.Stop();
    }

    private void LateUpdate()
    {
        if (!IsBattleAreaActive())
            return;

        if (_quitPanelOpen)
        {
            HandleQuitConfirmationShortcutInputs();
            if (_paused)
                PreserveMenuSelectionOnEmptyMouseClick();
            return;
        }

        if (!_paused)
            return;

        PreserveMenuSelectionOnEmptyMouseClick();
    }

    /// <summary>
    /// If the user left-clicks UI (or world) where there is no interactable under the active menu/settings root,
    /// keep the previous EventSystem selection so focus/highlight does not clear.
    /// </summary>
    private void PreserveMenuSelectionOnEmptyMouseClick()
    {
        var es = EventSystem.current;
        if (es == null)
            return;

        Transform scope = GetMenuSelectionScopeTransform();
        if (scope == null)
            return;

        var cur = es.currentSelectedGameObject;
        if (cur != null)
        {
            var sel = cur.GetComponentInParent<Selectable>();
            if (sel != null && sel.IsActive() && sel.interactable && IsUnderMenuScope(sel.transform, scope))
                _lastSelectableUnderMenuScope = sel.gameObject;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        Vector2 screenPos = mouse.position.ReadValue();
        var hitSelectable = GetTopInteractableSelectableUnderPointer(screenPos, es);
        if (hitSelectable != null && IsUnderMenuScope(hitSelectable.transform, scope))
            return;

        if (_lastSelectableUnderMenuScope == null)
            return;

        UIButtonHoverSfx.PrepareProgrammaticSelect(_lastSelectableUnderMenuScope);
        es.SetSelectedGameObject(_lastSelectableUnderMenuScope);
        var restore = _lastSelectableUnderMenuScope.GetComponent<Selectable>();
        if (restore != null)
            restore.Select();
    }

    private Transform GetMenuSelectionScopeTransform()
    {
        if (_quitPanelOpen && _quitConfirmPanel != null && _quitConfirmPanel.activeInHierarchy)
            return _quitConfirmPanel.transform;
        if (_settingsOpen && _settingsPanel != null && _settingsPanel.activeInHierarchy)
            return _settingsPanel.transform;
        if (menuRoot != null && menuRoot.activeInHierarchy)
            return menuRoot.transform;
        return null;
    }

    private static bool IsUnderMenuScope(Transform t, Transform scopeRoot)
    {
        if (t == null || scopeRoot == null)
            return false;
        return t == scopeRoot || t.IsChildOf(scopeRoot);
    }

    private Selectable GetTopInteractableSelectableUnderPointer(Vector2 screenPos, EventSystem es)
    {
        var data = new PointerEventData(es) { position = screenPos };
        _selectionRaycastScratch.Clear();
        es.RaycastAll(data, _selectionRaycastScratch);
        for (int i = 0; i < _selectionRaycastScratch.Count; i++)
        {
            var go = _selectionRaycastScratch[i].gameObject;
            if (go == null)
                continue;
            var s = go.GetComponentInParent<Selectable>();
            if (s != null && s.IsActive() && s.interactable)
                return s;
        }

        return null;
    }

    private void TryAutoWire()
    {
        // GameObject.Find() cannot find inactive objects. Our Menu is inactive by default in BattleArea,
        // so we must search scene roots including inactive hierarchies.
        if (menuRoot == null)
            menuRoot = FindInActiveSceneIncludingInactive("Menu");

        if (hudRoot == null)
        {
            // Backward/scene-variant compatibility: some scenes use "HUD" under a root "Canvas"
            // instead of the older "Canvas-HUD" root name.
            hudRoot = FindInActiveSceneIncludingInactive("Canvas-HUD");
            if (hudRoot == null)
                hudRoot = FindInActiveSceneIncludingInactive("HUD");
        }

        WireContinueButtonIfAny();
        WireSettingsButtonIfAny();
        WireQuitToMainMenuMenuButtonIfAny();

        // Ensure pause menu starts hidden when entering BattleArea.
        if (menuRoot != null && !_paused && menuRoot.activeSelf)
            menuRoot.SetActive(false);
    }

    private void WireContinueButtonIfAny()
    {
        if (menuRoot == null)
            return;

        var go = FindByNameUnder(menuRoot.transform, "ContinueBTN");
        if (go == null)
            return;

        var btn = go.GetComponent<Button>();
        if (btn == null)
            return;

        if (_continueButton != null && _continueButton != btn)
            _continueButton.onClick.RemoveListener(OnContinueClicked);

        _continueButton = btn;
        _continueButton.onClick.RemoveListener(OnContinueClicked);
        _continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void OnContinueClicked()
    {
        if (!IsBattleAreaActive())
            return;

        SetPaused(false);
    }

    private void WireQuitToMainMenuMenuButtonIfAny()
    {
        if (menuRoot == null)
            return;

        var go = FindByNameUnder(menuRoot.transform, quitToMainMenuMenuButtonName);
        if (go == null)
            return;

        var btn = go.GetComponent<Button>();
        if (btn == null)
            return;

        if (_quitToMainMenuMenuButton != null && _quitToMainMenuMenuButton != btn)
            _quitToMainMenuMenuButton.onClick.RemoveListener(OnQuitToMainMenuMenuButtonClicked);

        _quitToMainMenuMenuButton = btn;
        _quitToMainMenuMenuButton.onClick.RemoveListener(OnQuitToMainMenuMenuButtonClicked);
        _quitToMainMenuMenuButton.onClick.AddListener(OnQuitToMainMenuMenuButtonClicked);
    }

    private void OnQuitToMainMenuMenuButtonClicked()
    {
        if (!IsBattleAreaActive() || !_paused)
            return;

        OpenQuitConfirmationPanel();
    }

    private void EnsureQuitConfirmPanelExists()
    {
        if (_quitConfirmPanel != null)
            return;

        _quitConfirmPanel = FindInActiveSceneIncludingInactive(quitConfirmPanelName);
        if (_quitConfirmPanel == null && !string.IsNullOrWhiteSpace(quitConfirmPanelResourcesPath))
        {
            var prefab = Resources.Load<GameObject>(quitConfirmPanelResourcesPath);
            if (prefab != null)
            {
                Transform parent = menuRoot != null ? menuRoot.transform.root : null;
                _quitConfirmPanel = parent != null
                    ? Instantiate(prefab, parent, false)
                    : Instantiate(prefab);
                _quitConfirmPanel.name = quitConfirmPanelName;
            }
        }

        if (_quitConfirmPanel != null)
            _quitConfirmPanel.SetActive(false);
    }

    /// <summary>
    /// Clears inspector (persistent) and runtime onClick, then wires BattleArea behaviour.
    /// <see cref="UnityEvent.RemoveAllListeners"/> alone does not remove serialized (inspector) calls.
    /// </summary>
    private void WireQuitConfirmPanelButtons()
    {
        if (_quitConfirmPanel == null)
            return;

        var yesGo = FindByNameUnder(_quitConfirmPanel.transform, "YesBTN");
        var noGo = FindByNameUnder(_quitConfirmPanel.transform, "NoBTN");

        if (yesGo != null && yesGo.TryGetComponent<Button>(out var yesBtn))
        {
            ClearButtonOnClickPersistentAndRuntime(yesBtn);
            yesBtn.onClick.AddListener(OnConfirmQuitToMainMenuClicked);
        }

        if (noGo != null && noGo.TryGetComponent<Button>(out var noBtn))
        {
            ClearButtonOnClickPersistentAndRuntime(noBtn);
            noBtn.onClick.AddListener(CloseQuitConfirmationPanel);
        }
    }

    /// <summary>
    /// Some Unity versions expose <c>UnityEventBase.RemoveAllPersistentListeners</c>; others do not.
    /// Falls back to clearing the internal persistent call group via reflection when needed.
    /// </summary>
    private static void ClearButtonOnClickPersistentAndRuntime(Button button)
    {
        if (button == null)
            return;

        var onClick = button.onClick;
        var unityEventBase = (UnityEventBase)(object)onClick;

        var removePersistent = typeof(UnityEventBase).GetMethod(
            "RemoveAllPersistentListeners",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (removePersistent != null)
            removePersistent.Invoke(unityEventBase, null);
        else
        {
            var field = typeof(UnityEventBase).GetField(
                "m_PersistentCalls",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                var pcg = field.GetValue(unityEventBase);
                if (pcg != null)
                {
                    var clear = pcg.GetType().GetMethod(
                        "Clear",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    clear?.Invoke(pcg, null);
                }
            }
        }

        onClick.RemoveAllListeners();
    }

    private void OpenQuitConfirmationPanel()
    {
        EnsureQuitConfirmPanelExists();
        if (_quitConfirmPanel == null)
            return;

        // New dialog session: avoid a stuck state if LoadScene never ran after a spurious confirm.
        _quitLoadStarted = false;

        _quitConfirmPanel.transform.SetAsLastSibling();
        _quitConfirmPanel.SetActive(true);
        _quitPanelOpen = true;

        WireQuitConfirmPanelButtons();

        RegisterUiAnimatorsUnscaledUnder(_quitConfirmPanel);

        var es = EventSystem.current;
        var noGo = FindByNameUnder(_quitConfirmPanel.transform, "NoBTN");
        if (es != null && noGo != null && noGo.TryGetComponent<Selectable>(out var noSel))
        {
            UIButtonHoverSfx.PrepareProgrammaticSelect(noGo);
            es.SetSelectedGameObject(noGo);
            noSel.Select();
        }
    }

    private void CloseQuitConfirmationPanel()
    {
        if (!_quitPanelOpen)
            return;

        _quitPanelOpen = false;
        UnregisterUiAnimatorsUnder(_quitConfirmPanel);

        if (_quitConfirmPanel != null)
            _quitConfirmPanel.SetActive(false);

        if (_paused && menuRoot != null && menuRoot.activeInHierarchy)
        {
            var es = EventSystem.current;
            var quitBtnGo = _quitToMainMenuMenuButton != null ? _quitToMainMenuMenuButton.gameObject : null;
            if (quitBtnGo == null && menuRoot != null)
                quitBtnGo = FindByNameUnder(menuRoot.transform, quitToMainMenuMenuButtonName);

            if (es != null && quitBtnGo != null && quitBtnGo.TryGetComponent<Selectable>(out var sel))
            {
                UIButtonHoverSfx.PrepareProgrammaticSelect(quitBtnGo);
                es.SetSelectedGameObject(quitBtnGo);
                sel.Select();
            }
        }
    }

    private void UnregisterUiAnimatorsUnder(GameObject root)
    {
        if (root == null)
            return;

        var animators = root.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            var a = animators[i];
            if (a == null)
                continue;

            if (_animatorModesBeforePause.TryGetValue(a, out var mode))
            {
                a.updateMode = mode;
                _animatorModesBeforePause.Remove(a);
            }
        }
    }

    private void HandleQuitConfirmationShortcutInputs()
    {
        if (!_quitPanelOpen)
            return;

        // While loading, ignore shortcuts (scene is changing).
        if (_quitLoadStarted)
            return;

        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame)
        {
            CloseQuitConfirmationPanel();
            return;
        }

        foreach (var gp in Gamepad.all)
        {
            if (gp == null)
                continue;
            if (gp.buttonEast.wasPressedThisFrame)
            {
                CloseQuitConfirmationPanel();
                return;
            }
        }

        // Enter / South only when Yes is selected — global confirm raced UI Submit while No was default-focused.
        if (!IsQuitYesButtonOrChildSelected())
            return;

        if (k != null &&
            (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
        {
            OnConfirmQuitToMainMenuClicked();
            return;
        }

        foreach (var gp in Gamepad.all)
        {
            if (gp == null)
                continue;
            if (gp.buttonSouth.wasPressedThisFrame)
            {
                OnConfirmQuitToMainMenuClicked();
                return;
            }
        }
    }

    private bool IsQuitYesButtonOrChildSelected()
    {
        if (_quitConfirmPanel == null)
            return false;

        var es = EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        if (sel == null)
            return false;

        var yesGo = FindByNameUnder(_quitConfirmPanel.transform, "YesBTN");
        if (yesGo == null)
            return false;

        return sel == yesGo || sel.transform.IsChildOf(yesGo.transform);
    }

    private void OnConfirmQuitToMainMenuClicked()
    {
        if (!IsBattleAreaActive() || _quitLoadStarted)
            return;

        _quitLoadStarted = true;
        // Prevent a 1-frame "music blast" when leaving while paused:
        // stop any battle/pause menu music BEFORE unpausing the AudioListener.
        StopBattleMusic();
        StopPauseMenuMusic();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void WireSettingsButtonIfAny()
    {
        if (menuRoot == null)
            return;

        var go = FindByNameUnder(menuRoot.transform, settingsButtonName);
        if (go == null)
            return;

        var btn = go.GetComponent<Button>();
        if (btn == null)
            return;

        if (_settingsButton != null && _settingsButton != btn)
            _settingsButton.onClick.RemoveListener(OnSettingsClicked);

        _settingsButton = btn;
        _settingsButton.onClick.RemoveListener(OnSettingsClicked);
        _settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    private void WireSettingsBackButtonsIfAny()
    {
        if (_settingsPanel == null || string.IsNullOrWhiteSpace(settingsBackButtonName))
            return;

        var transforms = _settingsPanel.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t.name != settingsBackButtonName)
                continue;

            var btn = t.GetComponent<Button>();
            if (btn == null)
                continue;

            btn.onClick.RemoveListener(OnSettingsBackClicked);
            btn.onClick.AddListener(OnSettingsBackClicked);
        }
    }

    private void WireSettingsApplyButtonsIfAny()
    {
        if (_settingsPanel == null || string.IsNullOrWhiteSpace(settingsApplyButtonName))
            return;

        var transforms = _settingsPanel.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t.name != settingsApplyButtonName)
                continue;

            var btn = t.GetComponent<Button>();
            if (btn == null)
                continue;

            btn.onClick.RemoveListener(OnSettingsApplyClicked);
            btn.onClick.AddListener(OnSettingsApplyClicked);
        }
    }

    private void OnSettingsApplyClicked()
    {
        if (!IsBattleAreaActive())
            return;

        if (!_settingsOpen)
            return;

        GameSettingsPersistence.SaveAllSettingsToPlayerPrefs();
    }

    private void OnSettingsBackClicked()
    {
        if (!IsBattleAreaActive())
            return;

        if (!_settingsOpen)
            return;

        CloseSettings();
    }

    private void OnSettingsClicked()
    {
        if (!IsBattleAreaActive())
            return;

        if (!_paused)
            SetPaused(true);

        OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsOpen)
            return;

        if (_settingsLoading)
            return;

        if (_settingsLoadRoutine != null)
            StopCoroutine(_settingsLoadRoutine);

        _settingsLoadRoutine = StartCoroutine(OpenSettingsRoutine());
    }

    private void InstantiateSettingsPanelFromPrefab(GameObject prefab, bool stripNonUi)
    {
        if (prefab == null || _settingsPanel != null)
            return;

        Transform parent = null;
        if (hudRoot != null) parent = hudRoot.transform;
        else if (menuRoot != null) parent = menuRoot.transform.parent;

        _settingsPanel = parent != null
            ? Instantiate(prefab, parent, worldPositionStays: false)
            : Instantiate(prefab);

        _settingsPanel.name = settingsPanelName + " (BattleArea)";
        _settingsPanel.SetActive(false);

        if (stripNonUi)
            StripNonUiObjectsFromHierarchy(_settingsPanel);
    }

    private IEnumerator OpenSettingsRoutine()
    {
        _settingsLoading = true;
        GameSettingsRuntime.ApplyAllSavedSettingsToEngine();

        // 1) Project asset (drag prefab onto BattleAreaSettingsUI ScriptableObject in Resources).
        if (_settingsPanel == null && _settingsUiConfig != null && _settingsUiConfig.settingsPanelPrefab != null)
            InstantiateSettingsPanelFromPrefab(_settingsUiConfig.settingsPanelPrefab, stripNonUi: false);

        // 2) Direct prefab in Resources folder (path string).
        if (_settingsPanel == null && !string.IsNullOrWhiteSpace(settingsPrefabResourcesPath))
        {
            var prefab = Resources.Load<GameObject>(settingsPrefabResourcesPath);
            if (prefab != null)
                InstantiateSettingsPanelFromPrefab(prefab, stripNonUi: false);
        }

        if (_settingsPanel != null)
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);

            _settingsPanel.SetActive(true);
            ApplySettingsSectionFilter(_settingsPanel);

            _settingsOpen = true;
            _settingsLoading = false;
            SelectGraphicsTabOrFallback(_settingsPanel);
            GameSettingsUiSync.SyncSettingsUiFromPlayerPrefs(_settingsPanel);
            RegisterUiAnimatorsUnscaledUnder(_settingsPanel);
            WireSettingsBackButtonsIfAny();
            WireSettingsApplyButtonsIfAny();
            yield break;
        }

        // Load Settings scene additively if needed.
        if (!_settingsScene.IsValid() || !_settingsScene.isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(settingsSceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                _settingsLoading = false;
                yield break;
            }

            while (!op.isDone)
                yield return null;

            _settingsScene = SceneManager.GetSceneByName(settingsSceneName);
        }

        // Prevent any non-UI gameplay/environment objects from the additive scene (Fire/Torch/etc) from appearing.
        // We only need the Settings hierarchy as a prefab source.
        DisableAllRootsInScene(_settingsScene);

        // Locate Settings panel inside loaded scene (inactive included).
        var sourceSettings = FindInSceneIncludingInactive(_settingsScene, settingsPanelName);
        if (sourceSettings == null)
        {
            _settingsLoading = false;
            yield break;
        }

        // Instantiate a runtime copy into BattleArea, so we don't keep the whole MainMenu UI around.
        // (This avoids copy/paste between scenes while still reusing the same Settings hierarchy.)
        if (_settingsPanel == null)
            InstantiateSettingsPanelFromPrefab(sourceSettings, stripNonUi: true);

        // We no longer need the additive MainMenu scene once we have the clone.
        // (Safe even if it was already loaded; this controller is BattleArea-only.)
        if (_settingsScene.IsValid() && _settingsScene.isLoaded)
        {
            var unload = SceneManager.UnloadSceneAsync(_settingsScene);
            while (unload != null && !unload.isDone)
                yield return null;
            _settingsScene = default;
        }

        // Hide pause menu, show settings.
        if (menuRoot != null)
            menuRoot.SetActive(false);

        _settingsPanel.SetActive(true);
        ApplySettingsSectionFilter(_settingsPanel);

        _settingsOpen = true;
        _settingsLoading = false;

        // Ensure Graphics tab is active + selected by default (and panels toggle accordingly).
        SelectGraphicsTabOrFallback(_settingsPanel);
        GameSettingsUiSync.SyncSettingsUiFromPlayerPrefs(_settingsPanel);
        RegisterUiAnimatorsUnscaledUnder(_settingsPanel);
        WireSettingsBackButtonsIfAny();
        WireSettingsApplyButtonsIfAny();
    }

    private void RegisterUiAnimatorsUnscaledUnder(GameObject root)
    {
        if (root == null || !_paused)
            return;

        var animators = root.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            var a = animators[i];
            if (a == null)
                continue;

            if (!_animatorModesBeforePause.ContainsKey(a))
                _animatorModesBeforePause[a] = a.updateMode;

            a.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    private void RestoreUiAnimatorUpdateModes()
    {
        if (_animatorModesBeforePause.Count == 0)
            return;

        foreach (var kv in _animatorModesBeforePause)
        {
            if (kv.Key != null)
                kv.Key.updateMode = kv.Value;
        }

        _animatorModesBeforePause.Clear();
    }

    private void CloseSettings()
    {
        if (!_settingsOpen)
            return;

        _settingsOpen = false;

        SaveAllSettingsIn(_settingsPanel);

        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        // Show pause menu again (still paused); focus Settings (user came from there).
        if (_paused && menuRoot != null)
        {
            menuRoot.SetActive(true);
            StartCoroutine(SelectPauseMenuSettingsButtonNextFrame());
        }
    }

    private IEnumerator SelectPauseMenuSettingsButtonNextFrame()
    {
        yield return null;

        WireSettingsButtonIfAny();
        if (menuRoot == null || !menuRoot.activeInHierarchy)
            yield break;

        Selectable target = _settingsButton;
        if (target == null)
        {
            var go = FindByNameUnder(menuRoot.transform, settingsButtonName);
            if (go != null)
                target = go.GetComponent<Selectable>();
        }

        var es = EventSystem.current;
        if (es == null)
            yield break;

        if (target == null || !target.IsActive() || !target.IsInteractable())
        {
            FocusDefaultOrFirstSelectable();
            yield break;
        }

        UIButtonHoverSfx.PrepareProgrammaticSelect(target.gameObject);
        es.SetSelectedGameObject(null);
        es.SetSelectedGameObject(target.gameObject);
        target.Select();
    }

    private static GameObject FindInActiveSceneIncludingInactive(string exactName)
    {
        if (string.IsNullOrWhiteSpace(exactName))
            return null;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            if (root.name == exactName)
                return root;

            var t = root.transform.Find(exactName);
            if (t != null)
                return t.gameObject;

            // Full traversal (inactive included)
            var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int j = 0; j < transforms.Length; j++)
            {
                var tt = transforms[j];
                if (tt != null && tt.name == exactName)
                    return tt.gameObject;
            }
        }

        return null;
    }

    private bool IsBattleAreaActive()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name == BattleAreaSceneName;
    }

    private void FocusDefaultOrFirstSelectable()
    {
        var es = EventSystem.current;
        if (es == null || menuRoot == null)
            return;

        Selectable selectable = null;

        if (!string.IsNullOrWhiteSpace(defaultSelectedName))
        {
            var go = FindByNameUnder(menuRoot.transform, defaultSelectedName);
            if (go != null)
                selectable = go.GetComponent<Selectable>();
        }

        if (selectable == null)
            selectable = menuRoot.GetComponentInChildren<Selectable>(includeInactive: true);

        if (selectable == null)
            return;

        es.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }

    private static void FocusFirstSelectableUnder(GameObject root)
    {
        var es = EventSystem.current;
        if (es == null || root == null)
            return;

        var selectable = root.GetComponentInChildren<Selectable>(includeInactive: true);
        if (selectable == null)
            return;

        es.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }

    private static GameObject FindInSceneIncludingInactive(Scene scene, string exactName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(exactName))
            return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            if (root.name == exactName)
                return root;

            var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int j = 0; j < transforms.Length; j++)
            {
                var t = transforms[j];
                if (t != null && t.name == exactName)
                    return t.gameObject;
            }
        }
        return null;
    }

    private static void DisableAllRootsInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;
            if (root.activeSelf)
                root.SetActive(false);
        }
    }

    private static void DisableEverythingInSceneExcept(Scene scene, GameObject keep)
    {
        if (!scene.IsValid() || !scene.isLoaded || keep == null)
            return;

        var keepSet = new HashSet<GameObject>();
        var t = keep.transform;
        while (t != null)
        {
            keepSet.Add(t.gameObject);
            t = t.parent;
        }

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            bool shouldKeepRoot = IsAncestorOrSelfInSet(root.transform, keepSet);
            if (!shouldKeepRoot)
            {
                root.SetActive(false);
                continue;
            }

            // For kept roots, disable siblings not in ancestry.
            DisableNonKeptChildren(root.transform, keepSet);
        }
    }

    private void ApplySettingsSectionFilter(GameObject settingsRoot)
    {
        if (settingsRoot == null)
            return;

        // Panels
        var graphicsPanel = FindByNameUnder(settingsRoot.transform, "Graphics-panel");
        var audioPanel = FindByNameUnder(settingsRoot.transform, "Audio-panel");
        var controlsPanel = FindByNameUnder(settingsRoot.transform, "Controls-panel");

        // Only force-disable sections that are not allowed.
        // Do NOT force-enable panels here; SettingsTabController controls which one is active (Graphics by default).
        if (!showGraphicsSettings && graphicsPanel != null) graphicsPanel.SetActive(false);
        if (!showAudioSettings && audioPanel != null) audioPanel.SetActive(false);
        if (!showControlsSettings && controlsPanel != null) controlsPanel.SetActive(false);

        // Tabs (optional)
        var graphicsTab = FindByNameUnder(settingsRoot.transform, "GraphicsTab");
        var audioTab = FindByNameUnder(settingsRoot.transform, "AudioTab");
        var controlsTab = FindByNameUnder(settingsRoot.transform, "ControlsTab");
        if (graphicsTab != null) graphicsTab.SetActive(showGraphicsSettings);
        if (audioTab != null) audioTab.SetActive(showAudioSettings);
        if (controlsTab != null) controlsTab.SetActive(showControlsSettings);

        // If only one section is enabled, there's no reason to keep the tab controller logic running.
        int enabledCount = (showGraphicsSettings ? 1 : 0) + (showAudioSettings ? 1 : 0) + (showControlsSettings ? 1 : 0);
        var tabController = settingsRoot.GetComponentInChildren<SettingsTabController>(includeInactive: true);
        if (tabController != null)
            tabController.enabled = enabledCount > 1;
    }

    private void SelectGraphicsTabOrFallback(GameObject settingsRoot)
    {
        if (settingsRoot == null)
            return;

        var tabController = settingsRoot.GetComponentInChildren<SettingsTabController>(includeInactive: true);
        if (tabController != null && tabController.isActiveAndEnabled && showGraphicsSettings)
        {
            tabController.SelectGraphicsTab();
            return;
        }

        // Fallback: focus first selectable in settings.
        FocusFirstSelectableUnder(settingsRoot);
    }

    private static void StripNonUiObjectsFromHierarchy(GameObject root)
    {
        if (root == null)
            return;

        // Destroy children that are clearly not UI. We keep anything that has a RectTransform (Unity UI),
        // and we keep ancestors of UI elements.
        var all = root.GetComponentsInChildren<Transform>(includeInactive: true);

        // Build keep-set: all objects that are UI (RectTransform) + their ancestors up to root.
        var keep = new HashSet<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (t.GetComponent<RectTransform>() == null) continue;

            var p = t;
            while (p != null)
            {
                keep.Add(p.gameObject);
                if (p.gameObject == root) break;
                p = p.parent;
            }
        }

        // Remove obvious decorative/non-UI objects.
        // Iterate from leaves upwards so child destruction is safe.
        for (int i = all.Length - 1; i >= 0; i--)
        {
            var t = all[i];
            if (t == null) continue;
            var go = t.gameObject;
            if (go == null) continue;
            if (go == root) continue;

            if (keep.Contains(go))
                continue;

            // Extra safety: strip common decorative names even if they accidentally have keep ancestors.
            string n = go.name ?? string.Empty;
            if (n.StartsWith("Fire") || n.StartsWith("Torch") || n.Contains("Flame"))
            {
                Object.Destroy(go);
                continue;
            }

            // Default: destroy anything outside UI tree.
            Object.Destroy(go);
        }
    }

    private static void SaveAllSettingsIn(GameObject root)
    {
        if (root == null) return;
        // Same persistence as MainMenu Apply: all matching sliders in the loaded scenes (settings panel is active).
        GameSettingsPersistence.SaveAllSettingsToPlayerPrefs();
    }

    private static bool IsAncestorOrSelfInSet(Transform root, HashSet<GameObject> keepSet)
    {
        if (root == null) return false;
        if (keepSet.Contains(root.gameObject)) return true;

        for (int i = 0; i < root.childCount; i++)
        {
            if (IsAncestorOrSelfInSet(root.GetChild(i), keepSet))
                return true;
        }
        return false;
    }

    private static void DisableNonKeptChildren(Transform root, HashSet<GameObject> keepSet)
    {
        if (root == null) return;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null) continue;

            if (!IsAncestorOrSelfInSet(child, keepSet))
            {
                child.gameObject.SetActive(false);
            }
            else
            {
                DisableNonKeptChildren(child, keepSet);
            }
        }
    }

    private static GameObject FindByNameUnder(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrWhiteSpace(exactName))
            return null;

        var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t != null && t.name == exactName)
                return t.gameObject;
        }

        return null;
    }
}

