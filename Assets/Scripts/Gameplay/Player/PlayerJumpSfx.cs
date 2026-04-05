using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Jump SFX player. Intended to be called by Animation Events.
/// Event name expected by user clips: "jump".
/// Attach to Player root.
/// </summary>
public sealed class PlayerJumpSfx : MonoBehaviour
{
    [Header("Clip")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Output (optional)")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Throttling")]
    [Tooltip("Prevents multiple jump events firing too close together.")]
    [SerializeField, Min(0f)] private float minIntervalSeconds = 0.05f;

    private AudioSource _source;
    private float _nextAllowedTime;

    private void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;
    }

    /// <summary>
    /// Animation Event receiver (Jump clips).
    /// Add an event named exactly "jump".
    /// </summary>
    public void jump()
    {
        PlayJump();
    }

    /// <summary>Also supports AnimationEvent signature.</summary>
    public void jump(AnimationEvent _)
    {
        PlayJump();
    }

    private void PlayJump()
    {
        if (_source == null)
            return;

        if (minIntervalSeconds > 0f && Time.unscaledTime < _nextAllowedTime)
            return;

        if (jumpClip == null)
            return;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;

        _source.PlayOneShot(jumpClip, Mathf.Clamp01(volume));
        _nextAllowedTime = Time.unscaledTime + minIntervalSeconds;
    }
}

