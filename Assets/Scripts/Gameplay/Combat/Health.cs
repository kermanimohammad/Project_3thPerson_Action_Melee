using System;
using System.Collections;
using UnityEngine;
#if UNITY_AI_NAVIGATION
using UnityEngine.AI;
#endif

public class Health : MonoBehaviour, IDamageableWithSource
{
    [SerializeField] private int maxHealth = 1;

    [Header("Runtime (read-only)")]
    [SerializeField] private float currentHealth;
    [SerializeField, Range(0f, 1f)] private float normalized01;

    [Header("Hit reaction (optional)")]
    [SerializeField] private Animator animator;
    [Tooltip("Minimum seconds between hit reactions.")]
    [SerializeField] private float hitReactCooldownSeconds = 0.1f;
    private float nextHitReactTime;
    private Vector2 _lastHitDir01 = Vector2.up;

    /// <summary>
    /// Last planar hit direction in the player's local space (x=left/right, y=forward/back),
    /// used to drive the Hit blend tree (HitX/HitY).
    /// </summary>
    public Vector2 LastHitDirLocal01 => _lastHitDir01;

    [Header("Death")]
    [Tooltip("If false (e.g. player), only the Death animator trigger runs; the GameObject is never destroyed by Health.")]
    [SerializeField] private bool destroyGameObjectOnDeath = true;
    [Tooltip("After HP hits 0: play Death trigger, then wait this many seconds before Destroy. Ignored if Destroy On Death is off. 0 = destroy immediately.")]
    [SerializeField, Min(0f)] private float destroyDelayAfterDeath;
    [SerializeField] private string deathTriggerName = "Death";

    [Header("Player-style death lock (optional)")]
    [Tooltip("If true and Destroy On Death is OFF, disables other behaviours on this GameObject after Death triggers (prevents movement/attacks/other animations).")]
    [SerializeField] private bool disableOtherBehavioursOnDeathWhenNotDestroyed = true;

    [Tooltip("If > 0 and Destroy On Death is OFF: after this delay, freezes the Animator so no further animations can play. Leave at 0 to let Death play fully and keep Animator running.")]
    [SerializeField, Min(0f)] private float freezeAnimatorAfterDeathSeconds = 0f;

    [Tooltip("If true, disables the Animator component after freezing (strongest guarantee nothing else plays). Typically leave OFF so Death can play.")]
    [SerializeField] private bool disableAnimatorComponentAfterFreeze = false;

    public event Action<float, float> OnHealthChanged;
    // (currentHealth, maxHealth)

    public event Action OnDied;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float Normalized01 => maxHealth <= 0 ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => _dead;

    private Coroutine _deathRoutine;
    private Coroutine _deathLockRoutine;
    private bool _dead;
    private bool _freezeYawAfterDeath;
    private bool _deathYawCapturePending;
    private float _lockedYawDegrees;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(includeInactive: true);

        currentHealth = maxHealth;
        normalized01 = Normalized01;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void LateUpdate()
    {
        if (_deathYawCapturePending)
        {
            _lockedYawDegrees = transform.eulerAngles.y;
            _deathYawCapturePending = false;
            _freezeYawAfterDeath = true;
        }

        if (!_freezeYawAfterDeath)
            return;

        Vector3 e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(e.x, _lockedYawDegrees, e.z);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(source: null, amount: amount);
    }

