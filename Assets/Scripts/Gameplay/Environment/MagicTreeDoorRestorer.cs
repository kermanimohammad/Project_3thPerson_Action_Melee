using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Magic tree: player stays in trigger to repair doors.
/// The shared door-health <see cref="Slider"/> (same as <see cref="DoorAggregateHealthSlider"/>) animates from current combined HP to full during repair.
/// </summary>
public class MagicTreeDoorRestorer : MonoBehaviour
{
    public bool IsRepairCharging => chargeRoutine != null || restoring;

    [System.Serializable]
    public class DoorSpawn
    {
        public Transform spawn;
        public GameObject doorPrefab;
    }

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask playerLayers = ~0;

    [Header("Repair timing & door health UI")]
    [Tooltip("Same Slider as DoorAggregateHealthSlider: shows combined door HP; during repair it animates from current HP to full.")]
    [SerializeField] private Slider doorHealthSlider;
    [Tooltip("Match DoorAggregateHealthSlider.fullWhenNoBreakables when computing start fill.")]
    [SerializeField] private bool fullWhenNoBreakables = true;
    [Tooltip("Time to restore when all tracked doors have lost 100% of their combined max HP.")]
    [SerializeField] private float chargeSeconds = 3f;
    [SerializeField] private float minChargeSeconds = 0.05f;
    [Tooltip("Only these doors count (tag on self or parent). Empty = all DoorBreakable in scene.")]
    [SerializeField] private string onlyDoorsWithTag = "";

    [Header("Door restore")]
    [SerializeField] private List<DoorSpawn> doorSpawns = new List<DoorSpawn>();
    [SerializeField] private bool destroyExistingBreakableDoors = true;

    [Header("Audio")]
    [SerializeField] private AudioClip repairSound;
    [SerializeField, Range(0f, 1f)] private float repairSoundVolume = 1f;
    [SerializeField] private bool loopRepairSound = true;
    [SerializeField] private AudioSource repairAudioSource;
    [SerializeField] private AudioMixerGroup repairSoundOutputGroup;

    [Header("Repair complete")]
    [SerializeField] private AudioClip repairCompleteSound;
    [SerializeField, Range(0f, 1f)] private float repairCompleteSoundVolume = 1f;
    [SerializeField] private AudioMixerGroup repairCompleteSoundOutputGroup;
    [SerializeField] private Transform repairCompleteAudioFollow;
    [Tooltip("All listed systems play each time repair completes (positions stay as placed in the scene).")]
    [SerializeField] private ParticleSystem[] repairCompleteParticles;

    [Header("Repair complete UI (optional)")]
    [SerializeField] private Animator completionAnimator;
    [SerializeField] private string completionTriggerName = "showdoors";

    [Header("Player energy (optional)")]
    [Tooltip("Usually the player character Animator. If empty, first Animator under Player tag is used.")]
    [SerializeField] private Animator getEnergyAnimator;
    [SerializeField] private string getEnergyTriggerName = "GetEnergy";

    private Coroutine chargeRoutine;
    private bool restoring;
    private AudioSource _repairAudio;
    /// <summary>Separate source so <see cref="StopRepairSound"/> does not cancel <see cref="PlayOneShot"/> (Unity Stop() kills one-shots on the same AudioSource).</summary>
    private AudioSource _repairCompleteShotAudio;

    private readonly List<DoorBreakable> _doorScratch = new List<DoorBreakable>(16);

    private void Awake()
    {
        if (repairSound != null || repairCompleteSound != null)
        {
            _repairAudio = repairAudioSource != null ? repairAudioSource : GetComponent<AudioSource>();
            if (_repairAudio == null)
                _repairAudio = gameObject.AddComponent<AudioSource>();
            _repairAudio.playOnAwake = false;
            if (repairSoundOutputGroup != null && repairSound != null)
                _repairAudio.outputAudioMixerGroup = repairSoundOutputGroup;
        }

        if (repairCompleteSound != null)
        {
            _repairCompleteShotAudio = gameObject.AddComponent<AudioSource>();
            _repairCompleteShotAudio.playOnAwake = false;
            _repairCompleteShotAudio.loop = false;
            _repairCompleteShotAudio.spatialBlend = _repairAudio != null ? _repairAudio.spatialBlend : 0f;
            if (repairCompleteSoundOutputGroup != null)
                _repairCompleteShotAudio.outputAudioMixerGroup = repairCompleteSoundOutputGroup;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;
        StartCharging();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;
        StopCharging();
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

    private void StartCharging()
    {
        if (restoring)
            return;

        PlayRepairSound();

        if (chargeRoutine != null)
            StopCoroutine(chargeRoutine);

        chargeRoutine = StartCoroutine(ChargeRoutine());
    }

    private void StopCharging()
    {
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        if (doorHealthSlider != null)
            doorHealthSlider.value = GetTrackedAggregateHealth01();

        StopRepairSound();
    }

    private IEnumerator ChargeRoutine()
    {
        float duration = ComputeRepairChargeDuration();
        float elapsed = 0f;
        float startFill = GetTrackedAggregateHealth01();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / duration);
            if (doorHealthSlider != null)
                doorHealthSlider.value = Mathf.Lerp(startFill, 1f, a);
            yield return null;
        }

        chargeRoutine = null;
        restoring = true;

        try
        {
            RunRepairComplete();
        }
        catch
        {
            StopRepairSound();
            throw;
        }
        finally
        {
            restoring = false;
            if (doorHealthSlider != null)
                doorHealthSlider.value = 1f;
            // Do not call StopRepairSound() here — it runs after PlayRepairCompleteFeedback and would stop PlayOneShot on the loop AudioSource.
        }
    }

