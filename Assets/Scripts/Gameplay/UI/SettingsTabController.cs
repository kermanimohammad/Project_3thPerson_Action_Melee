using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Settings screen: three tab buttons toggle exactly one content panel (Graphics / Audio / Controls).
/// Gamepad LT / RT cycle tabs (backward / forward).
/// </summary>
public class SettingsTabController : MonoBehaviour
{
    [Header("Panels (one active at a time)")]
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;

    [Header("Tab buttons (for focus + LT/RT)")]
    [Tooltip("GraphicTab root (has Button).")]
    [SerializeField] private GameObject graphicsTabButton;
    [Tooltip("AudioTab root (has Button).")]
    [SerializeField] private GameObject audioTabButton;
    [Tooltip("ControlsTab root (has Button).")]
    [SerializeField] private GameObject controlsTabButton;

    [Header("Navigation default")]
    [Tooltip("When the Settings root is shown, start on the Graphics tab.")]
    [SerializeField] private bool selectGraphicsWhenEnabled = true;

    [Header("Gamepad triggers")]
    [SerializeField] [Range(0.2f, 0.95f)] private float triggerPressThreshold = 0.55f;

    private int _currentTabIndex;
    private float _prevRtCombined;
    private float _prevLtCombined;
    private bool _prevTabLeftPressed;
    private bool _prevTabRightPressed;

    private void OnEnable()
    {
        if (!selectGraphicsWhenEnabled) return;

        ShowTab(0);

        if (graphicsTabButton != null && graphicsTabButton.activeInHierarchy)
        {
            StopAllCoroutines();
            StartCoroutine(ApplyGraphicTabSelectedNextFrame());
        }
    }

    private void OnDisable()
    {
        _prevRtCombined = 0f;
        _prevLtCombined = 0f;
        _prevTabLeftPressed = false;
        _prevTabRightPressed = false;
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;
        if (InputRebindSession.IsActive)
            return;

        float rt = 0f;
        float lt = 0f;
        foreach (var gp in Gamepad.all)
        {
            if (gp == null) continue;
            rt = Mathf.Max(rt, gp.rightTrigger.ReadValue());
            lt = Mathf.Max(lt, gp.leftTrigger.ReadValue());
        }

        // LT/RT must only cycle tabs when a tab *header* is focused — same rule as D-Pad/stick left-right below.
        // Otherwise triggers fire while rebinding controls inside the Controls panel.
        bool tabSelected = IsAnyTabSelected();
        float t = triggerPressThreshold;
        if (tabSelected)
        {
            bool rtEdge = rt >= t && _prevRtCombined < t;
            bool ltEdge = lt >= t && _prevLtCombined < t;
            if (rtEdge)
                CycleTab(+1);
            else if (ltEdge)
                CycleTab(-1);
        }

        _prevRtCombined = rt;
        _prevLtCombined = lt;

        bool leftPressed = tabSelected && IsLeftNavPressed();
        bool rightPressed = tabSelected && IsRightNavPressed();
        bool leftEdge = leftPressed && !_prevTabLeftPressed;
        bool rightEdge = rightPressed && !_prevTabRightPressed;
        _prevTabLeftPressed = leftPressed;
        _prevTabRightPressed = rightPressed;

        if (rightEdge)
            CycleTab(+1);
        else if (leftEdge)
            CycleTab(-1);
    }

    /// <summary>RT: next tab (Graphic→Audio→Controls→Graphic). LT: previous.</summary>
    private void CycleTab(int delta)
    {
        int next = (_currentTabIndex + delta) % 3;
        if (next < 0) next += 3;
        UIButtonHoverSfx.PlayClickSfx();
        ShowTabAndFocus(next);
    }

    private void ShowTabAndFocus(int index)
    {
        ShowTab(index);
        FocusTabButton(GetTabButton(index));
    }

    private GameObject GetTabButton(int index)
    {
        return index switch
        {
            0 => graphicsTabButton,
            1 => audioTabButton,
            2 => controlsTabButton,
            _ => null
        };
    }

    /// <summary>
    /// Defer selection one frame so Button/Selectable applies the Selected sprite/state reliably after Settings activates.
    /// </summary>
    private IEnumerator ApplyGraphicTabSelectedNextFrame()
    {
        yield return null;

        if (graphicsTabButton == null || !graphicsTabButton.activeInHierarchy)
            yield break;

        FocusTabButton(graphicsTabButton);
    }

    private static void FocusTabButton(GameObject tabRoot)
    {
        if (tabRoot == null || !tabRoot.activeInHierarchy) return;

        var es = EventSystem.current;
        if (es == null) return;

        Canvas.ForceUpdateCanvases();
        es.SetSelectedGameObject(null);

        UIButtonHoverSfx.PrepareProgrammaticSelect(tabRoot);

        var btn = tabRoot.GetComponent<Button>();
        if (btn != null && btn.IsActive() && btn.IsInteractable())
            btn.Select();
        else
            es.SetSelectedGameObject(tabRoot);
    }

    /// <summary>Wire GraphicTab Button OnClick here.</summary>
    public void SelectGraphicsTab() => ShowTabAndFocus(0);

    /// <summary>Wire AudioTab Button OnClick here.</summary>
    public void SelectAudioTab() => ShowTabAndFocus(1);

    /// <summary>Wire ControlsTab Button OnClick here.</summary>
    public void SelectControlsTab() => ShowTabAndFocus(2);

    private void ShowTab(int index)
    {
        index = Mathf.Clamp(index, 0, 2);
        _currentTabIndex = index;
        if (graphicsPanel != null) graphicsPanel.SetActive(index == 0);
        if (audioPanel != null) audioPanel.SetActive(index == 1);
        if (controlsPanel != null) controlsPanel.SetActive(index == 2);

        PauseMenuController.RefreshUiAnimatorsAfterSettingsTabChange();
    }

    private bool IsAnyTabSelected()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var selected = es.currentSelectedGameObject;
        if (selected == null) return false;

        var selectable = selected.GetComponentInParent<Selectable>();
        if (selectable == null) return false;
        var root = selectable.gameObject;

        return root == graphicsTabButton || root == audioTabButton || root == controlsTabButton;
    }

    private static bool IsLeftNavPressed()
    {
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame)
                return true;
        }

        foreach (var gp in Gamepad.all)
        {
            if (gp == null) continue;
            if (gp.dpad.left.wasPressedThisFrame)
                return true;

            // Allow left-stick horizontal tap to cycle tabs while tab is selected.
            if (gp.leftStick.left.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool IsRightNavPressed()
    {
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame)
                return true;
        }

        foreach (var gp in Gamepad.all)
        {
            if (gp == null) continue;
            if (gp.dpad.right.wasPressedThisFrame)
                return true;

            if (gp.leftStick.right.wasPressedThisFrame)
                return true;
        }

        return false;
    }
}
