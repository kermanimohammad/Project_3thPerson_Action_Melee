using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// During a <see cref="PlayableDirector"/> cutscene, <b>Esc</b> or gamepad <b>B</b> (East): <b>release</b> before the hold threshold
/// advances to the next shot (playhead-based). <b>Hold</b> the same key for <see cref="holdSecondsToSkipEntireTimeline"/> seconds
/// to seek to the end and load the next scene. Optional <c>holdSkipProgressSlider</c> fills 0→1 over that time (Esc/B or left-click hold on the slider) and clears if released early.
/// After the last shot, a short release also ends the cutscene.
/// When the timeline <b>stops at the end</b>, loads <b>BattleArea</b> asynchronously while showing an optional loading panel + slider.
/// Shot positions are <b>frame numbers</b> (Timeline ruler in Frames).
/// </summary>
public sealed class CutsceneShotSkipInput : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [Tooltip("Start frame for shot 0, 1, 2, … (same order as Cinemachine Shots). Usually first entry is 0.")]
    [SerializeField] private int[] shotStartFrames = new int[9];
    [Tooltip("If the Playable Asset is not a Timeline, or you want to override: FPS used to convert frames → time.")]
    [SerializeField] private float fallbackFramesPerSecond = 60f;
    [Tooltip("If true, Esc / B use the Input System.")]
    [SerializeField] private bool enableInput = true;
    [Tooltip("Keep Esc or B held for this many seconds to skip the entire timeline and load the next scene.")]
    [SerializeField] private float holdSecondsToSkipEntireTimeline = 3f;
    [Header("Hold to skip entire cutscene (UI)")]
    [Tooltip("Optional. Fills 0→1 while Esc/B is held, or while left mouse button is held over this slider; resets if released before the hold completes.")]
    [SerializeField] private Slider holdSkipProgressSlider;

    [Header("After cutscene")]
    [SerializeField] private string battleAreaSceneName = "BattleArea";
    [Tooltip("Activated as soon as async load starts (keep inactive in the scene until then).")]
    [SerializeField] private GameObject loadingPanel;
    [Tooltip("0–1 while loading; uses Unity async progress (0…0.9 then full when activation is allowed).")]
    [SerializeField] private Slider loadingProgressSlider;
    [Tooltip("Shows integer percent 0–100 (same progress as the slider). {0} = percent value.")]
    [SerializeField] private TextMeshProUGUI loadingProgressText;
    [SerializeField] private string loadingProgressTextFormat = "{0}%";

    private bool _loadStarted;
    private bool _directorWasPlaying;

    private bool _skipInputHeldLastFrame;
    private float _skipHoldStartUnscaled;
    private bool _holdFullTimelineSkipFired;
    private bool _escOrEastUsedThisHold;

    private void Awake()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        SetHoldSkipProgressSlider(0f);
    }

    private void OnEnable()
    {
        if (director != null)
            director.stopped += OnDirectorStopped;
    }

    private void OnDisable()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
    }

    private void OnDirectorStopped(PlayableDirector d)
    {
        BeginLoadBattleAreaAsync();
    }

    private void Update()
    {
        if (director == null)
            return;

        if (!_loadStarted)
            PollTimelineNaturalEnd();

        if (!enableInput)
            return;

        if (shotStartFrames == null || shotStartFrames.Length == 0)
            return;

        bool escHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
        bool eastHeld = Gamepad.current != null && Gamepad.current.buttonEast.isPressed;
        bool mouseHeldOnSlider = IsLeftClickHeldOnHoldSkipSlider();
        bool skipHeld = escHeld || eastHeld || mouseHeldOnSlider;

        if (skipHeld && !_skipInputHeldLastFrame)
        {
            _skipHoldStartUnscaled = Time.unscaledTime;
            _holdFullTimelineSkipFired = false;
            _escOrEastUsedThisHold = false;
        }

        float holdThreshold = Mathf.Max(0f, holdSecondsToSkipEntireTimeline);

        if (skipHeld)
        {
            if (escHeld || eastHeld)
                _escOrEastUsedThisHold = true;

            float held = Time.unscaledTime - _skipHoldStartUnscaled;
            float denom = holdThreshold > 0f ? holdThreshold : 0.0001f;
            SetHoldSkipProgressSlider(Mathf.Min(1f, held / denom));

            if (!_holdFullTimelineSkipFired && held >= holdThreshold)
            {
                _holdFullTimelineSkipFired = true;
                SeekTimelineToEndAndLoad();
            }
        }
        else
        {
            SetHoldSkipProgressSlider(0f);

            if (_skipInputHeldLastFrame && !_holdFullTimelineSkipFired && _escOrEastUsedThisHold)
            {
                float held = Time.unscaledTime - _skipHoldStartUnscaled;
                if (held < holdSecondsToSkipEntireTimeline)
                    GoToNextShot();
            }

            _holdFullTimelineSkipFired = false;
        }

        _skipInputHeldLastFrame = skipHeld;
    }

    /// <summary>
    /// <see cref="PlayableDirector.stopped"/> does not always fire when the timeline reaches the end in play mode;
    /// this detects natural completion (playback reached end of duration).
    /// </summary>
    private void PollTimelineNaturalEnd()
    {
        if (director.playableAsset == null)
            return;

        double dur = director.duration;
        if (dur <= 0.0001)
            return;

        if (director.state == PlayState.Playing)
            _directorWasPlaying = true;

        if (!_directorWasPlaying)
            return;

        bool atEnd = director.time >= dur - 0.03f;

        if (atEnd && director.state != PlayState.Playing)
        {
            BeginLoadBattleAreaAsync();
            return;
        }

        if (atEnd && director.state == PlayState.Playing)
            BeginLoadBattleAreaAsync();
    }

    private void GoToNextShot()
    {
        int currentShot = GetCurrentShotIndexFromTimeline();
        int nextShot = currentShot + 1;

        if (nextShot >= shotStartFrames.Length)
        {
            SeekTimelineToEndAndLoad();
            return;
        }

        double fps = GetTimelineFrameRate();
        int frame = shotStartFrames[nextShot];
        if (frame < 0)
            frame = 0;

        double t = frame / fps;
        if (director.duration > 0.0 && t > director.duration)
            t = director.duration;

        director.time = t;
        director.Evaluate();
    }

    /// <summary>
    /// Largest shot index whose start frame is still at or before the current playhead (so Esc skips the shot we are in).
    /// </summary>
    private int GetCurrentShotIndexFromTimeline()
    {
        if (shotStartFrames == null || shotStartFrames.Length == 0)
            return 0;

        double fps = GetTimelineFrameRate();
        double currentFrame = director.time * fps;

        const double frameEpsilon = 0.5;
        int best = 0;
        for (int i = 0; i < shotStartFrames.Length; i++)
        {
            if (shotStartFrames[i] <= currentFrame + frameEpsilon)
                best = i;
        }

        return best;
    }

    private void SeekTimelineToEndAndLoad()
    {
        director.time = director.duration;
        director.Evaluate();
        director.Stop();
        BeginLoadBattleAreaAsync();
    }

    private void BeginLoadBattleAreaAsync()
    {
        if (_loadStarted)
            return;

        _loadStarted = true;
        enableInput = false;
        StartCoroutine(LoadBattleAreaRoutine());
    }

    private IEnumerator LoadBattleAreaRoutine()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingProgressSlider != null)
        {
            loadingProgressSlider.minValue = 0f;
            loadingProgressSlider.maxValue = 1f;
        }

        ApplyLoadingProgressVisual(0f);

        AsyncOperation op = SceneManager.LoadSceneAsync(battleAreaSceneName, LoadSceneMode.Single);
        if (op == null)
            yield break;

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            ApplyLoadingProgressVisual(op.progress / 0.9f);
            yield return null;
        }

        ApplyLoadingProgressVisual(1f);

        op.allowSceneActivation = true;
    }

    private void SetHoldSkipProgressSlider(float zeroToOne)
    {
        if (holdSkipProgressSlider == null)
            return;

        holdSkipProgressSlider.minValue = 0f;
        holdSkipProgressSlider.maxValue = 1f;
        holdSkipProgressSlider.value = Mathf.Clamp01(zeroToOne);
    }

    private void ApplyLoadingProgressVisual(float zeroToOne)
    {
        float t = Mathf.Clamp01(zeroToOne);

        if (loadingProgressSlider != null)
            loadingProgressSlider.value = t;

        if (loadingProgressText != null && !string.IsNullOrEmpty(loadingProgressTextFormat))
        {
            int pct = Mathf.RoundToInt(t * 100f);
            loadingProgressText.text = string.Format(loadingProgressTextFormat, pct);
        }
    }

    private double GetTimelineFrameRate()
    {
        if (director != null && director.playableAsset is TimelineAsset timeline)
            return timeline.editorSettings.frameRate;

        return Mathf.Max(1f, fallbackFramesPerSecond);
    }

    /// <summary>Reset if you replay the same director instance in edit/play tests.</summary>
    public void ResetShotIndex()
    {
        _loadStarted = false;
        _directorWasPlaying = false;
        _skipInputHeldLastFrame = false;
        _holdFullTimelineSkipFired = false;
        _escOrEastUsedThisHold = false;
        SetHoldSkipProgressSlider(0f);
    }

    private bool IsLeftClickHeldOnHoldSkipSlider()
    {
        if (holdSkipProgressSlider == null || Mouse.current == null)
            return false;

        if (!Mouse.current.leftButton.isPressed)
            return false;

        if (!(holdSkipProgressSlider.transform is RectTransform rt))
            return false;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
    }
}
