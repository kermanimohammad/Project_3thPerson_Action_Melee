using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>
/// Wires wave spawner events to TMP countdown and a single intermission panel after each cleared wave:
/// freezes player, plays victory audio, shows stats, Continue (next wave) and Main Menu.
/// </summary>
public class BattleWaveFlowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject countdownRoot;

    [Header("Intermission panel (after each wave)")]
    [Tooltip("Optional. If set, this root is used for the post-wave screen. Otherwise falls back to All Waves / Results roots below.")]
    [SerializeField] private GameObject waveIntermissionPanelRoot;
    [SerializeField] private GameObject resultsPanelRoot;
    [SerializeField] private TMP_Text resultsTitleText;
    [SerializeField] private TMP_Text resultsStatsText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonLabel;

    [Header("Legacy / alternate refs (same panel as intermission if you still use them)")]
    [SerializeField] private GameObject allWavesCompleteRoot;
    [SerializeField] private Button victoryMainMenuButton;
    [SerializeField] private Button victoryContinueButton;
    [SerializeField] private TMP_Text victoryTitleText;
    [SerializeField] private TMP_Text victoryStatsText;

    [Tooltip("Optional. If empty, uses tag Player.")]
    [SerializeField] private Transform playerRootOverride;
    [SerializeField] private AudioClip victoryAudioClip;
    [SerializeField, Range(0f, 1f)] private float victoryAudioVolume = 1f;
    [Tooltip("Uses PlayOneShot; add an AudioSource on this object or one will be added at runtime.")]
    [SerializeField] private AudioSource victoryAudioSource;
    [Tooltip("Optional. Assign a mixer group (e.g. SFX) so victory sting follows your mix.")]
    [SerializeField] private AudioMixerGroup victoryOutputMixerGroup;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Optional override for default selection after the intermission panel opens.")]
    [FormerlySerializedAs("victoryFirstSelectedOverride")]
    [SerializeField] private GameObject intermissionFirstSelectedOverride;

    [Header("Copy")]
    [SerializeField] private string countdownFormat = "{0}";
    [SerializeField] private string countdownWithWaveFormat = "Wave {0}/{1} — {2}";
    [Tooltip("Used when Total Waves is 0 (endless). Args: wave number, seconds text.")]
    [SerializeField] private string countdownEndlessWaveFormat = "Wave {0} — {1}";
    [SerializeField] private bool showWaveNumberInCountdown = true;
    [SerializeField] private string resultsTitleFormat = "Wave {0} cleared";
    [SerializeField] private string resultsTitleWithTotalFormat = "Wave {0} of {1} cleared";
    [SerializeField] private string resultsStatsFormat = "Enemies defeated: {0} / {1}";
    [SerializeField] private string continueLabelNextWave = "Continue";

    private int _countdownWaveNumber;
    private int _countdownTotalWaves;
    private WaveClearSummary _lastWaveSummary;
    private bool _intermissionFreezeActive;
    private readonly List<Behaviour> _disabledPlayerBehaviours = new List<Behaviour>(8);
    private PlayerMotor _frozenMotor;

    private GameObject IntermissionPanelRoot =>
        waveIntermissionPanelRoot != null
            ? waveIntermissionPanelRoot
            : resultsPanelRoot != null
                ? resultsPanelRoot
                : allWavesCompleteRoot;

    private TMP_Text EffectiveTitleText =>
        resultsTitleText != null ? resultsTitleText : victoryTitleText;

    private TMP_Text EffectiveStatsText =>
        resultsStatsText != null ? resultsStatsText : victoryStatsText;

    private void OnEnable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnPreWaveCountdownStarted += HandleCountdownStarted;
        waveSpawner.OnPreWaveCountdownTick += HandleCountdownTick;
        waveSpawner.OnWaveCleared += HandleWaveCleared;
        waveSpawner.OnAllWavesCompleted += HandleAllWavesCompleted;
        waveSpawner.OnWaveStarted += HandleWaveStarted;

        WireContinueButtons(true);
        WireMainMenuButtons(true);
    }

    private void OnDisable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnPreWaveCountdownStarted -= HandleCountdownStarted;
        waveSpawner.OnPreWaveCountdownTick -= HandleCountdownTick;
        waveSpawner.OnWaveCleared -= HandleWaveCleared;
        waveSpawner.OnAllWavesCompleted -= HandleAllWavesCompleted;
        waveSpawner.OnWaveStarted -= HandleWaveStarted;

        WireContinueButtons(false);
        WireMainMenuButtons(false);
    }

    private void WireContinueButtons(bool add)
    {
        void Toggle(Button b)
        {
            if (b == null)
                return;
            if (add)
                b.onClick.AddListener(OnContinueClicked);
            else
                b.onClick.RemoveListener(OnContinueClicked);
        }

        Toggle(continueButton);
        Toggle(victoryContinueButton);
    }

    private void WireMainMenuButtons(bool add)
    {
        if (victoryMainMenuButton == null)
            return;
        if (add)
            victoryMainMenuButton.onClick.AddListener(OnMainMenuClicked);
        else
            victoryMainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
    }

    private void Start()
    {
        if (countdownRoot != null)
            countdownRoot.SetActive(false);
        SetIntermissionPanelActive(false);

        if (victoryAudioSource == null && victoryAudioClip != null)
            victoryAudioSource = GetComponent<AudioSource>();
        if (victoryAudioSource == null && victoryAudioClip != null)
            victoryAudioSource = gameObject.AddComponent<AudioSource>();

        if (victoryAudioSource != null && victoryOutputMixerGroup != null)
            victoryAudioSource.outputAudioMixerGroup = victoryOutputMixerGroup;
    }

    private void SetIntermissionPanelActive(bool active)
    {
        if (!active)
        {
            if (waveIntermissionPanelRoot != null)
                waveIntermissionPanelRoot.SetActive(false);
            if (resultsPanelRoot != null)
                resultsPanelRoot.SetActive(false);
            if (allWavesCompleteRoot != null)
                allWavesCompleteRoot.SetActive(false);
            return;
        }

        GameObject show = IntermissionPanelRoot;
        if (show == null)
            return;

        if (waveIntermissionPanelRoot != null && waveIntermissionPanelRoot != show)
            waveIntermissionPanelRoot.SetActive(false);
        if (resultsPanelRoot != null && resultsPanelRoot != show)
            resultsPanelRoot.SetActive(false);
        if (allWavesCompleteRoot != null && allWavesCompleteRoot != show)
            allWavesCompleteRoot.SetActive(false);

        show.SetActive(true);
    }

    private void HandleCountdownStarted(int waveNumber, int totalWaves)
    {
        _countdownWaveNumber = waveNumber;
        _countdownTotalWaves = totalWaves;
        EndIntermissionPresentation();
        if (countdownRoot != null)
            countdownRoot.SetActive(true);
    }

    private void HandleCountdownTick(int secondsLeft)
    {
        if (countdownText == null)
            return;

        if (secondsLeft <= 0)
        {
            countdownText.text = string.Empty;
            if (countdownRoot != null)
                countdownRoot.SetActive(false);
            return;
        }

        if (showWaveNumberInCountdown)
        {
            string secText = string.Format(countdownFormat, secondsLeft);
            if (_countdownTotalWaves <= 0)
                countdownText.text = string.Format(countdownEndlessWaveFormat, _countdownWaveNumber, secText);
            else
                countdownText.text = string.Format(countdownWithWaveFormat, _countdownWaveNumber, _countdownTotalWaves, secText);
        }
        else
            countdownText.text = string.Format(countdownFormat, secondsLeft);
    }

    private void HandleWaveCleared(WaveClearSummary summary)
    {
        _lastWaveSummary = summary;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        ApplyIntermissionFreezeAndPresentation();
        SetIntermissionPanelActive(true);

        TMP_Text title = EffectiveTitleText;
        if (title != null)
        {
            title.text = summary.TotalWaves > 0
                ? string.Format(resultsTitleWithTotalFormat, summary.ClearedWaveNumber, summary.TotalWaves)
                : string.Format(resultsTitleFormat, summary.ClearedWaveNumber);
        }

        TMP_Text stats = EffectiveStatsText;
        if (stats != null)
            stats.text = string.Format(resultsStatsFormat, summary.EnemiesKilled, summary.EnemiesTotalInWave);

        if (continueButtonLabel != null)
            continueButtonLabel.text = continueLabelNextWave;

        StartCoroutine(SelectIntermissionDefaultAfterUiReady());
    }

    private void HandleWaveStarted(int currentWave, int totalWaves)
    {
        SetIntermissionPanelActive(false);
    }

    /// <summary>
    /// Finite run: after the player continues past the last wave, the spawner stops without a new countdown — hide UI and unfreeze here.
    /// </summary>
    private void HandleAllWavesCompleted()
    {
        if (countdownRoot != null)
            countdownRoot.SetActive(false);
        EndIntermissionPresentation();
    }

    private void ApplyIntermissionFreezeAndPresentation()
    {
        _intermissionFreezeActive = true;

        FreezePlayerForIntermissionUi();

        if (victoryAudioClip != null && victoryAudioSource != null)
            victoryAudioSource.PlayOneShot(victoryAudioClip, victoryAudioVolume);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EndIntermissionPresentation()
    {
        SetIntermissionPanelActive(false);
        RestorePlayerControlInternal();
        RestoreGameplayCursor();
        _intermissionFreezeActive = false;
    }

    private static void RestoreGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void FreezePlayerForIntermissionUi()
    {
        RestorePlayerControlInternal();

        GameObject root = ResolvePlayerRootForFreeze();
        if (root == null)
        {
            Debug.LogWarning(
                $"{nameof(BattleWaveFlowUI)}: Could not find an active {nameof(PlayerMotor)} or 'Player' tag. Assign {nameof(playerRootOverride)} to freeze controls after a wave clears.",
                this);
            return;
        }

        void DisableIfEnabled(Behaviour b)
        {
            if (b != null && b.enabled)
            {
                b.enabled = false;
                _disabledPlayerBehaviours.Add(b);
            }
        }

        _frozenMotor = root.GetComponentInChildren<PlayerMotor>(true);
        _frozenMotor?.SetExternalGameplayFreeze(true);

        foreach (var r in root.GetComponentsInChildren<PlayerInputRouter>(true))
        {
            r.ClearBufferedInput();
            DisableIfEnabled(r);
        }

        foreach (var p in root.GetComponentsInChildren<PlayerCameraPivot>(true))
            DisableIfEnabled(p);

        foreach (var c in root.GetComponentsInChildren<PlayerCombat>(true))
            DisableIfEnabled(c);

        foreach (var d in root.GetComponentsInChildren<PlayerDodge>(true))
            DisableIfEnabled(d);

        foreach (var a in root.GetComponentsInChildren<PlayerAttackMotion>(true))
            DisableIfEnabled(a);

        foreach (var h in root.GetComponentsInChildren<PlayerHitMove>(true))
            DisableIfEnabled(h);
    }

    private GameObject ResolvePlayerRootForFreeze()
    {
        if (playerRootOverride != null)
            return playerRootOverride.root.gameObject;

        var motors = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
        for (int i = 0; i < motors.Length; i++)
        {
            PlayerMotor m = motors[i];
            if (m != null && m.isActiveAndEnabled && m.gameObject.activeInHierarchy)
                return m.transform.root.gameObject;
        }

        GameObject tagged = null;
        try
        {
            tagged = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            // "Player" tag not registered in Tag Manager
        }

        return tagged != null ? tagged.transform.root.gameObject : null;
    }

    private void RestorePlayerControlInternal()
    {
        _frozenMotor?.SetExternalGameplayFreeze(false);
        _frozenMotor = null;

        for (int i = 0; i < _disabledPlayerBehaviours.Count; i++)
        {
            Behaviour b = _disabledPlayerBehaviours[i];
            if (b != null)
                b.enabled = true;
        }

        _disabledPlayerBehaviours.Clear();
    }

    private IEnumerator SelectIntermissionDefaultAfterUiReady()
    {
        yield return null;

        if (EventSystem.current == null)
            yield break;

        Button continueB = continueButton != null ? continueButton : victoryContinueButton;

        GameObject sel = intermissionFirstSelectedOverride != null && intermissionFirstSelectedOverride.activeInHierarchy
            ? intermissionFirstSelectedOverride
            : continueB != null && continueB.gameObject.activeInHierarchy
                ? continueB.gameObject
                : victoryMainMenuButton != null && victoryMainMenuButton.gameObject.activeInHierarchy
                    ? victoryMainMenuButton.gameObject
                    : null;

        if (sel == null)
            yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(sel);
    }

    private void OnContinueClicked()
    {
        if (waveSpawner != null)
            waveSpawner.ConfirmContinueToNextWave();
    }

    private void OnMainMenuClicked()
    {
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (_intermissionFreezeActive)
            RestorePlayerControlInternal();
    }
}
