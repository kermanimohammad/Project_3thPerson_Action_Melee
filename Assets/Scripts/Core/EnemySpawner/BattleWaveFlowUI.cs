using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires wave spawner events to TMP countdown, inter-wave results panel, and final victory panel.
/// Victory: freezes player input/look, plays a clip, shows UI with Continue + Main Menu.
/// </summary>
public class BattleWaveFlowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private GameObject resultsPanelRoot;
    [SerializeField] private TMP_Text resultsTitleText;
    [SerializeField] private TMP_Text resultsStatsText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonLabel;

    [Header("Victory (all waves cleared)")]
    [SerializeField] private GameObject allWavesCompleteRoot;
    [Tooltip("Optional. If empty, uses tag Player.")]
    [SerializeField] private Transform playerRootOverride;
    [SerializeField] private AudioClip victoryAudioClip;
    [SerializeField, Range(0f, 1f)] private float victoryAudioVolume = 1f;
    [Tooltip("Uses PlayOneShot; add an AudioSource on this object or one will be added at runtime.")]
    [SerializeField] private AudioSource victoryAudioSource;
    [Tooltip("Optional. Assign a mixer group (e.g. SFX) so victory sting follows your mix. If empty, keeps the AudioSource’s current Output.")]
    [SerializeField] private AudioMixerGroup victoryOutputMixerGroup;
    [SerializeField] private Button victoryMainMenuButton;
    [SerializeField] private Button victoryContinueButton;
    [Tooltip("Shown on victory panel; uses last wave stats if set.")]
    [SerializeField] private TMP_Text victoryTitleText;
    [SerializeField] private string victoryTitleDefault = "Victory!";
    [SerializeField] private TMP_Text victoryStatsText;
    [SerializeField] private string victoryStatsFormat = "Last wave: {0} / {1} enemies";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("After victory Continue: hide panel and unlock player (e.g. explore arena). Main Menu still loads a new scene.")]
    [SerializeField] private bool victoryContinueRestoresPlayerControl = true;
    [Tooltip("Optional override for default selection. If empty, Continue is selected when present, else Main Menu.")]
    [SerializeField] private GameObject victoryFirstSelectedOverride;

    [Header("Copy (optional)")]
    [SerializeField] private string countdownFormat = "{0}";
    [SerializeField] private string countdownWithWaveFormat = "Wave {0}/{1} — {2}";
    [SerializeField] private bool showWaveNumberInCountdown = true;
    [SerializeField] private string resultsTitleFormat = "Wave {0} cleared";
    [SerializeField] private string resultsStatsFormat = "Enemies defeated: {0} / {1}";
    [SerializeField] private string continueLabelNextWave = "Continue";
    [SerializeField] private string continueLabelAllDone = "Close";

    private int _countdownWaveNumber;
    private int _countdownTotalWaves;
    private WaveClearSummary _lastWaveSummary;
    private bool _victoryActive;
    private readonly List<Behaviour> _disabledPlayerBehaviours = new List<Behaviour>(8);
    private PlayerMotor _lockedPlayerMotor;
    private bool _motorWasLockedBeforeVictory;

    private void OnEnable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnPreWaveCountdownStarted += HandleCountdownStarted;
        waveSpawner.OnPreWaveCountdownTick += HandleCountdownTick;
        waveSpawner.OnWaveCleared += HandleWaveCleared;
        waveSpawner.OnAllWavesCompleted += HandleAllWavesCompleted;
        waveSpawner.OnWaveStarted += HandleWaveStarted;

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
        if (victoryMainMenuButton != null)
            victoryMainMenuButton.onClick.AddListener(OnVictoryMainMenuClicked);
        if (victoryContinueButton != null)
            victoryContinueButton.onClick.AddListener(OnVictoryContinueClicked);
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

        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
        if (victoryMainMenuButton != null)
            victoryMainMenuButton.onClick.RemoveListener(OnVictoryMainMenuClicked);
        if (victoryContinueButton != null)
            victoryContinueButton.onClick.RemoveListener(OnVictoryContinueClicked);
    }

    private void Start()
    {
        if (countdownRoot != null)
            countdownRoot.SetActive(false);
        if (resultsPanelRoot != null)
            resultsPanelRoot.SetActive(false);
        if (allWavesCompleteRoot != null)
            allWavesCompleteRoot.SetActive(false);

        if (victoryAudioSource == null && victoryAudioClip != null)
            victoryAudioSource = GetComponent<AudioSource>();
        if (victoryAudioSource == null && victoryAudioClip != null)
            victoryAudioSource = gameObject.AddComponent<AudioSource>();

        if (victoryAudioSource != null && victoryOutputMixerGroup != null)
            victoryAudioSource.outputAudioMixerGroup = victoryOutputMixerGroup;
    }

    private void HandleCountdownStarted(int waveNumber, int totalWaves)
    {
        _countdownWaveNumber = waveNumber;
        _countdownTotalWaves = totalWaves;
        if (countdownRoot != null)
            countdownRoot.SetActive(true);
        if (resultsPanelRoot != null)
            resultsPanelRoot.SetActive(false);
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
            countdownText.text = string.Format(countdownWithWaveFormat, _countdownWaveNumber, _countdownTotalWaves, string.Format(countdownFormat, secondsLeft));
        else
            countdownText.text = string.Format(countdownFormat, secondsLeft);
    }

    private void HandleWaveCleared(WaveClearSummary summary)
    {
        _lastWaveSummary = summary;

        if (resultsPanelRoot != null)
            resultsPanelRoot.SetActive(true);
        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (resultsTitleText != null)
            resultsTitleText.text = string.Format(resultsTitleFormat, summary.ClearedWaveNumber);

        if (resultsStatsText != null)
            resultsStatsText.text = string.Format(resultsStatsFormat, summary.EnemiesKilled, summary.EnemiesTotalInWave);

        if (continueButtonLabel != null)
            continueButtonLabel.text = summary.MoreWavesRemainAfterThisClear ? continueLabelNextWave : continueLabelAllDone;
    }

    private void HandleWaveStarted(int currentWave, int totalWaves)
    {
        if (resultsPanelRoot != null)
            resultsPanelRoot.SetActive(false);
    }

    private void HandleAllWavesCompleted()
    {
        if (resultsPanelRoot != null)
            resultsPanelRoot.SetActive(false);
        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        ApplyVictoryFreezeAndPresentation();

        if (allWavesCompleteRoot != null)
            allWavesCompleteRoot.SetActive(true);

        StartCoroutine(SelectVictoryDefaultAfterUiReady());
    }

    private void ApplyVictoryFreezeAndPresentation()
    {
        _victoryActive = true;

        FreezePlayerForVictoryUi();

        if (victoryTitleText != null)
            victoryTitleText.text = victoryTitleDefault;

        if (victoryStatsText != null)
        {
            victoryStatsText.text = string.Format(
                victoryStatsFormat,
                _lastWaveSummary.EnemiesKilled,
                _lastWaveSummary.EnemiesTotalInWave);
        }

        if (victoryAudioClip != null && victoryAudioSource != null)
            victoryAudioSource.PlayOneShot(victoryAudioClip, victoryAudioVolume);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void FreezePlayerForVictoryUi()
    {
        RestorePlayerControlInternal();

        GameObject root = playerRootOverride != null
            ? playerRootOverride.gameObject
            : GameObject.FindGameObjectWithTag("Player");

        if (root == null)
            return;

        foreach (var r in root.GetComponentsInChildren<PlayerInputRouter>(true))
        {
            if (r != null && r.enabled)
            {
                r.enabled = false;
                _disabledPlayerBehaviours.Add(r);
            }
        }

        foreach (var p in root.GetComponentsInChildren<PlayerCameraPivot>(true))
        {
            if (p != null && p.enabled)
            {
                p.enabled = false;
                _disabledPlayerBehaviours.Add(p);
            }
        }

        _lockedPlayerMotor = root.GetComponentInChildren<PlayerMotor>(true);
        if (_lockedPlayerMotor != null)
        {
            _motorWasLockedBeforeVictory = _lockedPlayerMotor.MovementLocked;
            _lockedPlayerMotor.SetMovementLocked(true);
        }
    }

    private void RestorePlayerControlInternal()
    {
        for (int i = 0; i < _disabledPlayerBehaviours.Count; i++)
        {
            Behaviour b = _disabledPlayerBehaviours[i];
            if (b != null)
                b.enabled = true;
        }

        _disabledPlayerBehaviours.Clear();

        if (_lockedPlayerMotor != null)
        {
            _lockedPlayerMotor.SetMovementLocked(_motorWasLockedBeforeVictory);
            _lockedPlayerMotor = null;
            _motorWasLockedBeforeVictory = false;
        }
    }

    /// <summary>After enabling the victory panel, selection must run next frame so the selectable picks up highlight.</summary>
    private IEnumerator SelectVictoryDefaultAfterUiReady()
    {
        yield return null;

        if (EventSystem.current == null)
            yield break;

        GameObject sel = victoryFirstSelectedOverride != null && victoryFirstSelectedOverride.activeInHierarchy
            ? victoryFirstSelectedOverride
            : victoryContinueButton != null && victoryContinueButton.gameObject.activeInHierarchy
                ? victoryContinueButton.gameObject
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

    private void OnVictoryMainMenuClicked()
    {
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void OnVictoryContinueClicked()
    {
        if (!victoryContinueRestoresPlayerControl)
            return;

        _victoryActive = false;
        RestorePlayerControlInternal();

        if (allWavesCompleteRoot != null)
            allWavesCompleteRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_victoryActive)
            RestorePlayerControlInternal();
    }
}