    /// <summary>Destroy tracked breakables, spawn restored doors, VFX/sfx. Same door filter as charge time.</summary>
    private void RunRepairComplete()
    {
        if (destroyExistingBreakableDoors)
        {
            CollectTrackedDoors(_doorScratch);
            for (int i = 0; i < _doorScratch.Count; i++)
            {
                DoorBreakable d = _doorScratch[i];
                if (d != null)
                    Destroy(d.gameObject);
            }
            _doorScratch.Clear();
        }

        for (int i = 0; i < doorSpawns.Count; i++)
        {
            DoorSpawn ds = doorSpawns[i];
            if (ds == null || ds.spawn == null || ds.doorPrefab == null)
                continue;
            Instantiate(ds.doorPrefab, ds.spawn.position, ds.spawn.rotation);
        }

        StopRepairSound();
        PlayRepairCompleteFeedback();
    }

    private float GetTrackedAggregateHealth01()
    {
        GetTrackedDoorHealthTotals(out float totalMax, out float totalMissing);
        if (totalMax <= 0f)
            return fullWhenNoBreakables ? 1f : 0f;
        float totalCurrent = totalMax - totalMissing;
        return Mathf.Clamp01(totalCurrent / totalMax);
    }

    private float ComputeRepairChargeDuration()
    {
        GetTrackedDoorHealthTotals(out float totalMax, out float totalMissing);

        float baseTime = Mathf.Max(0.01f, chargeSeconds);
        float floor = Mathf.Max(0f, minChargeSeconds);

        if (totalMax <= 0f)
            return Mathf.Max(floor, baseTime);

        float ratio = Mathf.Clamp01(totalMissing / totalMax);
        if (ratio <= 0f)
            return floor;

        return Mathf.Max(floor, baseTime * ratio);
    }

    private void GetTrackedDoorHealthTotals(out float totalMax, out float totalMissing)
    {
        totalMax = 0f;
        totalMissing = 0f;

        CollectTrackedDoors(_doorScratch);
        for (int i = 0; i < _doorScratch.Count; i++)
        {
            DoorBreakable d = _doorScratch[i];
            if (d == null)
                continue;
            float maxH = d.DoorMaxHealth;
            if (maxH <= 0f)
                continue;
            totalMax += maxH;
            totalMissing += d.DoorMissingHealth;
        }
        _doorScratch.Clear();
    }

    private void CollectTrackedDoors(List<DoorBreakable> into)
    {
        into.Clear();
        DoorBreakable[] doors = Object.FindObjectsByType<DoorBreakable>(FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            DoorBreakable d = doors[i];
            if (d == null || !d.isActiveAndEnabled)
                continue;
            if (!DoorMatchesFilter(d))
                continue;
            into.Add(d);
        }
    }

    private bool DoorMatchesFilter(DoorBreakable door)
    {
        if (string.IsNullOrEmpty(onlyDoorsWithTag))
            return true;

        return IsTaggedInParentChain(door.transform, onlyDoorsWithTag);
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

    private void PlayRepairSound()
    {
        if (repairSound == null || _repairAudio == null)
            return;

        if (repairSoundOutputGroup != null)
            _repairAudio.outputAudioMixerGroup = repairSoundOutputGroup;

        _repairAudio.clip = repairSound;
        _repairAudio.loop = loopRepairSound;
        _repairAudio.volume = Mathf.Clamp01(repairSoundVolume);
        _repairAudio.Play();
    }

    private void StopRepairSound()
    {
        if (_repairAudio == null)
            return;
        if (_repairAudio.isPlaying)
            _repairAudio.Stop();
        _repairAudio.clip = null;
    }

    private void PlayRepairCompleteFeedback()
    {
        PlayRepairCompleteParticles();

        if (repairCompleteSound != null)
        {
            Vector3 audioPos = repairCompleteAudioFollow != null ? repairCompleteAudioFollow.position : transform.position;

            if (_repairCompleteShotAudio != null)
            {
                _repairCompleteShotAudio.transform.position = audioPos;
                if (repairCompleteSoundOutputGroup != null)
                    _repairCompleteShotAudio.outputAudioMixerGroup = repairCompleteSoundOutputGroup;
                _repairCompleteShotAudio.PlayOneShot(repairCompleteSound, Mathf.Clamp01(repairCompleteSoundVolume));
            }
            else
                AudioSource.PlayClipAtPoint(repairCompleteSound, audioPos, Mathf.Clamp01(repairCompleteSoundVolume));
        }

        FireCompletionAnimatorTrigger();
        FireGetEnergyTrigger();
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

    private void PlayRepairCompleteParticles()
    {
        if (repairCompleteParticles == null || repairCompleteParticles.Length == 0)
            return;

        for (int i = 0; i < repairCompleteParticles.Length; i++)
        {
            ParticleSystem ps = repairCompleteParticles[i];
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }
}
