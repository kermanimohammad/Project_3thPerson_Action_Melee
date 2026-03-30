using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Attach to a UI Button representing one binding. Flow:
/// - Click arms the button (user sees it's ready).
/// - Press Enter / Submit starts interactive rebind.
/// - Next keyboard key or mouse button becomes the binding override.
/// - Override is saved to PlayerPrefs and the button label updates.
/// </summary>
[RequireComponent(typeof(Button))]
public class InputRebindButton : MonoBehaviour, IPointerClickHandler, ISubmitHandler, ICancelHandler
{
    public enum RebindDeviceMode
    {
        KeyboardMouse = 0,
        GamepadOnly = 1
    }

    public enum GamepadControlFilter
    {
        All = 0,
        NoSticks = 1,
        LeftStickOnly = 2,
        RightStickOnly = 3
    }

    [Header("Input binding target")]
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private string actionMap = "Player";
    [SerializeField] private string actionName = "Jump";

    [Tooltip("Optional: binding GUID (from .inputactions). If empty, first non-composite binding is used.")]
    [SerializeField] private string bindingId;

    [Header("Device")]
    [SerializeField] private RebindDeviceMode rebindDeviceMode = RebindDeviceMode.KeyboardMouse;
    [SerializeField] private GamepadControlFilter gamepadControlFilter = GamepadControlFilter.NoSticks;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("Optional: usually the Button's own Image; updated when a GamepadIconLibrary is assigned.")]
    [SerializeField] private Image bindingIconImage;
    [SerializeField] private GamepadIconLibrary gamepadIconLibrary;
    [SerializeField] private string armedPrefix = "> ";
    [SerializeField] private string listeningText = "Press any key...";
    [SerializeField] private string gamepadListeningText = "Press a gamepad control...";
    [SerializeField] private bool requireSubmitAfterClick = true;

    private Button _button;
    private bool _armed;
    private InputActionRebindingExtensions.RebindingOperation _rebindOp;
    private Coroutine _delayedGamepadRebindStart;
    private InputAction _rebindTargetAction;
    private bool _restoreSendNavigationEvents;
    private bool _navigationEventsCached;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (bindingIconImage == null)
            bindingIconImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (actions != null)
            InputRebindPersistence.LoadAndApply(actions);
        RefreshLabel();
    }

    private void OnDisable()
    {
        CancelRebind();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Arm();
        if (!requireSubmitAfterClick)
            StartRebind();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (_rebindOp != null) return;
        if (!_armed)
            Arm();

        // Gamepad Submit (A): listening begins after that button is released so it can still be chosen as the new binding.
        StartRebind();
    }

    public void OnCancel(BaseEventData eventData)
    {
        // UI "Cancel" is usually gamepad B; during rebind that press must become the new binding, not ICancelHandler.
        if (_rebindOp != null)
            return;

        Disarm();
        CancelRebind();
        RefreshLabel();
    }

    private void Arm()
    {
        _armed = true;
        RefreshLabel();
    }

    private void Disarm()
    {
        _armed = false;
    }

    private void StartRebind()
    {
        if (actions == null) return;

        var map = FindActionMapCaseInsensitive(actions, actionMap);
        var action = FindActionCaseInsensitive(map, actionName);
        if (action == null) return;

        int bindingIndex = FindBindingIndex(action);
        if (bindingIndex < 0) return;

        // Disable while rebinding so old binding doesn't also fire.
        _rebindTargetAction = action;
        action.Disable();

        _rebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.05f);

        if (rebindDeviceMode == RebindDeviceMode.GamepadOnly)
            ConfigureGamepadRebind();
        else
            ConfigureKeyboardMouseRebind();

        SetLabel(rebindDeviceMode == RebindDeviceMode.GamepadOnly ? gamepadListeningText : listeningText);

        _rebindOp.OnComplete(op =>
        {
            op.Dispose();
            _rebindOp = null;
            if (_rebindTargetAction != null)
            {
                _rebindTargetAction.Enable();
                _rebindTargetAction = null;
            }

            Disarm();

            // Persist overrides (paths are English/control-path based).
            InputRebindPersistence.Save(actions);
            InputBindingRuntimeSync.ApplySavedBindingsToAllRegistered();
            PopRebindUiIsolation();
            RefreshLabel();
            RefreshBindingIcon();
        });

        _rebindOp.OnCancel(op =>
        {
            op.Dispose();
            _rebindOp = null;
            if (_rebindTargetAction != null)
            {
                _rebindTargetAction.Enable();
                _rebindTargetAction = null;
            }

            Disarm();
            PopRebindUiIsolation();
            RefreshLabel();
            RefreshBindingIcon();
        });

        if (rebindDeviceMode == RebindDeviceMode.GamepadOnly)
        {
            StopDelayedGamepadRebindIfAny();
            _delayedGamepadRebindStart = StartCoroutine(DelayedStartGamepadRebind());
        }
        else
        {
            PushRebindUiIsolation();
            _rebindOp.Start();
        }
    }

    private void ConfigureKeyboardMouseRebind()
    {
        _rebindOp
            .WithControlsExcluding("<Keyboard>/enter")
            .WithControlsExcluding("<Keyboard>/numpadEnter")
            .WithControlsHavingToMatchPath("<Keyboard>");

        _rebindOp.WithControlsHavingToMatchPath("<Mouse>/leftButton")
            .WithControlsHavingToMatchPath("<Mouse>/rightButton")
            .WithControlsHavingToMatchPath("<Mouse>/middleButton")
            .WithControlsHavingToMatchPath("<Mouse>/forwardButton")
            .WithControlsHavingToMatchPath("<Mouse>/backButton");

        _rebindOp.WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll");
    }

    private void ConfigureGamepadRebind()
    {
        // Do not cancel with gamepad B — users must be able to bind buttonEast; use keyboard Escape to cancel.
        // Gamepad A (Submit) is allowed as a binding; listening starts after buttonSouth is released (see DelayedStartGamepadRebind).
        _rebindOp.WithControlsExcluding("<Keyboard>/enter")
            .WithControlsExcluding("<Keyboard>/numpadEnter");

        switch (gamepadControlFilter)
        {
            case GamepadControlFilter.LeftStickOnly:
                _rebindOp.WithControlsHavingToMatchPath("<Gamepad>/leftStick");
                break;
            case GamepadControlFilter.RightStickOnly:
                _rebindOp.WithControlsHavingToMatchPath("<Gamepad>/rightStick");
                break;
            default:
                _rebindOp.WithControlsHavingToMatchPath("<Gamepad>");
                if (gamepadControlFilter == GamepadControlFilter.NoSticks)
                {
                    _rebindOp.WithControlsExcluding("<Gamepad>/leftStick")
                        .WithControlsExcluding("<Gamepad>/rightStick");
                }
                break;
        }
    }

    private IEnumerator DelayedStartGamepadRebind()
    {
        PushRebindUiIsolation();
        yield return null;

        const float timeoutSec = 3f;
        float end = Time.unscaledTime + timeoutSec;
        while (Time.unscaledTime < end)
        {
            bool anySouthHeld = false;
            foreach (var gp in Gamepad.all)
            {
                if (gp == null) continue;
                if (gp.buttonSouth.isPressed)
                {
                    anySouthHeld = true;
                    break;
                }
            }

            if (!anySouthHeld)
                break;
            yield return null;
        }

        _delayedGamepadRebindStart = null;

        if (_rebindOp == null)
            yield break;

        _rebindOp.Start();
    }

    private void StopDelayedGamepadRebindIfAny()
    {
        if (_delayedGamepadRebindStart == null) return;
        StopCoroutine(_delayedGamepadRebindStart);
        _delayedGamepadRebindStart = null;

        if (_rebindOp != null)
        {
            _rebindOp.Dispose();
            _rebindOp = null;
        }

        if (_rebindTargetAction != null)
        {
            _rebindTargetAction.Enable();
            _rebindTargetAction = null;
        }

        PopRebindUiIsolation();
        Disarm();
        RefreshLabel();
    }

    private void CancelRebind()
    {
        StopDelayedGamepadRebindIfAny();

        if (_rebindOp == null) return;
        // OnCancel callback disposes the op and calls PopRebindUiIsolation.
        _rebindOp.Cancel();
    }

    private void PushRebindUiIsolation()
    {
        InputRebindSession.Begin();
        var es = EventSystem.current;
        if (es != null)
        {
            _restoreSendNavigationEvents = es.sendNavigationEvents;
            _navigationEventsCached = true;
            es.sendNavigationEvents = false;
        }
    }

    private void PopRebindUiIsolation()
    {
        if (!InputRebindSession.IsActive)
            return;

        InputRebindSession.End();

        if (_navigationEventsCached && EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = _restoreSendNavigationEvents;
        _navigationEventsCached = false;
    }

    private int FindBindingIndex(InputAction action)
    {
        if (action == null) return -1;

        // Prefer explicit binding GUID.
        if (!string.IsNullOrWhiteSpace(bindingId) && Guid.TryParse(bindingId, out var guid))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id == guid)
                    return i;
            }
        }

        if (rebindDeviceMode == RebindDeviceMode.GamepadOnly && string.IsNullOrWhiteSpace(bindingId))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (!string.IsNullOrEmpty(b.effectivePath) && b.effectivePath.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
        }

        // Fallback: first non-composite binding.
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            return i;
        }

        return -1;
    }

    private void RefreshLabel()
    {
        if (label == null) return;
        if (actions == null)
        {
            SetLabel(_armed ? armedPrefix + "-" : "-");
            return;
        }

        var map = FindActionMapCaseInsensitive(actions, actionMap);
        var action = FindActionCaseInsensitive(map, actionName);
        if (action == null)
        {
            SetLabel(_armed ? armedPrefix + "?" : "?");
            return;
        }

        int bindingIndex = FindBindingIndex(action);
        if (bindingIndex < 0)
        {
            SetLabel(_armed ? armedPrefix + "?" : "?");
            return;
        }

        string display = action.GetBindingDisplayString(bindingIndex, out _, out _);
        if (string.IsNullOrWhiteSpace(display))
            display = action.bindings[bindingIndex].effectivePath;

        SetLabel(_armed ? armedPrefix + display : display);
        RefreshBindingIcon();
    }

    /// <summary>
    /// Call this after changing binding overrides (e.g. from ResetAll) to update the UI immediately.
    /// </summary>
    public void ForceRefresh()
    {
        RefreshLabel();
    }

    public InputActionAsset GetActionsAsset() => actions;

    private void RefreshBindingIcon()
    {
        if (bindingIconImage == null || gamepadIconLibrary == null)
            return;
        if (rebindDeviceMode != RebindDeviceMode.GamepadOnly)
            return;
        if (actions == null)
            return;

        var map = FindActionMapCaseInsensitive(actions, actionMap);
        var action = FindActionCaseInsensitive(map, actionName);
        if (action == null)
            return;

        int bindingIndex = FindBindingIndex(action);
        if (bindingIndex < 0)
            return;

        string path = action.bindings[bindingIndex].effectivePath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var sprite = gamepadIconLibrary.ResolveSprite(path);
        if (sprite != null)
            bindingIconImage.sprite = sprite;
    }

    private void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    private static InputActionMap FindActionMapCaseInsensitive(InputActionAsset asset, string desiredName)
    {
        if (asset == null) return null;
        if (string.IsNullOrWhiteSpace(desiredName)) return null;

        // Fast path: exact match.
        var exact = asset.FindActionMap(desiredName, false);
        if (exact != null) return exact;

        foreach (var map in asset.actionMaps)
        {
            if (map == null) continue;
            if (string.Equals(map.name, desiredName, StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return null;
    }

    private static InputAction FindActionCaseInsensitive(InputActionMap map, string desiredName)
    {
        if (map == null) return null;
        if (string.IsNullOrWhiteSpace(desiredName)) return null;

        // Fast path: exact match.
        var exact = map.FindAction(desiredName, false);
        if (exact != null) return exact;

        foreach (var action in map.actions)
        {
            if (action == null) continue;
            if (string.Equals(action.name, desiredName, StringComparison.OrdinalIgnoreCase))
                return action;
        }

        return null;
    }
}

