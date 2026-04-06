using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

/// <summary>
/// Tree interaction for XP/Level: when the player enters range and XP is full,
/// drains XP to zero over a short time (showing slider emptying), then levels up and increases max XP.
/// Optional particle visible only while XP is draining; completion sound after level applies.
/// Requires a trigger collider on this object (or a child) marking the interaction range.
/// </summary>
[DisallowMultipleComponent]
public sealed class MagicTreeXpLevelUp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleXpSlider battleXp;

    [Header("Interaction")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0.1f)] private float drainSeconds = 3f;

    [Header("Drain VFX (optional)")]
    [Tooltip("Hidden until XP drain starts; hidden again when drain reaches zero (before level increments).")]
    [SerializeField] private ParticleSystem levelUpParticles;

    [Header("Level-up audio (optional)")]
    [SerializeField] private AudioClip levelUpCompleteSound;
    [SerializeField, Range(0f, 1f)] private float levelUpCompleteVolume = 1f;
    [SerializeField] private AudioMixerGroup levelUpCompleteOutputGroup;
    [SerializeField] private AudioSource levelUpAudioSource;

    [Header("Level-up UI (optional)")]
    [FormerlySerializedAs("levelUpCompleteImageAnimator")]
    [SerializeField] private Animator completionAnimator;
    [SerializeField] private string completionTriggerName = "showlevel";

    [Header("Player energy (optional)")]
    [Tooltip("Usually the player character Animator. If empty, first Animator under Player tag is used.")]
    [SerializeField] private Animator getEnergyAnimator;
    [SerializeField] private string getEnergyTriggerName = "GetEnergy";

    private void Awake()
    {
        if (battleXp == null)
            battleXp = FindFirstObjectByType<BattleXpSlider>();

        HideDrainParticles();

        if (levelUpCompleteSound != null && levelUpAudioSource == null)
        {
            levelUpAudioSource = GetComponent<AudioSource>();
            if (levelUpAudioSource == null)
                levelUpAudioSource = gameObject.AddComponent<AudioSource>();
            levelUpAudioSource.playOnAwake = false;
            levelUpAudioSource.loop = false;
            if (levelUpCompleteOutputGroup != null)
                levelUpAudioSource.outputAudioMixerGroup = levelUpCompleteOutputGroup;
        }
    }

    private void Start()
    {
        HideDrainParticles();
    }

    private void HideDrainParticles()
    {
        if (levelUpParticles == null)
            return;

        var main = levelUpParticles.main;
        main.playOnAwake = false;

        levelUpParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        levelUpParticles.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        TryStartLevelUp();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        TryStartLevelUp();
    }

    private void TryStartLevelUp()
    {
        if (battleXp == null)
            return;

        battleXp.StartTreeLevelUp(drainSeconds, OnLevelUpCompleted, OnDrainStarted, OnDrainEnded);
    }

    private void OnDrainStarted()
    {
        if (levelUpParticles == null)
            return;

        levelUpParticles.gameObject.SetActive(true);
        levelUpParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        levelUpParticles.Play();
    }

    private void OnDrainEnded()
    {
        HideDrainParticles();
    }

    private void OnLevelUpCompleted()
    {
        if (levelUpCompleteSound != null && levelUpAudioSource != null)
            levelUpAudioSource.PlayOneShot(levelUpCompleteSound, levelUpCompleteVolume);

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
}
