using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Splash / intro: hold <b>Esc</b>, gamepad <b>B</b> (East), or <b>left mouse</b> on the slider to fill it over <see cref="holdSecondsRequired"/>; release early and the bar drains smoothly.
/// When the bar completes, loads the next scene. Video end still loads immediately (no hold).
/// For an on-screen control, wire EventTrigger PointerDown → <see cref="UiHoldPressed"/> and PointerUp/PointerExit → <see cref="UiHoldReleased"/>.
/// </summary>
public sealed class SplashIntroToMainMenu : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Hold to skip")]
    [SerializeField] private Slider holdProgressSlider;
    [Tooltip("Seconds Esc / B must be held continuously to fill the bar and load the scene.")]
    [SerializeField, Min(0.01f)] private float holdSecondsRequired = 3f;
    [Tooltip("Seconds to drain the bar from full to empty when released early (linear).")]
    [SerializeField, Min(0.01f)] private float emptyLerpDuration = 0.5f;
    [Tooltip("Seconds after scene start before hold input counts (e.g. match delayed skip UI).")]
    [SerializeField, Min(0f)] private float enableHoldInputAfterSeconds = 6f;
    [Tooltip("If true, uses real time (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Video end (optional)")]
    [Tooltip("When the clip reaches its end (not looping), loads scene immediately (ignores hold slider).")]
    [SerializeField] private VideoPlayer videoPlayer;

    private bool _holdInputEnabled;
    private bool _loading;
    private float _holdProgress;
    private bool _uiHoldActive;
    private RectTransform _sliderRect;
    private Canvas _sliderCanvas;

    private void Awake()
    {
        if (holdProgressSlider != null)
        {
            holdProgressSlider.minValue = 0f;
            holdProgressSlider.maxValue = 1f;
            holdProgressSlider.value = 0f;
            _sliderRect = holdProgressSlider.GetComponent<RectTransform>();
            _sliderCanvas = holdProgressSlider.GetComponentInParent<Canvas>();
        }
    }

    private void Start()
    {
        if (videoPlayer == null)
            videoPlayer = FindFirstObjectByType<VideoPlayer>();

        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoLoopPointReached;

        StartCoroutine(EnableHoldInputAfterDelay());
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
    }

    private IEnumerator EnableHoldInputAfterDelay()
    {
        if (enableHoldInputAfterSeconds > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(enableHoldInputAfterSeconds);
            else
                yield return new WaitForSeconds(enableHoldInputAfterSeconds);
        }

        _holdInputEnabled = true;
    }

    private void Update()
    {
        if (_loading)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        bool hold = false;
        if (_holdInputEnabled)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.isPressed)
                hold = true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.isPressed)
                hold = true;
            if (_uiHoldActive)
                hold = true;
            if (IsLeftClickHeldOnSlider())
                hold = true;
        }

        if (hold)
            _holdProgress = Mathf.Min(1f, _holdProgress + dt / holdSecondsRequired);
        else if (_holdProgress > 0f)
            _holdProgress = Mathf.Max(0f, _holdProgress - dt / emptyLerpDuration);

        if (holdProgressSlider != null)
            holdProgressSlider.value = _holdProgress;

        if (_holdProgress >= 1f - 1e-5f)
            LoadMainMenu();
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        LoadMainMenu();
    }

    /// <summary>EventTrigger PointerDown on a skip button (must hold).</summary>
    public void UiHoldPressed()
    {
        _uiHoldActive = true;
    }

    /// <summary>EventTrigger PointerUp / PointerExit.</summary>
    public void UiHoldReleased()
    {
        _uiHoldActive = false;
    }

    /// <summary>Optional: instant load (e.g. debug). Normal flow uses hold slider.</summary>
    public void LoadMainMenuFromUi()
    {
        LoadMainMenu();
    }

    private void LoadMainMenu()
    {
        if (_loading)
            return;

        _loading = true;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.Stop();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private bool IsLeftClickHeldOnSlider()
    {
        if (_sliderRect == null)
            return false;

        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            return false;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Camera eventCam = null;
        if (_sliderCanvas != null &&
            (_sliderCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
             _sliderCanvas.renderMode == RenderMode.WorldSpace))
            eventCam = _sliderCanvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(_sliderRect, screenPos, eventCam);
    }
}
