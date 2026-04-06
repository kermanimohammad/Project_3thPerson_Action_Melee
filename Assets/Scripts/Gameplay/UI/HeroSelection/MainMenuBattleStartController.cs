using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire BattleBTN OnClick → <see cref="StartBattle"/>.
/// Saves current character + weapon/helmet/shield, shows loading UI, loads BattleArea asynchronously.
/// </summary>
public class MainMenuBattleStartController : MonoBehaviour
{
    [Header("Sources (same references as your menu pickers)")]
    [SerializeField] private HeroShowcaseCameraController characterShowcase;
    [SerializeField] private MenuWeaponPicker weaponPicker;
    [SerializeField] private MenuHelmetShieldPicker helmetShieldPicker;

    [Header("Target scene")]
    [SerializeField] private string battleSceneName = "BattleArea";

    [Header("Loading UI")]
    [Tooltip("Panel/canvas group shown while the next scene loads (assign an overlay with loading art).")]
    [SerializeField] private GameObject loadingOverlayRoot;
    [Tooltip("Optional: 0–1 fill while loading (leave null if you only show a static image).")]
    [SerializeField] private Slider loadingProgressSlider;
    [Tooltip("Optional: 0–100 or percent text.")]
    [SerializeField] private TextMeshProUGUI loadingProgressText;

    [Header("Loading SFX")]
    [Tooltip("Optional: plays when loading overlay appears (e.g. BaseBoom1).")]
    [SerializeField] private AudioClip loadingStartClip;
    [SerializeField] [Range(0f, 1f)] private float loadingStartClipVolume = 1f;
    [SerializeField] private AudioMixerGroup loadingSfxOutputGroup;

    [Header("Behaviour")]
    [SerializeField] private bool hideLoadingOverlayIfSceneFails = true;

    [Header("MainMenu UI (optional)")]
    [Tooltip("If assigned, this object will be deactivated when loading starts (e.g. Canvas/MainMenu).")]
    [SerializeField] private GameObject mainMenuRootToDeactivate;

    [Header("Menu music (optional)")]
    [Tooltip("If assigned, pause menu music before playing loading one-shot (e.g. BaseBoom1).")]
    [SerializeField] private MainMenuSettingsCoordinator mainMenuSettingsCoordinator;

    private Coroutine _loadRoutine;
    private AudioSource _loadingSfxSource;
    private CursorLockMode _prevCursorLockMode;
    private bool _prevCursorVisible;

    private void Awake()
    {
        // Important: do not reuse an existing AudioSource on this object.
        // MainMenuSettingsCoordinator (music) may already add one AudioSource and we must keep loading SFX independent.
        _loadingSfxSource = gameObject.AddComponent<AudioSource>();

        _loadingSfxSource.playOnAwake = false;
        _loadingSfxSource.loop = false;
        _loadingSfxSource.spatialBlend = 0f;
        if (loadingSfxOutputGroup == null)
            loadingSfxOutputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _loadingSfxSource.outputAudioMixerGroup = loadingSfxOutputGroup;
    }

    private void EnsureMainMenuRootResolved()
    {
        if (mainMenuRootToDeactivate != null)
            return;

        // Best-effort auto-bind for the common hierarchy: Canvas/MainMenu
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        var mainMenu = canvas.transform.Find("MainMenu");
        if (mainMenu == null)
            return;

        mainMenuRootToDeactivate = mainMenu.gameObject;
    }

    /// <summary>Assign this to BattleBTN OnClick().</summary>
    public void StartBattle()
    {
        if (_loadRoutine != null) return;

        if (mainMenuSettingsCoordinator == null)
            mainMenuSettingsCoordinator = FindObjectOfType<MainMenuSettingsCoordinator>();

        // Important: pause first so BaseBoom1 isn't masked by the looping menu music.
        if (mainMenuSettingsCoordinator != null)
            mainMenuSettingsCoordinator.PauseMenuMusic();

        if (characterShowcase == null || weaponPicker == null || helmetShieldPicker == null)
        {
            Debug.LogWarning($"{nameof(MainMenuBattleStartController)}: assign character showcase + weapon + helmet/shield pickers.", this);
            return;
        }

        int characterIndex = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        var loadout = new BattleMenuLoadout
        {
            CharacterIndex = characterIndex,
            WeaponIndex = weaponPicker.GetWeaponIndexForCharacter(characterIndex),
            HelmetIndex = helmetShieldPicker.GetHelmetIndexForCharacter(characterIndex),
            ShieldIndex = helmetShieldPicker.GetShieldIndexForCharacter(characterIndex)
        };

        BattleLoadoutPersistence.Save(loadout);

        _loadRoutine = StartCoroutine(LoadBattleSceneAsync());
    }

    private IEnumerator LoadBattleSceneAsync()
    {
        EnsureMainMenuRootResolved();

        _prevCursorLockMode = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (loadingOverlayRoot != null)
            loadingOverlayRoot.SetActive(true);

        if (mainMenuRootToDeactivate != null)
            mainMenuRootToDeactivate.SetActive(false);

        SetProgressUi(0f);

        float loadingSfxRemaining = 0f;
        if (loadingStartClip != null)
        {
            if (_loadingSfxSource != null)
            {
                if (loadingSfxOutputGroup == null)
                    loadingSfxOutputGroup = GameAudioSettings.FindMixerGroup("SFX");
                _loadingSfxSource.outputAudioMixerGroup = loadingSfxOutputGroup;
                _loadingSfxSource.mute = false;
                _loadingSfxSource.volume = 1f;
                _loadingSfxSource.PlayOneShot(loadingStartClip, Mathf.Clamp01(loadingStartClipVolume));
            }
            loadingSfxRemaining = loadingStartClip.length;
        }
        // if loadingStartClip is null, nothing will play (fine).

        AsyncOperation op = SceneManager.LoadSceneAsync(battleSceneName, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError($"{nameof(MainMenuBattleStartController)}: scene '{battleSceneName}' not in Build Settings or name mismatch.", this);
            if (hideLoadingOverlayIfSceneFails && loadingOverlayRoot != null)
                loadingOverlayRoot.SetActive(false);
            if (mainMenuRootToDeactivate != null)
                mainMenuRootToDeactivate.SetActive(true);
            Cursor.lockState = _prevCursorLockMode;
            Cursor.visible = _prevCursorVisible;
            _loadRoutine = null;
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            float displayed = Mathf.Clamp01(op.progress / 0.9f);
            SetProgressUi(displayed);
            yield return null;
        }

        SetProgressUi(1f);

        while (loadingSfxRemaining > 0f)
        {
            loadingSfxRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        _loadRoutine = null;
    }

    private void SetProgressUi(float normalized01)
    {
        if (loadingProgressSlider != null)
            loadingProgressSlider.value = normalized01;

        if (loadingProgressText != null)
            loadingProgressText.text = $"{Mathf.RoundToInt(normalized01 * 100f)}%";
    }
}
