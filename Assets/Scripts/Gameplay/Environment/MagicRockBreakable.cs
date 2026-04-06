using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Breakable prop with health that can receive damage from the same pipeline as doors (AOE → DamageService).
/// Attach this to the prop root that should receive damage.
/// </summary>
[DisallowMultipleComponent]
public sealed class MagicRockBreakable : MonoBehaviour, IDamageableWithSource
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 5f;
    [SerializeField] private float currentHealth = 5f;

    [Header("Damage tuning")]
    [Tooltip("Multiplier applied to normal Attack damage when hitting THIS prop.")]
    [SerializeField] private float normalAttackDamageMultiplier = 1f;
    [Tooltip("Multiplier applied to Special attack damage when hitting THIS prop.")]
    [SerializeField] private float specialAttackDamageMultiplier = 1f;

    [Header("Break behaviour")]
    [Tooltip("If false, the rock root stays active; only the pieces are released.")]
    [SerializeField] private bool disableRootOnBreak = false;
    [Tooltip("If true, the GameObject is destroyed when health reaches 0 (overrides disableRootOnBreak).")]
    [SerializeField] private bool destroyRootOnBreak = false;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 1f;
    [SerializeField] private float hitSoundCooldownSeconds = 0.08f;
    [SerializeField] private AudioClip breakSound;
    [SerializeField, Range(0f, 1f)] private float breakSoundVolume = 1f;
    [Tooltip("Optional: assign to route prop SFX through your mixer.")]
    [SerializeField] private AudioMixerGroup sfxOutputGroup;
    [Tooltip("Optional: use an existing AudioSource on this object; otherwise one is added if any clip is set.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Fractured pieces")]
    [Tooltip("Rigidbodies that make up the rock. If empty, all child rigidbodies (excluding self) are used.")]
    [SerializeField] private List<Rigidbody> pieces = new List<Rigidbody>();
    [SerializeField] private float impulseStrength = 8f;
    [SerializeField] private float upwardImpulse = 0.75f;
    [SerializeField] private float randomTorque = 6f;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    [Header("Post-break cleanup")]
    [Tooltip("After breaking, wait this many seconds then disable Rigidbody and MeshCollider components on pieces.")]
    [SerializeField] private float disablePhysicsAfterSeconds = 1.0f;
    [Tooltip("Rigidbody cannot be disabled; this instead freezes it (kinematic, no gravity, zero velocities, no collisions).")]
    [SerializeField] private bool freezePieceRigidbodies = true;
    [SerializeField] private bool disablePieceMeshColliders = true;

    private bool _broken;
    private AudioSource _audio;
    private float _nextHitSoundTime;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsBroken => _broken;

    private void Awake()
    {
        if (maxHealth <= 0f)
            maxHealth = 1f;

        if (currentHealth <= 0f || currentHealth > maxHealth)
            currentHealth = maxHealth;

        if (pieces == null)
            pieces = new List<Rigidbody>();

        if (pieces.Count == 0)
        {
            var rbs = GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] == null)
                    continue;
                if (rbs[i].gameObject == gameObject)
                    continue;
                pieces.Add(rbs[i]);
            }
        }

        if (hitSound != null || breakSound != null)
        {
            _audio = audioSource != null ? audioSource : GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            if (sfxOutputGroup != null)
                _audio.outputAudioMixerGroup = sfxOutputGroup;
        }
    }

    public void TakeDamage(float amount) => TakeDamage(null, amount);

    public void TakeDamage(GameObject source, float amount)
    {
        if (_broken)
            return;

        float finalAmount = amount * ResolveMultiplier(source);
        if (finalAmount <= 0f)
            return;

        currentHealth = Mathf.Clamp(currentHealth - finalAmount, 0f, maxHealth);

        if (currentHealth <= 0f)
        {
            Vector3 dir = ResolveBreakDirection(source);
            Break(dir);
        }
        else
        {
            PlayHitSound();
        }
    }

    private float ResolveMultiplier(GameObject source)
    {
        if (source == null)
            return normalAttackDamageMultiplier;

        Animator a = source.GetComponent<Animator>();
        if (a == null)
            a = source.GetComponentInChildren<Animator>();

        if (a != null)
        {
            const int layer = 0;
            var s = a.GetCurrentAnimatorStateInfo(layer);
            if (s.IsName("SpacialAttack"))
                return specialAttackDamageMultiplier;

            if (a.IsInTransition(layer))
            {
                var n = a.GetNextAnimatorStateInfo(layer);
                if (n.IsName("SpacialAttack"))
                    return specialAttackDamageMultiplier;
            }
        }

        return normalAttackDamageMultiplier;
    }

    private Vector3 ResolveBreakDirection(GameObject source)
    {
        if (source == null)
            return transform.forward;

        Vector3 d = transform.position - source.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f)
            return transform.forward;
        return d.normalized;
    }

    private void Break(Vector3 direction)
    {
        _broken = true;

        PlayBreakSound();

        if (pieces != null && pieces.Count > 0)
            ReleasePieces(direction);

        if (disablePhysicsAfterSeconds > 0f && (freezePieceRigidbodies || disablePieceMeshColliders))
            StartCoroutine(DisablePiecePhysicsAfterDelay());

        if (destroyRootOnBreak)
        {
            Destroy(gameObject);
        }
        else if (disableRootOnBreak)
        {
            gameObject.SetActive(false);
        }
    }

    private void ReleasePieces(Vector3 direction)
    {
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        dir = dir.normalized;

        Vector3 impulse = dir * impulseStrength + Vector3.up * upwardImpulse;

        for (int i = 0; i < pieces.Count; i++)
        {
            Rigidbody rb = pieces[i];
            if (rb == null)
                continue;

            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddForce(impulse, forceMode);

            if (randomTorque > 0f)
            {
                Vector3 t = new Vector3(
                    Random.Range(-randomTorque, randomTorque),
                    Random.Range(-randomTorque, randomTorque),
                    Random.Range(-randomTorque, randomTorque)
                );
                rb.AddTorque(t, forceMode);
            }
        }
    }

    private System.Collections.IEnumerator DisablePiecePhysicsAfterDelay()
    {
        yield return new WaitForSeconds(disablePhysicsAfterSeconds);

        for (int i = 0; i < pieces.Count; i++)
        {
            Rigidbody rb = pieces[i];
            if (rb == null)
                continue;

            if (freezePieceRigidbodies)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            if (disablePieceMeshColliders)
            {
                var mcs = rb.GetComponents<MeshCollider>();
                for (int c = 0; c < mcs.Length; c++)
                {
                    if (mcs[c] != null)
                        mcs[c].enabled = false;
                }
            }
        }
    }

    private void PlayHitSound()
    {
        if (hitSound == null || _audio == null)
            return;
        if (Time.time < _nextHitSoundTime)
            return;
        _nextHitSoundTime = Time.time + Mathf.Max(0f, hitSoundCooldownSeconds);
        _audio.PlayOneShot(hitSound, Mathf.Clamp01(hitSoundVolume));
    }

    private void PlayBreakSound()
    {
        if (breakSound == null || _audio == null)
            return;
        _audio.PlayOneShot(breakSound, Mathf.Clamp01(breakSoundVolume));
    }
}

