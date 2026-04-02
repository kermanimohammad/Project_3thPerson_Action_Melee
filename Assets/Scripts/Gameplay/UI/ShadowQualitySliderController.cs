using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Shadow resolution slider (discrete) with 4 states: Low / Medium / High / Ultra.
/// Maps to <see cref="QualitySettings.shadowResolution"/>:
/// Low, Medium, High, VeryHigh (enum values 0–3).
/// Mouse: left/right half of track steps ±1; keyboard/gamepad navigation unchanged.
/// </summary>
public class ShadowQualitySliderController : MonoBehaviour
{
    private const string PlayerPrefsKey = "Settings_ShadowQualityIndex";

    [Header("UI")]
    [SerializeField] private Slider shadowSlider;
    [SerializeField] private TextMeshProUGUI shadowText;

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
        if (shadowSlider == null)
            shadowSlider = GetComponent<Slider>();
        if (shadowText == null)
            shadowText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (shadowSlider == null)
        {
            Debug.LogWarning($"{nameof(ShadowQualitySliderController)}: shadowSlider is not assigned.");
            return;
        }

        shadowSlider.wholeNumbers = true;
        shadowSlider.minValue = 0;
        shadowSlider.maxValue = 3;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentUiIndexFromQualitySettings();

        _lastUiIndex = uiIndex;

        shadowSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        shadowSlider.onValueChanged.AddListener(OnSliderValueChanged);

        shadowSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);

        if (applyOnInitialize)
            ApplyShadowQuality(uiIndex);

        if (hideHandle)
            HideHandle();

        if (stepByHalfClick)
            SetupPointerCatcher();
    }

    /// <summary>Syncs slider + label from PlayerPrefs (engine already applied).</summary>
    public void RefreshFromPlayerPrefs()
    {
        if (shadowSlider == null)
            shadowSlider = GetComponent<Slider>();
        if (shadowSlider == null)
            return;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentUiIndexFromQualitySettings();

        _lastUiIndex = uiIndex;
        shadowSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);
    }

    private void OnDisable()
    {
        if (shadowSlider != null)
            shadowSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
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
                    shadowSlider.SetValueWithoutNotify(_lastUiIndex);
                return;
            }
        }

        if (uiIndex == _lastUiIndex)
            return;

        _lastUiIndex = uiIndex;
        UpdateText(uiIndex);
        ApplyShadowQuality(uiIndex);
    }

    private void ApplyShadowQuality(int uiIndex)
    {
        QualitySettings.shadowResolution = UiIndexToShadowResolution(uiIndex);

        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, uiIndex);
            PlayerPrefs.Save();
        }
    }

    private static ShadowResolution UiIndexToShadowResolution(int uiIndex)
    {
        uiIndex = Mathf.Clamp(uiIndex, 0, 3);
        return (ShadowResolution)uiIndex;
    }

    private int GetCurrentUiIndexFromQualitySettings()
    {
        int v = (int)QualitySettings.shadowResolution;
        if (v < 0 || v > 3)
            return 2;
        return v;
    }

    private void UpdateText(int uiIndex)
    {
        if (shadowText == null) return;
        shadowText.text = uiIndex switch
        {
            0 => "Low",
            1 => "Medium",
            2 => "High",
            3 => "Ultra",
            _ => "High"
        };
    }

    private void HideHandle()
    {
        if (shadowSlider == null) return;
        if (shadowSlider.handleRect == null) return;
        shadowSlider.handleRect.gameObject.SetActive(false);
    }

    private void SetupPointerCatcher()
    {
        if (shadowSlider == null) return;

        _pointerCatcher = shadowSlider.GetComponent<PointerCatcher>();
        if (_pointerCatcher == null)
            _pointerCatcher = shadowSlider.gameObject.AddComponent<PointerCatcher>();

        _pointerCatcher.Init(this);
    }

    private void HandleHalfClick(PointerEventData eventData)
    {
        if (shadowSlider == null) return;

        var sliderRect = shadowSlider.GetComponent<RectTransform>();
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

        shadowSlider.SetValueWithoutNotify(nextIndex);
        _lastUiIndex = nextIndex;
        UpdateText(nextIndex);
        ApplyShadowQuality(nextIndex);
    }

    private void BeginMouseStep() => _mouseStepping = true;
    private void EndMouseStep() => _mouseStepping = false;

    private sealed class PointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private ShadowQualitySliderController _controller;
        public void Init(ShadowQualitySliderController controller) => _controller = controller;

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
