using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Footstep SFX for enemies — same Animation Event name as player clips: <c>foot_sound</c>.
/// Must sit on the same GameObject as the <see cref="Animator"/> (Unity sends events there).
/// </summary>
public sealed class EnemyFootstepSfx : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

    [Header("Output (optional)")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Throttling")]
    [SerializeField, Min(0f)] private float minIntervalSeconds = 0.04f;

    [Header("Spatial")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;

    private AudioSource _source;
    private float _nextAllowedTime;

    private void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = spatialBlend;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;
    }

    /// <summary>Animation Event — Walk/Run clips.</summary>
    public void foot_sound() => PlayFootstep();

    public void foot_sound(AnimationEvent _) => PlayFootstep();

    public void PlayFootstep()
    {
        if (_source == null)
            return;

        if (minIntervalSeconds > 0f && Time.unscaledTime < _nextAllowedTime)
            return;

        if (footstepClips == null || footstepClips.Length == 0)
            return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null)
            return;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;

        _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        _nextAllowedTime = Time.unscaledTime + minIntervalSeconds;
    }
}
