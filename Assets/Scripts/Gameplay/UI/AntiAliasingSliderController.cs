using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// MSAA / anti-aliasing slider (4 discrete steps) for URP and built-in fallback.
/// URP: <see cref="UniversalRenderPipelineAsset.msaaSampleCount"/> (1 = off, 2, 4, 8).
/// Built-in: <see cref="QualitySettings.antiAliasing"/> (0 = off, 2, 4, 8).
/// Mouse: left/right half steps ±1; keyboard/gamepad unchanged.
/// </summary>
public class AntiAliasingSliderController : MonoBehaviour
{
    private const string PlayerPrefsKey = "Settings_AntiAliasingIndex";

    [Header("UI")]
    [SerializeField] private Slider aaSlider;
    [SerializeField] private TextMeshProUGUI aaText;

    [Header("Mouse click behavior")]
    [SerializeField] private bool stepByHalfClick = true;
    [SerializeField] private bool hideHandle = true;

    [Header("Behavior")]
    [SerializeField] private bool applyOnInitialize = true;
    [SerializeField] private bool saveToPlayerPrefs = true;

    private int _lastUiIndex;
    private bool _mouseStepping;
    private PointerCatcher _pointerCatcher;

    private void Awake()
    {
        if (aaSlider == null)
            aaSlider = GetComponent<Slider>();
        if (aaText == null)
            aaText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (aaSlider == null)
        {
            Debug.LogWarning($"{nameof(AntiAliasingSliderController)}: aaSlider is not assigned.");
            return;
        }

        aaSlider.wholeNumbers = true;
        aaSlider.minValue = 0;
        aaSlider.maxValue = 3;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentUiIndexFromSettings();

        _lastUiIndex = uiIndex;

        aaSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        aaSlider.onValueChanged.AddListener(OnSliderValueChanged);

        aaSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);

        if (applyOnInitialize)
            ApplyAntiAliasing(uiIndex);

        if (hideHandle)
            HideHandle();

        if (stepByHalfClick)
            SetupPointerCatcher();
    }

    /// <summary>Syncs slider + label from PlayerPrefs (engine already applied).</summary>
    public void RefreshFromPlayerPrefs()
    {
        if (aaSlider == null)
            aaSlider = GetComponent<Slider>();
        if (aaSlider == null)
            return;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentUiIndexFromSettings();

        _lastUiIndex = uiIndex;
        aaSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);
    }

    private void OnDisable()
    {
        if (aaSlider != null)
            aaSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float v)
    {
        int uiIndex = Mathf.RoundToInt(v);
        uiIndex = Mathf.Clamp(uiIndex, 0, 3);

        if (stepByHalfClick)
        {
            var mouse = Mouse.current;
            bool leftMouseDown = mouse != null && mouse.leftButton.isPressed;
            if (_mouseStepping || leftMouseDown)
            {
                if (uiIndex != _lastUiIndex)
                    aaSlider.SetValueWithoutNotify(_lastUiIndex);
                return;
            }
        }

        if (uiIndex == _lastUiIndex)
            return;

        _lastUiIndex = uiIndex;
        UpdateText(uiIndex);
        ApplyAntiAliasing(uiIndex);
    }

    private void ApplyAntiAliasing(int uiIndex)
    {
        int urpSamples = UiIndexToUrpMsaaSamples(uiIndex);
        var urp = GetActiveUrpAsset();
        if (urp != null)
            urp.msaaSampleCount = urpSamples;
        else
            QualitySettings.antiAliasing = UiIndexToBuiltInMsaa(uiIndex);

        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, uiIndex);
            PlayerPrefs.Save();
        }
    }

    /// <summary>URP MsaaQuality: Disabled = 1, then 2, 4, 8.</summary>
    private static int UiIndexToUrpMsaaSamples(int uiIndex)
    {
        return uiIndex switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            3 => 8,
            _ => 4
        };
    }

    /// <summary>Built-in: 0 = off, else 2 / 4 / 8.</summary>
    private static int UiIndexToBuiltInMsaa(int uiIndex) => UiIndexToUrpMsaaSamples(uiIndex) == 1 ? 0 : UiIndexToUrpMsaaSamples(uiIndex);

    private static UniversalRenderPipelineAsset GetActiveUrpAsset()
    {
        if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset q)
            return q;
        return GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
    }

    private int GetCurrentUiIndexFromSettings()
    {
        var urp = GetActiveUrpAsset();
        if (urp != null)
            return SamplesToUiIndex(urp.msaaSampleCount);

        return SamplesToUiIndex(QualitySettings.antiAliasing == 0 ? 1 : QualitySettings.antiAliasing);
    }

    private static int SamplesToUiIndex(int samples)
    {
        switch (samples)
        {
            case 1: return 0;
            case 2: return 1;
            case 4: return 2;
            case 8: return 3;
            default:
                if (samples < 2) return 0;
                if (samples < 4) return 1;
                if (samples < 8) return 2;
                return 3;
        }
    }

    private void UpdateText(int uiIndex)
    {
        if (aaText == null) return;
        aaText.text = uiIndex switch
        {
            0 => "Off",
            1 => "2x MSAA",
            2 => "4x MSAA",
            3 => "8x MSAA",
            _ => "4x MSAA"
        };
    }

    private void HideHandle()
    {
        if (aaSlider == null) return;
        if (aaSlider.handleRect == null) return;
        aaSlider.handleRect.gameObject.SetActive(false);
    }

    private void SetupPointerCatcher()
    {
        if (aaSlider == null) return;

        _pointerCatcher = aaSlider.GetComponent<PointerCatcher>();
        if (_pointerCatcher == null)
            _pointerCatcher = aaSlider.gameObject.AddComponent<PointerCatcher>();

        _pointerCatcher.Init(this);
    }

    private void HandleHalfClick(PointerEventData eventData)
    {
        if (aaSlider == null) return;

        var sliderRect = aaSlider.GetComponent<RectTransform>();
        if (sliderRect == null) return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sliderRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint))
            return;

        float t = Mathf.InverseLerp(sliderRect.rect.xMin, sliderRect.rect.xMax, localPoint.x);
        bool clickedLeftHalf = t < 0.5f;

        int currentIndex = _lastUiIndex;
        int nextIndex = clickedLeftHalf ? currentIndex - 1 : currentIndex + 1;
        nextIndex = Mathf.Clamp(nextIndex, 0, 3);
        if (nextIndex == _lastUiIndex)
            return;

        aaSlider.SetValueWithoutNotify(nextIndex);
        _lastUiIndex = nextIndex;
        UpdateText(nextIndex);
        ApplyAntiAliasing(nextIndex);
    }

    private void BeginMouseStep() => _mouseStepping = true;
    private void EndMouseStep() => _mouseStepping = false;

    private sealed class PointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private AntiAliasingSliderController _controller;
        public void Init(AntiAliasingSliderController controller) => _controller = controller;

        public void OnPointerDown(PointerEventData eventData)
        {
            _controller?.BeginMouseStep();
            _controller?.HandleHalfClick(eventData);
            eventData.Use();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _controller?.EndMouseStep();
            eventData.Use();
        }
    }
}
