using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Applies persisted linear volume levels from <see cref="MainMenuSettingsKeys"/> to the project <see cref="AudioMixer"/>.
/// Mixer asset: <c>Resources/Audio/AudioMixer</c> (same asset as before; loaded at runtime if nothing registered).
/// </summary>
public static class GameAudioSettings
{
    private static AudioMixer _mixer;

    public static AudioMixer Mixer => _mixer;

    public static void RegisterMixer(AudioMixer mixer)
    {
        if (mixer != null)
            _mixer = mixer;
    }

    public static void EnsureMixerRegistered()
    {
        if (_mixer != null)
            return;
        _mixer = Resources.Load<AudioMixer>("Audio/AudioMixer");
    }

    /// <summary>Use when a component has an optional mixer reference (fixes null refs in prefabs/scenes).</summary>
    public static AudioMixer ResolveMixer(AudioMixer localReference)
    {
        if (localReference != null)
            return localReference;
        EnsureMixerRegistered();
        return _mixer;
    }

    /// <summary>
    /// Finds a mixer bus by its group name (e.g. "Music", "SFX", "MenuItems").
    /// Use when assigning <see cref="AudioSource.outputAudioMixerGroup"/> at runtime so volume sliders affect playback.
    /// </summary>
    public static AudioMixerGroup FindMixerGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return null;

        EnsureMixerRegistered();
        if (_mixer == null)
            return null;

        var groups = _mixer.FindMatchingGroups(groupName);
        if (groups == null || groups.Length == 0)
            return null;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == groupName)
                return groups[i];
        }

        return groups[0];
    }

    public static void ApplyAllVolumesFromPlayerPrefs()
    {
        EnsureMixerRegistered();
        if (_mixer == null)
            return;

        MainMenuSettingsKeys.EnsureDefaultsWritten();

        SetLinearExposed("MenuItemsVolume", MainMenuSettingsKeys.MenuItemsVolume, MainMenuSettingsKeys.DefaultMenuItemsVolume, 0f, 0.14f);
        SetLinearExposed("MasterVolume", MainMenuSettingsKeys.MasterVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
        SetLinearExposed("MusicVolume", MainMenuSettingsKeys.MusicVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
        SetLinearExposed("SFXVolume", MainMenuSettingsKeys.SfxVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
        SetLinearExposed("DialogueVolume", MainMenuSettingsKeys.DialogueVolume, MainMenuSettingsKeys.DefaultLinearVolume, 0f, 1f);
    }

    private static void SetLinearExposed(string exposedName, string prefsKey, float defaultLinear, float minSlider, float maxSlider)
    {
        float v = PlayerPrefs.GetFloat(prefsKey, defaultLinear);
        v = Mathf.Clamp(v, minSlider, maxSlider);
        v = Mathf.Max(v, 1e-6f);
        float db = 20f * Mathf.Log10(v);
        db = Mathf.Clamp(db, -80f, 0f);
        _mixer.SetFloat(exposedName, db);
    }
}
