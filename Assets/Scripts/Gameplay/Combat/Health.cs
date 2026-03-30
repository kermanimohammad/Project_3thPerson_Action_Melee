using System;
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

    public event Action<float, float> OnHealthChanged;
    // (currentHealth, maxHealth)

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float Normalized01 => maxHealth <= 0 ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

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
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        normalized01 = Normalized01;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        TryPlayHitReaction();

        if (currentHealth <= 0)
        {
            Die();
        }
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
        Destroy(gameObject);
    }
}