using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Applies persisted audio levels at MainMenu startup (before Settings UI is enabled) and saves all settings when Apply is pressed.
/// </summary>
public class MainMenuSettingsCoordinator : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;

    [Header("Menu music")]
    [SerializeField] private bool playMenuMusicOnStart = true;
    [SerializeField] private bool loopMenuMusic = true;
    [SerializeField] private AudioClip menuMusicClip;
    [SerializeField] private AudioMixerGroup menuMusicOutputGroup;

    private AudioSource _menuMusicSource;

    private void Start()
    {
        MainMenuSettingsKeys.EnsureDefaultsWritten();
        ApplyAllAudioFromPrefs();

        if (playMenuMusicOnStart)
            EnsureMenuMusicPlaying();
    }

    /// <summary>Wire Apply button OnClick to this.</summary>
    public void SaveAllSettings()
    {
        foreach (var s in FindObjectsByType<MenuItemsVolumeSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.SaveToPlayerPrefs();
        foreach (var s in FindObjectsByType<MixerVolumeSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.SaveToPlayerPrefs();
        foreach (var s in FindObjectsByType<FloatSettingSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.SaveToPlayerPrefs();
        PlayerPrefs.Save();
    }

    private void ApplyAllAudioFromPrefs()
    {
        if (mixer == null) return;

        SetMixerFromLinearKey("MenuItemsVolume", MainMenuSettingsKeys.MenuItemsVolume, MainMenuSettingsKeys.DefaultMenuItemsVolume, 0f, 0.14f);
        SetMixerFromLinearKey("MasterVolume", MainMenuSettingsKeys.MasterVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
        SetMixerFromLinearKey("MusicVolume", MainMenuSettingsKeys.MusicVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
        SetMixerFromLinearKey("SFXVolume", MainMenuSettingsKeys.SfxVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
        SetMixerFromLinearKey("DialogueVolume", MainMenuSettingsKeys.DialogueVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
    }

    private void EnsureMenuMusicPlaying()
    {
        if (menuMusicClip == null)
        {
            Debug.LogWarning($"{nameof(MainMenuSettingsCoordinator)}: menuMusicClip is not assigned.", this);
            return;
        }

        if (_menuMusicSource == null)
        {
            _menuMusicSource = GetComponent<AudioSource>();
            if (_menuMusicSource == null)
                _menuMusicSource = gameObject.AddComponent<AudioSource>();
        }

        _menuMusicSource.clip = menuMusicClip;
        _menuMusicSource.loop = loopMenuMusic;
        _menuMusicSource.playOnAwake = false;
        _menuMusicSource.outputAudioMixerGroup = menuMusicOutputGroup;
        _menuMusicSource.volume = 1f;

        if (!_menuMusicSource.isPlaying)
            _menuMusicSource.Play();
    }

    /// <summary>
    /// Pauses menu music so one-shot loading SFX (e.g. BaseBoom1) can be heard clearly.
    /// </summary>
    public void PauseMenuMusic()
    {
        if (_menuMusicSource == null)
        {
            // If EnsureMenuMusicPlaying never ran (rare), try once.
            EnsureMenuMusicPlaying();
        }

        if (_menuMusicSource == null) return;

        // Use Stop + mute to avoid audible "tail" buffered from the loop.
        _menuMusicSource.mute = true;
        _menuMusicSource.volume = 0f;
        _menuMusicSource.Pause();
        _menuMusicSource.Stop();
    }

    /// <summary>
    /// Resumes menu music (useful if user returns to MainMenu).
    /// </summary>
    public void ResumeMenuMusic()
    {
        if (_menuMusicSource == null)
        {
            EnsureMenuMusicPlaying();
        }

        if (_menuMusicSource == null) return;

        _menuMusicSource.mute = false;
        _menuMusicSource.volume = 1f;

        if (!_menuMusicSource.isPlaying)
            _menuMusicSource.Play();
    }

    private void SetMixerFromLinearKey(string exposedName, string key, float defaultLinear, float minSlider, float maxSlider)
    {
        float v = PlayerPrefs.GetFloat(key, defaultLinear);
        v = Mathf.Clamp(v, minSlider, maxSlider);
        v = Mathf.Max(v, 1e-6f);
        float db = 20f * Mathf.Log10(v);
        db = Mathf.Clamp(db, -80f, 0f);
        mixer.SetFloat(exposedName, db);
    }
}
