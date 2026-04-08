using UnityEngine;

/// <summary>
/// Simple stamina model for enemies. Designed to be driven by AI (movement/attack gating).
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField, Min(0.01f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float startingStamina = 100f;

    [Header("Regen")]
    [SerializeField, Min(0f)] private float regenPerSecond = 18f;
    [SerializeField, Min(0f)] private float regenDelaySeconds = 0.6f;

    [Header("Action costs")]
    [Tooltip("Cost to start an attack input (one-shot).")]
    [SerializeField, Min(0f)] private float attackCost = 10f;
    [Tooltip("Cost per second while moving at full speed (scaled by speed fraction 0..1).")]
    [SerializeField, Min(0f)] private float moveDrainPerSecondAtFullSpeed = 10f;

    [Header("Minimum stamina gates")]
    [SerializeField, Min(0f)] private float minStaminaToAttack = 1f;
    [SerializeField, Min(0f)] private float minStaminaToMove = 0f;

    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;
    public float Normalized => maxStamina <= 0f ? 0f : Mathf.Clamp01(CurrentStamina / maxStamina);

    public event System.Action<float, float> OnStaminaChanged;

    private float _lastSpendTime;

    private void Awake()
    {
        maxStamina = Mathf.Max(0.01f, maxStamina);
        CurrentStamina = Mathf.Clamp(startingStamina, 0f, maxStamina);
        _lastSpendTime = -999f;
        RaiseChanged();
    }

    private void Update()
    {
        if (regenPerSecond <= 0f || CurrentStamina >= maxStamina)
            return;

        if (Time.time < _lastSpendTime + regenDelaySeconds)
            return;

        float before = CurrentStamina;
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + regenPerSecond * Time.deltaTime);
        if (!Mathf.Approximately(before, CurrentStamina))
            RaiseChanged();
    }

    public bool CanMove() => CurrentStamina >= minStaminaToMove;

    public void SpendMove(float speed01, float dt)
    {
        if (moveDrainPerSecondAtFullSpeed <= 0f)
            return;
        if (dt <= 0f)
            return;
        if (speed01 <= 0f)
            return;

        SpendInternal(moveDrainPerSecondAtFullSpeed * Mathf.Clamp01(speed01) * dt);
    }

    public bool CanStartAttack() => CurrentStamina >= Mathf.Max(minStaminaToAttack, attackCost);

    public bool TrySpendAttack()
    {
        if (attackCost <= 0f)
            return true;
        if (CurrentStamina < Mathf.Max(minStaminaToAttack, attackCost))
            return false;
        SpendInternal(attackCost);
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f)
            return;
        float before = CurrentStamina;
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + amount);
        if (!Mathf.Approximately(before, CurrentStamina))
            RaiseChanged();
    }

    private void SpendInternal(float amount)
    {
        if (amount <= 0f)
            return;
        float before = CurrentStamina;
        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        _lastSpendTime = Time.time;
        if (!Mathf.Approximately(before, CurrentStamina))
            RaiseChanged();
    }

    private void RaiseChanged() => OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
}

