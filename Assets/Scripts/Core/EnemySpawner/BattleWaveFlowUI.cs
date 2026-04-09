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
    [Tooltip("Recovery break (after every N waves). Args: seconds left, upcoming wave number.")]
    [SerializeField] private string recoveryCountdownFormat = "Recovery: {0}s — wave {1} next (use trees)";
    [SerializeField] private bool showWaveNumberInCountdown = true;
    [SerializeField] private string resultsTitleFormat = "Wave {0} cleared";
    [SerializeField] private string resultsTitleWithTotalFormat = "Wave {0} of {1} cleared";
    [SerializeField] private string resultsStatsFormat = "Enemies defeated: {0} / {1}";
    [SerializeField] private string continueLabelNextWave = "Continue";

    [Header("Defeat (player died)")]
    [SerializeField] private GameObject defeatPanelRoot;
    [SerializeField] private TMP_Text defeatStatsText;
    [SerializeField] private Button defeatRestartButton;
    [SerializeField] private Button defeatMainMenuButton;
    [Tooltip("Optional override for default selection on defeat panel.")]
    [SerializeField] private GameObject defeatFirstSelectedOverride;
    [SerializeField] private string defeatSceneRestartNameOverride = "";

    [Header("Defeat conditions")]
    [Tooltip("Optional. If empty, tries GlobalReferences.MagicStone and then name-based fallback (MagicalSton/MagicStone).")]
    [SerializeField] private GameObject magicStoneObjectiveOverride;
    [SerializeField] private string defeatStatsFormat = "Waves cleared: {0}\nEnemies defeated: {1}";
    [Tooltip("Retry interval (seconds) to find and bind player Health if it spawns late. 0 disables retry.")]
    [SerializeField, Min(0f)] private float playerHealthBindRetrySeconds = 1f;
    [SerializeField] private string playerTag = "Player";

    private int _countdownWaveNumber;
    private int _countdownTotalWaves;
    private int _recoveryUpcomingWave;
    private bool _recoveryCountdownActive;
    private WaveClearSummary _lastWaveSummary;
    private bool _intermissionFreezeActive;
    private readonly List<Behaviour> _disabledPlayerBehaviours = new List<Behaviour>(8);
    private PlayerMotor _frozenMotor;
    private Health _playerHealth;
    private Health _magicStoneHealth;
    private bool _gameOverDefeat;
    private Coroutine _playerHealthBindRoutine;

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
        waveSpawner.OnRecoveryBreakStarted += HandleRecoveryBreakStarted;
        waveSpawner.OnRecoveryBreakTick += HandleRecoveryBreakTick;
        waveSpawner.OnWaveCleared += HandleWaveCleared;
        waveSpawner.OnAllWavesCompleted += HandleAllWavesCompleted;
        waveSpawner.OnWaveStarted += HandleWaveStarted;

        WireContinueButtons(true);
        WireMainMenuButtons(true);
        WireDefeatButtons(true);
        TryBindPlayerHealthForDefeat();
        TryBindMagicStoneForDefeat();
    }

    private void OnDisable()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.OnPreWaveCountdownStarted -= HandleCountdownStarted;
        waveSpawner.OnPreWaveCountdownTick -= HandleCountdownTick;
        waveSpawner.OnRecoveryBreakStarted -= HandleRecoveryBreakStarted;
        waveSpawner.OnRecoveryBreakTick -= HandleRecoveryBreakTick;
        waveSpawner.OnWaveCleared -= HandleWaveCleared;
        waveSpawner.OnAllWavesCompleted -= HandleAllWavesCompleted;
        waveSpawner.OnWaveStarted -= HandleWaveStarted;

        WireContinueButtons(false);
        WireMainMenuButtons(false);
        WireDefeatButtons(false);
        UnbindPlayerHealthForDefeat();
        UnbindMagicStoneForDefeat();
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

    private void WireDefeatButtons(bool add)
    {
        void Toggle(Button b, UnityEngine.Events.UnityAction act)
        {
            if (b == null)
                return;
            if (add)
                b.onClick.AddListener(act);
            else
                b.onClick.RemoveListener(act);
        }

        Toggle(defeatRestartButton, OnDefeatRestartClicked);
        Toggle(defeatMainMenuButton, OnMainMenuClicked);
    }

    private void Start()
    {
        if (countdownRoot != null)
            countdownRoot.SetActive(false);
        SetIntermissionPanelActive(false);
        SetDefeatPanelActive(false);

        if (victoryAudioSource == null && victoryAudioClip != null)
            victoryAudioSource = GetComponent<AudioSource>();
        if (victoryAudioSource == null && victoryAudioClip != null)
            victoryAudioSource = gameObject.AddComponent<AudioSource>();

        if (victoryAudioSource != null && victoryOutputMixerGroup != null)
            victoryAudioSource.outputAudioMixerGroup = victoryOutputMixerGroup;
    }

    private void SetDefeatPanelActive(bool active)
    {
        if (defeatPanelRoot != null)
            defeatPanelRoot.SetActive(active);
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
        if (_gameOverDefeat)
            return;
        _recoveryCountdownActive = false;
        _countdownWaveNumber = waveNumber;
        _countdownTotalWaves = totalWaves;
        EndIntermissionPresentation();
        if (countdownRoot != null)
            countdownRoot.SetActive(true);
    }

    private void HandleRecoveryBreakStarted(int upcomingWaveNumber, int totalSeconds)
    {
        if (_gameOverDefeat)
            return;
        _recoveryUpcomingWave = upcomingWaveNumber;
        _recoveryCountdownActive = true;
        EndIntermissionPresentation();
        if (countdownRoot != null)
            countdownRoot.SetActive(true);
        if (countdownText != null)
            countdownText.text = totalSeconds > 0
                ? string.Format(recoveryCountdownFormat, totalSeconds, upcomingWaveNumber)
                : string.Empty;
    }

    private void HandleRecoveryBreakTick(int secondsLeft)
    {
        if (_gameOverDefeat)
            return;
        if (!_recoveryCountdownActive || countdownText == null)
            return;

        if (secondsLeft <= 0)
        {
            countdownText.text = string.Empty;
            if (countdownRoot != null)
                countdownRoot.SetActive(false);
            _recoveryCountdownActive = false;
            return;
        }

        countdownText.text = string.Format(recoveryCountdownFormat, secondsLeft, _recoveryUpcomingWave);
    }

    private void HandleCountdownTick(int secondsLeft)
    {
        if (_gameOverDefeat)
            return;
        if (_recoveryCountdownActive)
            return;

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
        if (_gameOverDefeat)
            return;
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
        if (_gameOverDefeat)
            return;
        SetIntermissionPanelActive(false);
    }

    /// <summary>
    /// Finite run: after the player continues past the last wave, the spawner stops without a new countdown — hide UI and unfreeze here.
    /// </summary>
    private void HandleAllWavesCompleted()
    {
        if (_gameOverDefeat)
            return;
        _recoveryCountdownActive = false;
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
        if (_gameOverDefeat)
            return;
        if (waveSpawner != null)
            waveSpawner.ConfirmContinueToNextWave();
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void OnDefeatRestartClicked()
    {
        Time.timeScale = 1f;
        string scene = !string.IsNullOrWhiteSpace(defeatSceneRestartNameOverride)
            ? defeatSceneRestartNameOverride
            : SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }

    private void TryBindPlayerHealthForDefeat()
    {
        UnbindPlayerHealthForDefeat();

        if (TryResolveActivePlayerHealth(out var h))
        {
            _playerHealth = h;
            _playerHealth.OnDied += HandlePlayerDied;
            return;
        }

        if (playerHealthBindRetrySeconds > 0f && _playerHealthBindRoutine == null)
            _playerHealthBindRoutine = StartCoroutine(PlayerHealthBindRetryRoutine());
    }

    private void UnbindPlayerHealthForDefeat()
    {
        if (_playerHealthBindRoutine != null)
        {
            StopCoroutine(_playerHealthBindRoutine);
            _playerHealthBindRoutine = null;
        }

        if (_playerHealth != null)
            _playerHealth.OnDied -= HandlePlayerDied;
        _playerHealth = null;
    }

    private IEnumerator PlayerHealthBindRetryRoutine()
    {
        while (!_gameOverDefeat && _playerHealth == null)
        {
            if (TryResolveActivePlayerHealth(out var h))
            {
                _playerHealth = h;
                _playerHealth.OnDied += HandlePlayerDied;
                _playerHealthBindRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(playerHealthBindRetrySeconds);
        }

        _playerHealthBindRoutine = null;
    }

    private bool TryResolveActivePlayerHealth(out Health health)
    {
        health = null;

        // Prefer Health under an object tagged Player (including parent chain), and only those active in hierarchy.
        Health[] all = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            if (h == null || !h.isActiveAndEnabled)
                continue;

            if (IsTaggedInParentChain(h.transform, playerTag))
            {
                health = h;
                return true;
            }
        }

        // Fallback: first active Health in scene (in case tag isn't set yet).
        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            if (h == null || !h.isActiveAndEnabled)
                continue;
            health = h;
            return true;
        }

        return false;
    }

    private static bool IsTaggedInParentChain(Transform t, string tag)
    {
        while (t != null)
        {
            if (t.CompareTag(tag))
                return true;
            t = t.parent;
        }
        return false;
    }

    private void HandlePlayerDied()
    {
        TriggerDefeatAndShowPanel();
    }

    private void TryBindMagicStoneForDefeat()
    {
        UnbindMagicStoneForDefeat();

        GameObject stone = ResolveMagicStoneObjective();
        if (stone == null)
            return;

        _magicStoneHealth = stone.GetComponentInChildren<Health>(includeInactive: true);
        if (_magicStoneHealth != null)
            _magicStoneHealth.OnDied += HandleMagicStoneDied;
    }

    private void UnbindMagicStoneForDefeat()
    {
        if (_magicStoneHealth != null)
            _magicStoneHealth.OnDied -= HandleMagicStoneDied;
        _magicStoneHealth = null;
    }

    private void HandleMagicStoneDied()
    {
        TriggerDefeatAndShowPanel();
    }

    private GameObject ResolveMagicStoneObjective()
    {
        if (magicStoneObjectiveOverride != null)
            return magicStoneObjectiveOverride;

        if (GlobalReferences.Instance != null)
        {
            GameObject go = GlobalReferences.Instance.GetMagicStone();
            if (go != null)
                return go;
        }

        // Fallback: robust name contains match used elsewhere in the project.
        var gos = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < gos.Length; i++)
        {
            var go = gos[i];
            if (go == null) continue;
            var n = go.name;
            if (string.IsNullOrEmpty(n)) continue;
            if (n.Contains("MagicalSton") || n.Contains("MagicStone") || n.Contains("Magic Stone"))
                return go;
        }

        return null;
    }

    private void TriggerDefeatAndShowPanel()
    {
        if (_gameOverDefeat)
            return;

        _gameOverDefeat = true;
        _recoveryCountdownActive = false;

        if (waveSpawner != null)
            waveSpawner.StopSpawning();

        SetIntermissionPanelActive(false);
        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        ApplyIntermissionFreezeAndPresentation();
        SetDefeatPanelActive(true);
        WriteDefeatStats();
        StartCoroutine(SelectDefeatDefaultAfterUiReady());
    }

    private void WriteDefeatStats()
    {
        if (defeatStatsText == null)
            return;

        int wavesCleared = waveSpawner != null ? Mathf.Max(0, waveSpawner.WavesClearedOverall) : 0;
        int totalKills = waveSpawner != null ? Mathf.Max(0, waveSpawner.TotalEnemiesKilledOverall) : 0;
        defeatStatsText.text = string.Format(defeatStatsFormat, wavesCleared, totalKills);
    }

    private IEnumerator SelectDefeatDefaultAfterUiReady()
    {
        yield return null;

        if (EventSystem.current == null)
            yield break;

        GameObject sel = defeatFirstSelectedOverride != null && defeatFirstSelectedOverride.activeInHierarchy
            ? defeatFirstSelectedOverride
            : defeatRestartButton != null && defeatRestartButton.gameObject.activeInHierarchy
                ? defeatRestartButton.gameObject
                : defeatMainMenuButton != null && defeatMainMenuButton.gameObject.activeInHierarchy
                    ? defeatMainMenuButton.gameObject
                    : null;

        if (sel == null)
            yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(sel);
    }

    private void OnDestroy()
    {
        if (_intermissionFreezeActive)
            RestorePlayerControlInternal();
        UnbindPlayerHealthForDefeat();
        UnbindMagicStoneForDefeat();
    }
}
