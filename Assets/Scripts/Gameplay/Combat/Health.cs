using System;
using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
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

    [Header("Death")]
    [Tooltip("If false (e.g. player), only the Death animator trigger runs; the GameObject is never destroyed by Health.")]
    [SerializeField] private bool destroyGameObjectOnDeath = true;
    [Tooltip("After HP hits 0: play Death trigger, then wait this many seconds before Destroy. Ignored if Destroy On Death is off. 0 = destroy immediately.")]
    [SerializeField, Min(0f)] private float destroyDelayAfterDeath;
    [SerializeField] private string deathTriggerName = "Death";

    public event Action<float, float> OnHealthChanged;
    // (currentHealth, maxHealth)

    public event Action OnDied;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float Normalized01 => maxHealth <= 0 ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => _dead;

    private Coroutine _deathRoutine;
    private bool _dead;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        currentHealth = maxHealth;
        normalized01 = Normalized01;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (_dead)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        normalized01 = Normalized01;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth > 0f)
            TryPlayHitReaction();

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

    private void TryPlayHitReaction()
    {
        if (animator == null)
            return;

        if (Time.time < nextHitReactTime)
            return;

        nextHitReactTime = Time.time + hitReactCooldownSeconds;
        animator.ResetTrigger(AnimParams.Hit);
        animator.SetTrigger(AnimParams.Hit);
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

        TryPlayDeathAnimation();

        if (!destroyGameObjectOnDeath)
            return;

        if (destroyDelayAfterDeath <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);
        _deathRoutine = StartCoroutine(DestroyAfterDeathDelay());
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
}
