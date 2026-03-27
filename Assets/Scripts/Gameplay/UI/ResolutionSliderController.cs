using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Controls game resolution via a UI Slider.
/// - Reads OS resolution from Screen.currentResolution
/// - Builds a unique list of supported resolutions from Screen.resolutions (ignores refresh rate differences for uniqueness)
/// - Slider index maps to (width, height) in ascending order by area
/// - Updates the TMP text with "WIDTH x HEIGHT"
/// </summary>
public class ResolutionSliderController : MonoBehaviour
{
    [System.Serializable]
    private struct ResolutionOption
    {
        public int width;
        public int height;
        public int refreshRate;

        public int Area => width * height;
    }

    [Header("UI")]
    [SerializeField] private Slider resolutionSlider;
    [SerializeField] private TextMeshProUGUI resolutionText;

    [Header("Behavior")]
    [Tooltip("If true, SetResolution is applied immediately when the slider value is initialized.")]
    [SerializeField] private bool applyOnInitialize = false;

    [Tooltip("Keeps the current Screen.fullScreenMode. If you want a fixed mode, change this script.")]
    [SerializeField] private bool keepCurrentFullscreenMode = true;

    [Tooltip("When current resolution isn't found in Screen.resolutions list, fallback index.")]
    [SerializeField] private int fallbackIndex = 1;

    private readonly List<ResolutionOption> _options = new List<ResolutionOption>();
    private bool _suppressCallback;
    // When the slider value changes due to mouse click, we revert it and apply our custom ±1 step logic only.
    private bool _mouseStepping;
    private int _lastAppliedIndex;
    private PointerCatcher _pointerCatcher;

    private void Awake()
    {
        if (resolutionSlider == null)
            resolutionSlider = GetComponent<Slider>();

        if (resolutionText == null)
            resolutionText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (resolutionSlider == null || resolutionText == null)
        {
            Debug.LogWarning($"{nameof(ResolutionSliderController)}: Slider/TMP references missing.");
            return;
        }

        BuildOptions();
        InitFromCurrentResolution();

        resolutionSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        resolutionSlider.onValueChanged.AddListener(OnSliderValueChanged);

        HideHandle();
        SetupPointerCatcher();
    }

    private void OnDisable()
    {
        if (resolutionSlider == null) return;
        resolutionSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void HideHandle()
    {
        if (resolutionSlider == null) return;
        if (resolutionSlider.handleRect == null) return;
        resolutionSlider.handleRect.gameObject.SetActive(false);
    }

    private void SetupPointerCatcher()
    {
        if (resolutionSlider == null) return;

        // Attach catcher to the Slider GameObject itself.
        // This guarantees we receive OnPointerDown for both track-click and edge-click.
        _pointerCatcher = resolutionSlider.GetComponent<PointerCatcher>();
        if (_pointerCatcher == null)
            _pointerCatcher = resolutionSlider.gameObject.AddComponent<PointerCatcher>();

        _pointerCatcher.Init(this);
    }

    private void HandleTrackClick(PointerEventData eventData)
    {
        if (resolutionSlider == null || _options.Count == 0)
            return;

        var sliderRect = resolutionSlider.GetComponent<RectTransform>();
        if (sliderRect == null)
            return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sliderRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint))
            return;

        float t = Mathf.InverseLerp(sliderRect.rect.xMin, sliderRect.rect.xMax, localPoint.x);
        bool clickedLeftHalf = t < 0.5f;

        // IMPORTANT:
        // Slider.value is often already moved by Unity to the click position (min/max on extremes).
        // We must step from the last applied index, not from the UI's temporary value.
        int currentIndex = _lastAppliedIndex;
        int nextIndex = clickedLeftHalf ? currentIndex - 1 : currentIndex + 1;
        nextIndex = Mathf.Clamp(nextIndex, 0, _options.Count - 1);

        if (nextIndex == _lastAppliedIndex)
            return;

        // Update UI + resolution directly (no dependency on onValueChanged firing).
        _suppressCallback = true;
        resolutionSlider.SetValueWithoutNotify(nextIndex);
        _suppressCallback = false;