    public void TakeDamage(GameObject source, float amount)
    {
        if (_dead)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        normalized01 = Normalized01;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth > 0f)
            TryPlayHitReaction(source);

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(float amount)
    {
        if (_dead)
            return;

        if (amount <= 0f)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        normalized01 = Normalized01;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void TryPlayHitReaction(GameObject source)
    {
        if (animator == null)
            return;

        if (Time.time < nextHitReactTime)
            return;

        nextHitReactTime = Time.time + hitReactCooldownSeconds;

        UpdateHitDirectionParams(source);
        animator.ResetTrigger(AnimParams.Hit);
        animator.SetTrigger(AnimParams.Hit);
    }

    private void UpdateHitDirectionParams(GameObject source)
    {
        if (animator == null)
            return;

        if (source != null)
        {
            Vector3 toSource = source.transform.position - transform.position;
            toSource.y = 0f;
            if (toSource.sqrMagnitude > 1e-6f)
            {
                Vector3 local = transform.InverseTransformDirection(toSource.normalized);
                // BlendTree uses HitX (left/right) and HitY (forward/back).
                _lastHitDir01 = new Vector2(local.x, local.z);
            }
        }

        Vector2 v = _lastHitDir01;
        if (v.sqrMagnitude < 1e-6f)
            v = Vector2.up;
        v = Vector2.ClampMagnitude(v, 1f);

        animator.SetFloat(AnimParams.HitX, v.x);
        animator.SetFloat(AnimParams.HitY, v.y);
    }

    private void Die()
    {
        if (_dead)
            return;

        _dead = true;
        OnDied?.Invoke();

        foreach (var attack in GetComponentsInChildren<AttackManager>(true))
        {
            if (attack != null)
                attack.CancelBufferedComboNow();
        }

        StopLocomotionDriversForDeath();

        TryPlayDeathAnimation();

        BeginDeathYawFreeze();

        if (!destroyGameObjectOnDeath)
        {
            if (_deathLockRoutine != null)
                StopCoroutine(_deathLockRoutine);
            _deathLockRoutine = StartCoroutine(DeathLockRoutine());
            return;
        }

        if (destroyDelayAfterDeath <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);
        _deathRoutine = StartCoroutine(DestroyAfterDeathDelay());
    }

    /// <summary>
    /// When <see cref="destroyGameObjectOnDeath"/> is true, <see cref="DeathLockRoutine"/> does not run,
    /// so NavMesh, squad AI, decision-tree AI (<see cref="EnemyAIBase"/>), and <see cref="CharacterDefense"/>
    /// could keep driving the animator until Destroy (spin, defend overlay on Death, etc.).
    /// </summary>
    private void StopLocomotionDriversForDeath()
    {
#if UNITY_AI_NAVIGATION
        foreach (var agent in GetComponentsInChildren<NavMeshAgent>(true))
        {
            if (agent == null)
                continue;
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.angularSpeed = 0f;
            agent.updateRotation = false;
            agent.updatePosition = false;
            agent.enabled = false;
        }
#endif
        foreach (var mover in GetComponentsInChildren<EnemyMover>(true))
        {
            if (mover != null)
                mover.enabled = false;
        }

        foreach (var squad in GetComponentsInChildren<EnemyGroupMemberAI>(true))
        {
            if (squad != null)
                squad.enabled = false;
        }

        foreach (var treeAi in GetComponentsInChildren<EnemyAIBase>(true))
        {
            if (treeAi != null)
                treeAi.enabled = false;
        }

        foreach (var defense in GetComponentsInChildren<CharacterDefense>(true))
        {
            if (defense == null)
                continue;
            defense.StopDefend();
            defense.enabled = false;
        }

        foreach (var hitMove in GetComponentsInChildren<EnemyHitMove>(true))
        {
            if (hitMove != null)
                hitMove.enabled = false;
        }

        foreach (var atk in GetComponentsInChildren<AttackManager>(true))
        {
            if (atk != null)
                atk.enabled = false;
        }

        // Death clips / blends can still drive transform rotation via root motion — disable for the corpse window.
        foreach (var anim in GetComponentsInChildren<Animator>(true))
        {
            if (anim != null)
                anim.applyRootMotion = false;
        }

        // Non-kinematic Rigidbody + CharacterController on enemies: after AI stops, physics can still receive
        // collision impulses → visible pops / launch upward. Freeze the corpse for the destroy delay window.
        FreezePhysicsForDeathCorpse();
    }

    private void FreezePhysicsForDeathCorpse()
    {
        bool hasDynamicRigidbody = false;
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb != null && !rb.isKinematic)
            {
                hasDynamicRigidbody = true;
                break;
            }
        }

        // Player-style setups often use only CharacterController; do not disable CC unless a dynamic RB was driving conflicts.
        if (!hasDynamicRigidbody)
            return;

        foreach (var cc in GetComponentsInChildren<CharacterController>(true))
        {
            if (cc != null)
                cc.enabled = false;
        }

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null)
                continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints |= RigidbodyConstraints.FreezeRotationY;
        }
    }

    private void BeginDeathYawFreeze()
    {
        _deathYawCapturePending = true;
    }

    private void TryPlayDeathAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(deathTriggerName))
            return;

        animator.enabled = true;
        int id = Animator.StringToHash(deathTriggerName);
        animator.ResetTrigger(id);
        animator.SetTrigger(id);
    }

    private IEnumerator DestroyAfterDeathDelay()
    {
        yield return new WaitForSeconds(destroyDelayAfterDeath);
        _deathRoutine = null;
        Destroy(gameObject);
    }

    private IEnumerator DeathLockRoutine()
    {
        // Immediately stop other gameplay behaviours from driving animator params / movement.
        if (disableOtherBehavioursOnDeathWhenNotDestroyed)
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null || b == this)
                    continue;
                b.enabled = false;
            }
        }

        // Optional: after some time (e.g. after Death has clearly finished), freeze the animator.
        // Default is 0 seconds, meaning: do not freeze; just block gameplay inputs/behaviours.
        if (animator != null && freezeAnimatorAfterDeathSeconds > 0f)
        {
            yield return new WaitForSeconds(freezeAnimatorAfterDeathSeconds);
            animator.speed = 0f;
            if (disableAnimatorComponentAfterFreeze)
                animator.enabled = false;
        }

        _deathLockRoutine = null;
    }
}
