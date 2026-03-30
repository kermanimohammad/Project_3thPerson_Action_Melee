using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Texture quality slider (discrete) with 4 states: Low / Medium / High / Ultra.
/// Uses QualitySettings.masterTextureLimit:
/// - 0 = Full res (best)
/// - 1 = Half
/// - 2 = Quarter
/// - 3 = Eighth (worst)
///
/// UI mapping (user-facing):
/// 0 = Low    -> masterTextureLimit 3
/// 1 = Medium -> masterTextureLimit 2
/// 2 = High   -> masterTextureLimit 1
/// 3 = Ultra  -> masterTextureLimit 0
///
/// Mouse clicks on left/right half step by 1 (like Resolution slider).
/// Keyboard/gamepad navigation still works.
/// </summary>
public class TextureQualitySliderController : MonoBehaviour
{
    private const string PlayerPrefsKey = "Settings_TextureQualityIndex";

    [Header("UI")]
    [SerializeField] private Slider textureSlider;
    [SerializeField] private TextMeshProUGUI textureText;

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
        if (textureSlider == null)
            textureSlider = GetComponent<Slider>();
        if (textureText == null)
            textureText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (textureSlider == null)
        {
            Debug.LogWarning($"{nameof(TextureQualitySliderController)}: textureSlider is not assigned.");
            return;
        }

        textureSlider.wholeNumbers = true;
        textureSlider.minValue = 0;
        textureSlider.maxValue = 3;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentUiIndexFromQualitySettings();

        _lastUiIndex = uiIndex;

        textureSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        textureSlider.onValueChanged.AddListener(OnSliderValueChanged);

        textureSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);

        if (applyOnInitialize)
            ApplyTextureQuality(uiIndex);

        if (hideHandle)
            HideHandle();

        if (stepByHalfClick)
            SetupPointerCatcher();
    }

    /// <summary>Syncs slider + label from PlayerPrefs (engine already applied).</summary>
    public void RefreshFromPlayerPrefs()
    {
        if (textureSlider == null)
            textureSlider = GetComponent<Slider>();
        if (textureSlider == null)
            return;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentUiIndexFromQualitySettings();

        _lastUiIndex = uiIndex;
        textureSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);
    }

    private void OnDisable()
    {
        if (textureSlider != null)
            textureSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
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
                    textureSlider.SetValueWithoutNotify(_lastUiIndex);
                return;
            }
        }

        if (uiIndex == _lastUiIndex)
            return;

        _lastUiIndex = uiIndex;
        UpdateText(uiIndex);
        ApplyTextureQuality(uiIndex);
    }

    private void ApplyTextureQuality(int uiIndex)
    {
        int masterLimit = UiIndexToMasterTextureLimit(uiIndex);
        QualitySettings.globalTextureMipmapLimit = masterLimit;

        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, uiIndex);
            PlayerPrefs.Save();
        }
    }

    private int GetCurrentUiIndexFromQualitySettings()
    {
        int masterLimit = Mathf.Clamp(QualitySettings.globalTextureMipmapLimit, 0, 3);
        // Invert mapping.
        return masterLimit switch
        {
            3 => 0, // Low
            2 => 1, // Medium
            1 => 2, // High
            0 => 3, // Ultra
            _ => 2
        };
    }

    private static int UiIndexToMasterTextureLimit(int uiIndex)
    {
        // 0 Low -> 3, 1 Med -> 2, 2 High -> 1, 3 Ultra -> 0
        return Mathf.Clamp(3 - uiIndex, 0, 3);
    }

    private void UpdateText(int uiIndex)
    {
        if (textureText == null) return;
        textureText.text = uiIndex switch
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
        if (textureSlider == null) return;
        if (textureSlider.handleRect == null) return;
        textureSlider.handleRect.gameObject.SetActive(false);
    }

    private void SetupPointerCatcher()
    {
        if (textureSlider == null) return;

        _pointerCatcher = textureSlider.GetComponent<PointerCatcher>();
        if (_pointerCatcher == null)
            _pointerCatcher = textureSlider.gameObject.AddComponent<PointerCatcher>();

        _pointerCatcher.Init(this);
    }

    private void HandleHalfClick(PointerEventData eventData)
    {
        if (textureSlider == null) return;

        var sliderRect = textureSlider.GetComponent<RectTransform>();
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

        textureSlider.SetValueWithoutNotify(nextIndex);
        _lastUiIndex = nextIndex;
        UpdateText(nextIndex);
        ApplyTextureQuality(nextIndex);
    }

    private void BeginMouseStep() => _mouseStepping = true;
    private void EndMouseStep() => _mouseStepping = false;

    private sealed class PointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private TextureQualitySliderController _controller;
        public void Init(TextureQualitySliderController controller) => _controller = controller;

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

