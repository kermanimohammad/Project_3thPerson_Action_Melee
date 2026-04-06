using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Footstep SFX player. Intended to be called by Animation Events.
/// Event name expected by user clips: "foot_sound".
/// Attach to Player root.
/// </summary>
public sealed class PlayerFootstepSfx : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    [Header("Output (optional)")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Gating")]
    [Tooltip("If assigned, footsteps only play when grounded and planar speed exceeds threshold.")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField, Min(0f)] private float minPlanarSpeedToPlay = 0.15f;

    [Header("Throttling")]
    [Tooltip("Prevents double events in the same frame / tiny spacing.")]
    [SerializeField, Min(0f)] private float minIntervalSeconds = 0.04f;

    private AudioSource _source;
    private float _nextAllowedTime;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;
    }

    /// <summary>
    /// Animation Event receiver (Walk/Run clips).
    /// Add an event named exactly "foot_sound".
    /// </summary>
    public void foot_sound()
    {
        PlayFootstep();
    }

    /// <summary>Also supports AnimationEvent signature (if you ever switch).</summary>
    public void foot_sound(AnimationEvent _)
    {
        PlayFootstep();
    }

    public void PlayFootstep()
    {
        if (_source == null)
            return;

        if (motor != null)
        {
            if (!motor.IsGrounded)
                return;
            if (motor.PlanarVelocity.magnitude < minPlanarSpeedToPlay)
                return;
        }

        if (minIntervalSeconds > 0f && Time.unscaledTime < _nextAllowedTime)
            return;

        if (footstepClips == null || footstepClips.Length == 0)
            return;

        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null)
            return;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;

        _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        _nextAllowedTime = Time.unscaledTime + minIntervalSeconds;
    }
}

