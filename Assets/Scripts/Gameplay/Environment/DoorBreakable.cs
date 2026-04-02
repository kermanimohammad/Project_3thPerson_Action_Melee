using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breakable door made of pre-fractured rigidbody pieces.
/// Pieces should have Rigidbody + Collider (MeshCollider is fine) and start as isKinematic=true.
/// Damage is expected to come from AOE via DamageService.
/// </summary>
public class DoorBreakable : MonoBehaviour, IDamageableWithSource
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 1f;
    [SerializeField] private float currentHealth = 1f;

    [Header("Damage tuning (doors only)")]
    [Tooltip("Multiplier applied to normal Attack damage when hitting THIS door.")]
    [SerializeField] private float normalAttackDamageMultiplier = 1f;
    [Tooltip("Multiplier applied to Special attack damage when hitting THIS door.")]
    [SerializeField] private float specialAttackDamageMultiplier = 1f;

    [Header("Pieces (optional)")]
    [Tooltip("If empty, all child rigidbodies (excluding self) are used as door pieces.")]
    [SerializeField] private List<Rigidbody> pieces = new List<Rigidbody>();

    [Header("Break force")]
    [SerializeField] private float impulseStrength = 8f;
    [SerializeField] private float upwardImpulse = 0.75f;
    [SerializeField] private float randomTorque = 6f;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    [Header("Post-break cleanup")]
    [Tooltip("After breaking, wait this many seconds then disable collisions and gravity on pieces.")]
    [SerializeField] private float disablePhysicsAfterSeconds = 1.0f;
    [SerializeField] private bool disablePieceColliders = true;
    [SerializeField] private bool disablePieceGravity = true;
    [Tooltip("If true, also makes pieces kinematic after the delay (freezes them in place).")]
    [SerializeField] private bool freezePiecesKinematicAfterDelay = true;

    private bool broken;

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
    }

    public void TakeDamage(float amount)
    {
        // Fall back when damage source isn't provided (direction = forward).
        TakeDamage(null, amount);
    }

    public void TakeDamage(GameObject source, float amount)
    {
        if (broken)
            return;

        float finalAmount = amount * ResolveMultiplier(source);

        currentHealth -= finalAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (currentHealth <= 0f)
        {
            Vector3 dir = ResolveBreakDirection(source);
            Break(dir);
        }
    }

    private float ResolveMultiplier(GameObject source)
    {
        if (source == null)
            return normalAttackDamageMultiplier;

        // Detect special attack by checking the attacker's current/next Animator state.
        // This keeps door tuning in Unity without changing the whole damage pipeline.
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
        broken = true;

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

        if (disablePhysicsAfterSeconds > 0f && (disablePieceColliders || disablePieceGravity || freezePiecesKinematicAfterDelay))
        {
            StartCoroutine(DisablePiecePhysicsAfterDelay());
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

            if (disablePieceGravity)
                rb.useGravity = false;

            if (freezePiecesKinematicAfterDelay)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (disablePieceColliders)
            {
                var cols = rb.GetComponents<Collider>();
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] != null)
                        cols[c].enabled = false;
                }
            }
        }
    }
}