        _lastAppliedIndex = nextIndex;
        UpdateTextForIndex(nextIndex);
        ApplyResolutionAtIndex(nextIndex);
    }

    private void BeginMouseStep()
    {
        _mouseStepping = true;
    }

    private void EndMouseStep()
    {
        _mouseStepping = false;
    }

    private void BuildOptions()
    {
        _options.Clear();

        var current = Screen.currentResolution;
        resolutionText.text = FormatResolution(current.width, current.height);

        // Unique by (width, height), keep the highest refresh rate for each pair.
        var bestByPair = new Dictionary<(int w, int h), ResolutionOption>();
        var all = Screen.resolutions;
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r.width <= 0 || r.height <= 0)
                continue;

            var key = (r.width, r.height);
            if (!bestByPair.TryGetValue(key, out var existing))
            {
                bestByPair[key] = new ResolutionOption { width = r.width, height = r.height, refreshRate = r.refreshRate };
            }
            else
            {
                if (r.refreshRate > existing.refreshRate)
                    bestByPair[key] = new ResolutionOption { width = r.width, height = r.height, refreshRate = r.refreshRate };
            }
        }

        _options.AddRange(bestByPair.Values);
        _options.Sort((a, b) => a.Area.CompareTo(b.Area));
    }

    private void InitFromCurrentResolution()
    {
        if (_options.Count == 0)
            return;

        resolutionSlider.minValue = 0;
        resolutionSlider.maxValue = _options.Count - 1;
        resolutionSlider.wholeNumbers = true;

        var current = Screen.currentResolution;
        int currentIndex = _options.FindIndex(o => o.width == current.width && o.height == current.height);

        int defaultIndex;
        if (currentIndex >= 0)
            defaultIndex = currentIndex;
        else
            defaultIndex = Mathf.Clamp(fallbackIndex, 0, _options.Count - 1);

        _suppressCallback = true;
        resolutionSlider.value = defaultIndex;
        _suppressCallback = false;

        // Update TMP to the chosen index (usually matches OS resolution as requested).
        UpdateTextForIndex(defaultIndex);

        _lastAppliedIndex = defaultIndex;
        _mouseStepping = false;

        if (applyOnInitialize)
            ApplyResolutionAtIndex(defaultIndex);
    }

    private void OnSliderValueChanged(float value)
    {
        if (_suppressCallback) return;

        int idx = Mathf.RoundToInt(value);
        idx = Mathf.Clamp(idx, 0, _options.Count - 1);

        // Robust against event ordering:
        // if the user is currently holding left mouse button, Slider may temporarily jump
        // to min/max based on click position. We must ignore that and always revert
        // to the last applied index, letting HandleTrackClick do the ±1 step.
        bool leftMouseDown = false;
        var mouse = Mouse.current;
        if (mouse != null)
            leftMouseDown = mouse.leftButton.isPressed;

        if (leftMouseDown)
        {
            if (idx != _lastAppliedIndex)
            {
                _suppressCallback = true;
                resolutionSlider.SetValueWithoutNotify(_lastAppliedIndex);
                _suppressCallback = false;
            }
            return;
        }

        _lastAppliedIndex = idx;
        UpdateTextForIndex(idx);
        ApplyResolutionAtIndex(idx);
    }

    private void UpdateTextForIndex(int idx)
    {
        if (idx < 0 || idx >= _options.Count) return;
        var o = _options[idx];
        resolutionText.text = FormatResolution(o.width, o.height);
    }

    private void ApplyResolutionAtIndex(int idx)
    {
        if (idx < 0 || idx >= _options.Count) return;
        var o = _options[idx];

        var mode = keepCurrentFullscreenMode ? Screen.fullScreenMode : FullScreenMode.Windowed;

        // Unity versions differ; Screen.fullScreenMode overload is widely available in modern Unity.
        Screen.SetResolution(o.width, o.height, mode);
    }

    private static string FormatResolution(int width, int height)
        => $"{width} x {height}";

    private sealed class PointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private ResolutionSliderController _controller;

        public void Init(ResolutionSliderController controller) => _controller = controller;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Activate "mouse stepping" immediately, before any Slider onValueChanged can react.
            _controller?.BeginMouseStep();
            _controller?.HandleTrackClick(eventData);
            eventData.Use(); // try to stop Slider's own handling (drag/thumb)
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _controller?.EndMouseStep();
            eventData.Use();
        }
    }
}

