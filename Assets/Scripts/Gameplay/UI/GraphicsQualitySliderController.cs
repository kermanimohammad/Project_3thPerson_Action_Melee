using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Discrete Graphics Quality selector for MainMenu Settings.
/// Maps slider indexes:
/// 0 = Low, 1 = Medium, 2 = High, 3 = Ultra
/// Uses Unity QualitySettings + optional PlayerPrefs persistence.
/// </summary>
public class GraphicsQualitySliderController : MonoBehaviour
{
    private const string PlayerPrefsKey = "Settings_GraphicsQualityIndex";

    [Header("UI")]
    [SerializeField] private Slider qualitySlider;
    [SerializeField] private TextMeshProUGUI qualityText;

    [Header("Mouse click behavior")]
    [Tooltip("If true, clicking left/right half of the slider track steps by 1 (like Resolution slider).")]
    [SerializeField] private bool stepByHalfClick = true;

    [Tooltip("Optional: hide the slider handle (like Resolution slider).")]
    [SerializeField] private bool hideHandle = true;

    [Header("Quality names (must match Unity QualitySettings.names)")]
    [SerializeField] private string lowName = "Low";
    [SerializeField] private string mediumName = "Medium";
    [SerializeField] private string highName = "High";
    [SerializeField] private string ultraName = "Ultra";

    [Header("Behavior")]
    [SerializeField] private bool applyOnInitialize = true;
    [SerializeField] private bool saveToPlayerPrefs = true;

    private int[] _levelIndicesByQualityIndex; // length = 4, values are Unity quality level indices
    private int _lastUiIndex;
    private bool _mouseStepping;
    private PointerCatcher _pointerCatcher;

    private void Awake()
    {
        if (qualitySlider == null)
            qualitySlider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (qualitySlider == null)
        {
            Debug.LogWarning($"{nameof(GraphicsQualitySliderController)}: qualitySlider is not assigned.");
            return;
        }

        BuildLevelIndicesMap();

        qualitySlider.wholeNumbers = true;
        qualitySlider.minValue = 0;
        qualitySlider.maxValue = 3;

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentQualityUiIndex();

        _lastUiIndex = uiIndex;

        qualitySlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        qualitySlider.onValueChanged.AddListener(OnSliderValueChanged);

        qualitySlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);

        if (applyOnInitialize)
            ApplyQuality(uiIndex);

        if (hideHandle)
            HideHandle();

        if (stepByHalfClick)
            SetupPointerCatcher();
    }

    /// <summary>Syncs slider + label from PlayerPrefs without re-applying quality (engine already matches).</summary>
    public void RefreshFromPlayerPrefs()
    {
        if (qualitySlider == null)
            qualitySlider = GetComponent<Slider>();
        if (qualitySlider == null)
            return;

        BuildLevelIndicesMap();

        int stored = saveToPlayerPrefs ? PlayerPrefs.GetInt(PlayerPrefsKey, -1) : -1;
        int uiIndex = stored >= 0 && stored <= 3 ? stored : GetCurrentQualityUiIndex();

        _lastUiIndex = uiIndex;
        qualitySlider.SetValueWithoutNotify(uiIndex);
        UpdateText(uiIndex);
    }

    private void BuildLevelIndicesMap()
    {
        _levelIndicesByQualityIndex = new int[4];
        _levelIndicesByQualityIndex[0] = FindQualityLevelIndex(lowName);
        _levelIndicesByQualityIndex[1] = FindQualityLevelIndex(mediumName);
        _levelIndicesByQualityIndex[2] = FindQualityLevelIndex(highName);
        _levelIndicesByQualityIndex[3] = FindQualityLevelIndex(ultraName);

        int available = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        for (int i = 0; i < 4; i++)
        {
            int idx = _levelIndicesByQualityIndex[i];
            if (idx < 0 || idx >= available)
                _levelIndicesByQualityIndex[i] = Mathf.Clamp(i, 0, Mathf.Max(0, available - 1));
        }
    }

    private void OnDisable()
    {
        if (qualitySlider != null)
            qualitySlider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float v)
    {
        int uiIndex = Mathf.RoundToInt(v);
        uiIndex = Mathf.Clamp(uiIndex, 0, 3);

        // When user is clicking the track with mouse, Unity's Slider tends to jump to the click position.
        // We ignore that and only allow our half-click step logic.
        if (stepByHalfClick)
        {
            var mouse = Mouse.current;
            bool leftMouseDown = mouse != null && mouse.leftButton.isPressed;
            if (_mouseStepping || leftMouseDown)
            {
                if (uiIndex != _lastUiIndex)
                    qualitySlider.SetValueWithoutNotify(_lastUiIndex);
                return;
            }
        }

        if (uiIndex == _lastUiIndex)
            return;

        _lastUiIndex = uiIndex;
        UpdateText(uiIndex);
        ApplyQuality(uiIndex);
    }

    private void ApplyQuality(int uiIndex)
    {
        int unityLevelIndex = _levelIndicesByQualityIndex[uiIndex];
        bool applyExpensiveChanges = true;
        QualitySettings.SetQualityLevel(unityLevelIndex, applyExpensiveChanges);

        if (saveToPlayerPrefs)
            PlayerPrefs.SetInt(PlayerPrefsKey, uiIndex);

        // Some graphics settings take a frame; we rely on Unity to apply.
        if (saveToPlayerPrefs)
            PlayerPrefs.Save();
    }

    private void UpdateText(int uiIndex)
    {
        if (qualityText == null)
            return;

        qualityText.text = uiIndex switch
        {
            0 => "Low",
            1 => "Medium",
            2 => "High",
            3 => "Ultra",
            _ => "High"
        };
    }

    private int GetCurrentQualityUiIndex()
    {
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < 4; i++)
        {
            if (_levelIndicesByQualityIndex[i] == current)
                return i;
        }

        // If current isn't mapped, default to High.
        return 2;
    }

    private int FindQualityLevelIndex(string name)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
            return -1;

        if (string.IsNullOrWhiteSpace(name))
            return -1;

        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (string.Equals(QualitySettings.names[i], name, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private void HideHandle()
    {
        if (qualitySlider == null) return;
        if (qualitySlider.handleRect == null) return;
        qualitySlider.handleRect.gameObject.SetActive(false);
    }

    private void SetupPointerCatcher()
    {
        if (qualitySlider == null) return;

        _pointerCatcher = qualitySlider.GetComponent<PointerCatcher>();
        if (_pointerCatcher == null)
            _pointerCatcher = qualitySlider.gameObject.AddComponent<PointerCatcher>();

        _pointerCatcher.Init(this);
    }

    private void HandleHalfClick(PointerEventData eventData)
    {
        if (qualitySlider == null) return;

        var sliderRect = qualitySlider.GetComponent<RectTransform>();
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

        int currentIndex = _lastUiIndex; // step from last applied (not from Slider's temporary value)
        int nextIndex = clickedLeftHalf ? currentIndex - 1 : currentIndex + 1;
        nextIndex = Mathf.Clamp(nextIndex, 0, 3);
        if (nextIndex == _lastUiIndex)
            return;

        qualitySlider.SetValueWithoutNotify(nextIndex);
        _lastUiIndex = nextIndex;
        UpdateText(nextIndex);
        ApplyQuality(nextIndex);
    }

    private void BeginMouseStep() => _mouseStepping = true;
    private void EndMouseStep() => _mouseStepping = false;

    private sealed class PointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private GraphicsQualitySliderController _controller;
        public void Init(GraphicsQualitySliderController controller) => _controller = controller;

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

