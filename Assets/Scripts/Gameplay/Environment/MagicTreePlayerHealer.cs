using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

/// <summary>
/// Heals the player while they stay in a trigger. Plays looping VFX + sound; on full HP plays complete sound and stops particles.
/// Assign a root object that contains <see cref="ParticleSystem"/> components (children included) or leave empty and fill the array.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MagicTreePlayerHealer : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask playerLayers = ~0;

    [Header("Healing")]
    [Tooltip("HP restored per second while in the zone.")]
    [SerializeField, Min(0.01f)] private float healPerSecond = 20f;

    [Header("Particles")]
    [Tooltip("If set, all ParticleSystem on this object and under it are played while healing.")]
    [SerializeField] private Transform particleRoot;
    [Tooltip("Optional extra systems (e.g. not under particle root).")]
    [SerializeField] private ParticleSystem[] extraParticleSystems;
    [Tooltip("If true, all healing VFX are stopped and cleared at startup and only play while the player is in the trigger and healing is running.")]
    [SerializeField] private bool hideParticlesUntilHealing = true;

    [Header("Audio")]
    [Tooltip("Index 0 = Paladin, 1 = Erika (same as main menu / BattleLoadoutPersistence). Leave empty to use Healing Loop Sound fallback.")]
    [SerializeField] private AudioClip[] healingLoopSoundsByCharacter;
    [Tooltip("Optional separate complete sounds per hero. Index 0 = Paladin, 1 = Erika. Fallback: Healing Complete Sound.")]
    [SerializeField] private AudioClip[] healingCompleteSoundsByCharacter;
    [Tooltip("Fallback loop clip when the per-character slot is empty.")]
    [SerializeField] private AudioClip healingLoopSound;
    [SerializeField, Range(0f, 1f)] private float healingLoopVolume = 1f;
    [SerializeField] private AudioMixerGroup healingLoopOutputGroup;
    [Tooltip("If false, no healing loop sound is played (only optional complete sound).")]
    [SerializeField] private bool playHealingLoopSound = true;
    [Tooltip("Fallback complete clip when the per-character slot is empty.")]
    [SerializeField] private AudioClip healingCompleteSound;
    [SerializeField, Range(0f, 1f)] private float healingCompleteVolume = 1f;
    [SerializeField] private AudioMixerGroup healingCompleteOutputGroup;
    [SerializeField] private AudioSource healingAudioSource;
    [SerializeField] private Transform healingAudioFollow;

    [Header("Healing complete UI (optional)")]
    [FormerlySerializedAs("healingCompleteImageAnimator")]
    [SerializeField] private Animator completionAnimator;
    [SerializeField] private string completionTriggerName = "showhealth";

    [Header("Player energy (optional)")]
    [Tooltip("Usually the player character Animator. If empty, first Animator under Player tag is used.")]
    [SerializeField] private Animator getEnergyAnimator;
    [SerializeField] private string getEnergyTriggerName = "GetEnergy";

    private readonly List<ParticleSystem> _particles = new List<ParticleSystem>(16);
    private Health _playerHealth;
    private bool _playerInZone;
    private Coroutine _healRoutine;
    private AudioSource _loopAudio;
    private AudioSource _completeShotAudio;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        CollectParticleSystems();

        if (hideParticlesUntilHealing)
            ApplyParticlesHiddenAtStartup();

        if (HasAnyLoopClip() || HasAnyCompleteClip())
        {
            _loopAudio = healingAudioSource != null ? healingAudioSource : GetComponent<AudioSource>();
            if (_loopAudio == null)
                _loopAudio = gameObject.AddComponent<AudioSource>();
            _loopAudio.playOnAwake = false;
            if (healingLoopOutputGroup != null && HasAnyLoopClip())
                _loopAudio.outputAudioMixerGroup = healingLoopOutputGroup;
            if (!playHealingLoopSound && _loopAudio.isPlaying)
                _loopAudio.Stop();
        }

        if (HasAnyCompleteClip())
        {
            _completeShotAudio = gameObject.AddComponent<AudioSource>();
            _completeShotAudio.playOnAwake = false;
            _completeShotAudio.loop = false;
            if (_loopAudio != null)
                _completeShotAudio.spatialBlend = _loopAudio.spatialBlend;
            if (healingCompleteOutputGroup != null)
                _completeShotAudio.outputAudioMixerGroup = healingCompleteOutputGroup;
        }

    }

    private void Start()
    {
        // ParticleSystem may run Play On Awake after Awake; enforce hidden state again.
        if (hideParticlesUntilHealing)
            ApplyParticlesHiddenAtStartup();
    }

    private void CollectParticleSystems()
    {
        _particles.Clear();
        if (particleRoot != null)
        {
            _particles.AddRange(particleRoot.GetComponentsInChildren<ParticleSystem>(true));
        }
        if (extraParticleSystems != null)
        {
            for (int i = 0; i < extraParticleSystems.Length; i++)
            {
                if (extraParticleSystems[i] != null && !_particles.Contains(extraParticleSystems[i]))
                    _particles.Add(extraParticleSystems[i]);
            }
        }
    }

    private void ApplyParticlesHiddenAtStartup()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            ParticleSystem ps = _particles[i];
            if (ps == null)
                continue;

            var main = ps.main;
            main.playOnAwake = false;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerHealth = FindHealthInHierarchy(other.transform);
        _playerInZone = true;

        if (_playerHealth == null)
            return;

        if (_healRoutine != null)
            StopCoroutine(_healRoutine);
        _healRoutine = StartCoroutine(HealingRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerInZone = false;

        if (_healRoutine != null)
        {
            StopCoroutine(_healRoutine);
            _healRoutine = null;
        }

        StopHealingLoopSound();
        StopHealingParticles();
    }

    private IEnumerator HealingRoutine()
    {
        if (_playerHealth == null)
            yield break;

        if (_playerHealth.CurrentHealth >= _playerHealth.MaxHealth - 0.001f)
            yield break;

        PlayHealingParticles();
        PlayHealingLoopSound();

        while (_playerInZone && _playerHealth != null && _playerHealth.isActiveAndEnabled)
        {
            if (_playerHealth.CurrentHealth >= _playerHealth.MaxHealth - 0.001f)
                break;

            _playerHealth.Heal(healPerSecond * Time.deltaTime);
            yield return null;
        }

        StopHealingLoopSound();
        StopHealingParticles();

        if (_playerInZone && _playerHealth != null && _playerHealth.CurrentHealth >= _playerHealth.MaxHealth - 0.001f)
        {
            PlayHealingCompleteSound();
            PlayHealingCompleteImageAnimation();
        }

        _healRoutine = null;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;
        if (((1 << other.gameObject.layer) & playerLayers.value) == 0)
            return false;
        if (other.CompareTag(playerTag))
            return true;

        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag))
                return true;
            t = t.parent;
        }
        return false;
    }

    private static Health FindHealthInHierarchy(Transform start)
    {
        Transform t = start;
        while (t != null)
        {
            Health h = t.GetComponent<Health>();
            if (h != null && h.isActiveAndEnabled)
                return h;
            t = t.parent;
        }
        return null;
    }

    private void PlayHealingParticles()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            ParticleSystem ps = _particles[i];
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }

    private void StopHealingParticles()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            ParticleSystem ps = _particles[i];
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void PlayHealingLoopSound()
    {
        if (!playHealingLoopSound)
            return;

        AudioClip loop = ResolveLoopClip();
        if (loop == null || _loopAudio == null)
            return;

        Vector3 pos = healingAudioFollow != null ? healingAudioFollow.position : transform.position;
        _loopAudio.transform.position = pos;

        if (healingLoopOutputGroup != null)
            _loopAudio.outputAudioMixerGroup = healingLoopOutputGroup;

        _loopAudio.clip = loop;
        _loopAudio.loop = false;
        _loopAudio.volume = Mathf.Clamp01(healingLoopVolume);
        _loopAudio.Play();
    }

    private void StopHealingLoopSound()
    {
        if (_loopAudio == null)
            return;
        if (_loopAudio.isPlaying)
            _loopAudio.Stop();
        _loopAudio.clip = null;
    }

    private void PlayHealingCompleteSound()
    {
        AudioClip clip = ResolveCompleteClip();
        if (clip == null)
            return;

        Vector3 pos = healingAudioFollow != null ? healingAudioFollow.position : transform.position;

        if (_completeShotAudio != null)
        {
            _completeShotAudio.transform.position = pos;
            if (healingCompleteOutputGroup != null)
                _completeShotAudio.outputAudioMixerGroup = healingCompleteOutputGroup;
            _completeShotAudio.PlayOneShot(clip, Mathf.Clamp01(healingCompleteVolume));
        }
        else
            AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(healingCompleteVolume));
    }

    private void PlayHealingCompleteImageAnimation()
    {
        FireCompletionAnimatorTrigger();
    }

    private void FireCompletionAnimatorTrigger()
    {
        if (completionAnimator == null || string.IsNullOrEmpty(completionTriggerName))
            return;

        completionAnimator.enabled = true;
        completionAnimator.SetTrigger(completionTriggerName);
    }

    private void FireGetEnergyTrigger()
    {
        MagicTreePlayerGetEnergyTrigger.Fire(getEnergyAnimator, getEnergyTriggerName, playerTag);
    }

    private static int GetSelectedCharacterIndexForBattle()
    {
        if (BattleLoadoutPersistence.TryLoad(out var lo))
            return Mathf.Clamp(lo.CharacterIndex, 0, 1);
        return 0;
    }

    private bool HasAnyLoopClip()
    {
        if (healingLoopSound != null)
            return true;
        if (healingLoopSoundsByCharacter == null)
            return false;
        for (int i = 0; i < healingLoopSoundsByCharacter.Length; i++)
        {
            if (healingLoopSoundsByCharacter[i] != null)
                return true;
        }
        return false;
    }

    private bool HasAnyCompleteClip()
    {
        if (healingCompleteSound != null)
            return true;
        if (healingCompleteSoundsByCharacter == null)
            return false;
        for (int i = 0; i < healingCompleteSoundsByCharacter.Length; i++)
        {
            if (healingCompleteSoundsByCharacter[i] != null)
                return true;
        }
        return false;
    }

    private AudioClip ResolveLoopClip()
    {
        int idx = GetSelectedCharacterIndexForBattle();
        if (healingLoopSoundsByCharacter != null && idx >= 0 && idx < healingLoopSoundsByCharacter.Length && healingLoopSoundsByCharacter[idx] != null)
            return healingLoopSoundsByCharacter[idx];
        return healingLoopSound;
    }

    private AudioClip ResolveCompleteClip()
    {
        int idx = GetSelectedCharacterIndexForBattle();
        if (healingCompleteSoundsByCharacter != null && idx >= 0 && idx < healingCompleteSoundsByCharacter.Length && healingCompleteSoundsByCharacter[idx] != null)
            return healingCompleteSoundsByCharacter[idx];
        return healingCompleteSound;
    }
}
