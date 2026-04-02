using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// VSync slider with two discrete states: Off / On.
/// - Slider values: 0 = Off, 1 = On
/// - Keyboard/gamepad navigation still works
/// - Mouse click on left/right half steps by 1 (like Resolution slider)
/// </summary>
public class VSyncSliderController : MonoBehaviour
{
    private const string PlayerPrefsKey = "Settings_VSync";

    [Header("UI")]
    [SerializeField] private Slider vsyncSlider;
    [SerializeField] private TextMeshProUGUI vsyncText;

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
        if (vsyncSlider == null)
            vsyncSlider = GetComponent<Slider>();
        if (vsyncText == null)
            vsyncText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (vsyncSlider == null)
        {
            Debug.LogWarning($"{nameof(VSyncSliderController)}: vsyncSlider is not assigned.");
            return;
        }

        vsyncSlider.wholeNumbers = true;
        vsyncSlider.minValue = 0;
        vsyncSlider.maxValue = 1;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored == 0 || stored == 1 ? stored : GetCurrentUiIndex();

        _lastUiIndex = uiIndex;

        vsyncSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        vsyncSlider.onValueChanged.AddListener(OnSliderValueChanged);

        vsyncSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);

        if (applyOnInitialize)
            ApplyVSync(uiIndex);

        if (hideHandle)
            HideHandle();

        if (stepByHalfClick)
            SetupPointerCatcher();
    }

    /// <summary>Syncs slider + label from PlayerPrefs (engine already applied).</summary>
    public void RefreshFromPlayerPrefs()
    {
        if (vsyncSlider == null)
            vsyncSlider = GetComponent<Slider>();
        if (vsyncSlider == null)
            return;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored == 0 || stored == 1 ? stored : GetCurrentUiIndex();

        _lastUiIndex = uiIndex;
        vsyncSlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);
    }

    private void OnDisable()
    {
        if (vsyncSlider != null)
            vsyncSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float v)
    {
        int uiIndex = Mathf.RoundToInt(v);
        uiIndex = Mathf.Clamp(uiIndex, 0, 1);

        if (stepByHalfClick)
        {
            var mouse = Mouse.current;
            bool leftMouseDown = mouse != null && mouse.leftButton.isPressed;
            if (_mouseStepping || leftMouseDown)
            {
                if (uiIndex != _lastUiIndex)
                    vsyncSlider.SetValueWithoutNotify(_lastUiIndex);
                return;
            }
        }

        if (uiIndex == _lastUiIndex)
            return;

        _lastUiIndex = uiIndex;
        UpdateText(uiIndex);
        ApplyVSync(uiIndex);
    }

    private void ApplyVSync(int uiIndex)
    {
        // 0 = don't sync, 1 = sync every v-blank
        QualitySettings.vSyncCount = uiIndex == 1 ? 1 : 0;

        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, uiIndex);
            PlayerPrefs.Save();
        }
    }

    private int GetCurrentUiIndex()
        => QualitySettings.vSyncCount > 0 ? 1 : 0;

    private void UpdateText(int uiIndex)
    {
        if (vsyncText == null) return;
        vsyncText.text = uiIndex == 1 ? "On" : "Off";
    }

    private void HideHandle()
    {
        if (vsyncSlider == null) return;
        if (vsyncSlider.handleRect == null) return;
        vsyncSlider.handleRect.gameObject.SetActive(false);
    }

    private void SetupPointerCatcher()
    {
        if (vsyncSlider == null) return;

        _pointerCatcher = vsyncSlider.GetComponent<PointerCatcher>();
        if (_pointerCatcher == null)
            _pointerCatcher = vsyncSlider.gameObject.AddComponent<PointerCatcher>();

        _pointerCatcher.Init(this);
    }

    private void HandleHalfClick(PointerEventData eventData)
    {
        if (vsyncSlider == null) return;

        var sliderRect = vsyncSlider.GetComponent<RectTransform>();
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
        nextIndex = Mathf.Clamp(nextIndex, 0, 1);
        if (nextIndex == _lastUiIndex)
            return;

        vsyncSlider.SetValueWithoutNotify(nextIndex);
        _lastUiIndex = nextIndex;
        UpdateText(nextIndex);
        ApplyVSync(nextIndex);
    }

    private void BeginMouseStep() => _mouseStepping = true;
    private void EndMouseStep() => _mouseStepping = false;

    private sealed class PointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private VSyncSliderController _controller;
        public void Init(VSyncSliderController controller) => _controller = controller;

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

