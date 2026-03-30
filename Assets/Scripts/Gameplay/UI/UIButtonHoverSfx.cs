using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Audio;

/// <summary>
/// Plays a UI hover/select SFX for any Selectable across active panels (including Slider).
/// - Mouse hover: when pointer moves onto a different Selectable.
/// - Keyboard/Gamepad: when EventSystem selection changes to a different Selectable because the user navigated.
/// - When the user moves the mouse (or scrolls / clicks), EventSystem selection follows the hovered Selectable
///   so a keyboard/gamepad-selected control does not stay visually selected while another is under the cursor.
/// - When selection moves via keyboard/gamepad while the mouse is idle, a <c>pointerExit</c> is sent to the
///   Selectable still under the cursor so mouse hover highlight does not remain on the wrong control.
/// Programmatic default selection (panel open) must call <see cref="PrepareProgrammaticSelect"/> right before
/// <c>SetSelectedGameObject</c> so hover SFX does not play until the user actually moves over the control.
/// </summary>
public class UIButtonHoverSfx : MonoBehaviour
{
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] [Range(0f, 1f)] private float hoverVolume = 1f;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] [Range(0f, 1f)] private float clickVolume = 1f;
    [SerializeField] private AudioMixerGroup outputGroup;
    [Header("Optional click mute")]
    [SerializeField] private GameObject muteClickOnButton;

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
    private GameObject _lastHoveredButton;
    private GameObject _lastSelectedButton;
    private GameObject _lastPlayedButton;
    private int _lastPlayedFrame = -1;
    private readonly Dictionary<Button, UnityAction> _clickHandlers = new Dictionary<Button, UnityAction>(64);
    private float _nextButtonsRefreshAt;
    private AudioSource _uiSfxSource;
    private float _suppressNavHoverUntilUnscaledTime;
    /// <summary>Max left-stick squared magnitude from last frame (for deadzone cross detection).</summary>
    private float _prevStickMaxSq;

    private static UIButtonHoverSfx _instance;

    private const float StickDeadzoneSq = 0.12f;

    /// <summary>
    /// Call immediately before <c>EventSystem.SetSelectedGameObject</c> when selection is not from user
    /// pointer/navigation (e.g. default focus when a panel opens).
    /// </summary>
    public static void PrepareProgrammaticSelect(GameObject selectedOrChild)
    {
        if (_instance == null) return;

        if (selectedOrChild == null)
        {
            _instance._lastSelectedButton = null;
            return;
        }

        // Normalize to the Selectable root (Button/Slider/etc.) so TrackPointerHover/TrackSelection match.
        var selectable = selectedOrChild.GetComponentInParent<Selectable>();
        if (selectable != null && selectable.IsActive() && selectable.interactable)
            _instance._lastSelectedButton = selectable.gameObject;
        else
            _instance._lastSelectedButton = selectedOrChild;

        // Ignore held navigation for a moment so opening a panel does not count as navigation-hover.
        _instance._suppressNavHoverUntilUnscaledTime = Time.unscaledTime + 0.08f;
    }

    /// <summary>
    /// Plays the configured UI click clip (same as Button click) without a button target, e.g. gamepad LT/RT.
    /// </summary>
    public static void PlayClickSfx()
    {
        if (_instance == null) return;
        if (_instance.clickClip == null) return;
        _instance.PlaySfx(_instance.clickClip, _instance.clickVolume);
    }

    private void Awake()
    {
        LoadDefaultClipsFromResourcesIfNeeded();

        // MainMenu (or any scene) can Awake before PauseMenuController's RuntimeInitialize bootstrap.
        // The first UIButtonHoverSfx wins _instance; bootstrap then Destroy(this) and we lose the DDOL copy.
        // Later, unloading MainMenu destroys the only instance and _instance becomes null — no UI SFX in BattleArea.
        // Prefer the persistent PauseMenuController GameObject when both exist.
        if (_instance != null && _instance != this)
        {
            bool thisIsPauseBootstrap = GetComponent<PauseMenuController>() != null;
            if (thisIsPauseBootstrap)
                Destroy(_instance);
            else
            {
                Destroy(this);
                return;
            }
        }

        _instance = this;

        _uiSfxSource = gameObject.GetComponent<AudioSource>();
        if (_uiSfxSource == null)
            _uiSfxSource = gameObject.AddComponent<AudioSource>();

        _uiSfxSource.playOnAwake = false;
        _uiSfxSource.loop = false;
        _uiSfxSource.spatialBlend = 0f;
        _uiSfxSource.ignoreListenerPause = true;
        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("MenuItems");
        _uiSfxSource.outputAudioMixerGroup = outputGroup;
    }

    /// <summary>Loads <c>Assets/Resources/Audio/SFX/Hover</c> and <c>Click</c> when clips are not assigned in the Inspector.</summary>
    private void LoadDefaultClipsFromResourcesIfNeeded()
    {
        if (hoverClip == null)
            hoverClip = Resources.Load<AudioClip>("Audio/SFX/Hover");
        if (clickClip == null)
            clickClip = Resources.Load<AudioClip>("Audio/SFX/Click");
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextButtonsRefreshAt)
        {
            RefreshButtonClickListeners();
            _nextButtonsRefreshAt = Time.unscaledTime + 0.5f;
        }
    }

    private void LateUpdate()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        float curStickSq = GetMaxLeftStickSq();

        // Pointer → selection sync runs even when hover SFX clip is unset (Resources load may be empty).
        TrackPointerHover(es);
        TrackSelection(es, curStickSq);

        _prevStickMaxSq = curStickSq;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        foreach (var kv in _clickHandlers)
        {
            if (kv.Key != null) kv.Key.onClick.RemoveListener(kv.Value);
        }
        _clickHandlers.Clear();
    }

    private void TrackPointerHover(EventSystem es)
    {
        Vector2 pointerPos;
        if (Mouse.current != null)
            pointerPos = Mouse.current.position.ReadValue();
        else
            pointerPos = Input.mousePosition;

        var data = new PointerEventData(es) { position = pointerPos };
        _raycastResults.Clear();
        es.RaycastAll(data, _raycastResults);

        GameObject hoveredSelectable = null;
        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var go = _raycastResults[i].gameObject;
            if (go == null) continue;
            var selectable = go.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsActive() && selectable.interactable)
            {
                hoveredSelectable = selectable.gameObject;
                break;
            }
        }

        // When the user moves the mouse (or scrolls / clicks), make that control the EventSystem
        // selection so keyboard/gamepad "selected" highlight does not stay on another button.
        // Only run on real pointer activity so pure keyboard navigation is not overwritten while
        // the cursor happens to sit over a different widget.
        if (hoveredSelectable != null && IsMouseDrivingUiThisFrame())
        {
            GameObject selectedRoot = null;
            var cur = es.currentSelectedGameObject;
            if (cur != null)
            {
                var sel = cur.GetComponentInParent<Selectable>();
                if (sel != null && sel.IsActive() && sel.interactable)
                    selectedRoot = sel.gameObject;
            }

            if (selectedRoot != hoveredSelectable)
            {
                es.SetSelectedGameObject(hoveredSelectable);
                _lastSelectedButton = hoveredSelectable;
            }
        }

        if (hoverClip != null && hoveredSelectable != null && hoveredSelectable != _lastHoveredButton)
            PlayHover(hoveredSelectable);

        _lastHoveredButton = hoveredSelectable;
    }

    private static bool IsMouseDrivingUiThisFrame()
    {
        if (Mouse.current == null)
            return false;

        if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame ||
            Mouse.current.middleButton.wasPressedThisFrame)
            return true;

        if (Mouse.current.scroll.ReadValue().sqrMagnitude > 0.0001f)
            return true;

        return Mouse.current.delta.ReadValue().sqrMagnitude > 0.25f;
    }

    private void TrackSelection(EventSystem es, float curLeftStickSq)
    {
        GameObject selected = es.currentSelectedGameObject;
        GameObject selectedSelectable = null;

        if (selected != null)
        {
            var selectable = selected.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsActive() && selectable.interactable)
                selectedSelectable = selectable.gameObject;
        }

        if (selectedSelectable != _lastSelectedButton)
        {
            // Mouse did not drive UI this frame: selection changed via keyboard/gamepad (or code). Clear
            // pointer-over state on whatever is still under the cursor so it does not stay "highlighted".
            if (!IsMouseDrivingUiThisFrame())
                SendPointerExitToSelectableUnderCursorIfDifferentFromSelection(es, selectedSelectable);

            if (selectedSelectable != null && ShouldPlayHoverForSelectionChange(curLeftStickSq))
                PlayHover(selectedSelectable);
            _lastSelectedButton = selectedSelectable;
        }
    }

    private static void SendPointerExitToSelectableUnderCursorIfDifferentFromSelection(
        EventSystem es,
        GameObject newSelectedRoot)
    {
        if (es == null)
            return;

        Vector2 pointerPos;
        if (Mouse.current != null)
            pointerPos = Mouse.current.position.ReadValue();
        else
            pointerPos = Input.mousePosition;

        // Synthetic event; -1 matches legacy UI modules when kFakePointerId is not exposed.
        var data = new PointerEventData(es) { pointerId = -1, position = pointerPos };

        var results = new List<RaycastResult>(16);
        es.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            var go = results[i].gameObject;
            if (go == null)
                continue;

            var selectable = go.GetComponentInParent<Selectable>();
            if (selectable == null || !selectable.IsActive() || !selectable.interactable)
                continue;

            if (newSelectedRoot != null && selectable.gameObject == newSelectedRoot)
                return;

            ExecuteEvents.Execute(selectable.gameObject, data, ExecuteEvents.pointerExitHandler);
            return;
        }
    }

    /// <summary>
    /// True when selection moved because the user navigated (not automatic/programmatic focus).
    /// Mouse-driven focus uses <see cref="MainMenuButtonPointerSelectionSync"/> / pointer raycast instead.
    /// </summary>
    private bool ShouldPlayHoverForSelectionChange(float curLeftStickSq)
    {
        float now = Time.unscaledTime;
        bool stickDeflected = curLeftStickSq >= StickDeadzoneSq;
        bool stickJustCrossed = stickDeflected && _prevStickMaxSq < StickDeadzoneSq;
        bool strongThisFrame = WasStrongKeyboardOrDpadPressThisFrame() || stickJustCrossed;

        if (now < _suppressNavHoverUntilUnscaledTime && !strongThisFrame)
            return false;

        if (strongThisFrame)
            return true;

        var k = Keyboard.current;
        if (k != null)
        {
            if (k.upArrowKey.isPressed || k.downArrowKey.isPressed ||
                k.leftArrowKey.isPressed || k.rightArrowKey.isPressed ||
                k.wKey.isPressed || k.sKey.isPressed ||
                k.aKey.isPressed || k.dKey.isPressed)
                return true;
        }

        if (AnyGamepadDpadHeld() || stickDeflected)
            return true;

        return false;
    }

    private static bool WasStrongKeyboardOrDpadPressThisFrame()
    {
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.tabKey.wasPressedThisFrame) return true;
            if (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
                k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ||
                k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame)
                return true;
        }

        foreach (var gp in Gamepad.all)
        {
            if (gp == null) continue;
            if (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool AnyGamepadDpadHeld()
    {
        foreach (var gp in Gamepad.all)
        {
            if (gp == null) continue;
            if (gp.dpad.up.isPressed || gp.dpad.down.isPressed ||
                gp.dpad.left.isPressed || gp.dpad.right.isPressed)
                return true;
        }

        return false;
    }

    private static float GetMaxLeftStickSq()
    {
        float maxSq = 0f;
        foreach (var gp in Gamepad.all)
        {
            if (gp == null) continue;
            Vector2 v = gp.leftStick.ReadValue();
            float sq = v.sqrMagnitude;
            if (sq > maxSq) maxSq = sq;
        }

        return maxSq;
    }

    private void PlayHover(GameObject button)
    {
        if (button == null || hoverClip == null) return;

        // If a button sets both pointer-hover and selected in one frame, play once.
        if (_lastPlayedButton == button && _lastPlayedFrame == Time.frameCount) return;

        _lastPlayedButton = button;
        _lastPlayedFrame = Time.frameCount;
        PlaySfx(hoverClip, hoverVolume);
    }

    private void RefreshButtonClickListeners()
    {
        var buttons = Object.FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null || _clickHandlers.ContainsKey(button)) continue;

            Button capturedButton = button;
            UnityAction action = () => PlayClickFor(capturedButton);
            button.onClick.AddListener(action);
            _clickHandlers.Add(button, action);
        }
    }

    private void PlayClickFor(Button clickedButton)
    {
        if (clickedButton != null && muteClickOnButton != null && clickedButton.gameObject == muteClickOnButton)
            return;

        if (clickClip == null) return;
        PlaySfx(clickClip, clickVolume);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || _uiSfxSource == null) return;

        if (_uiSfxSource.outputAudioMixerGroup != outputGroup)
            _uiSfxSource.outputAudioMixerGroup = outputGroup;

        _uiSfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
