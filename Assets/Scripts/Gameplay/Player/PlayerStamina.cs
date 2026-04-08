using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField, Min(0.01f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float startingStamina = 100f;

    [Header("Regen")]
    [SerializeField, Min(0f)] private float regenPerSecond = 20f;
    [SerializeField, Min(0f)] private float regenDelaySeconds = 0.75f;
    [Tooltip("If false, stamina will not regenerate while sprint is held.")]
    [SerializeField] private bool regenWhileSprinting = false;
    [Tooltip("If false, stamina will not regenerate while block is held.")]
    [SerializeField] private bool regenWhileBlocking = false;

    [Header("Action costs (one-shot)")]
    [SerializeField, Min(0f)] private float attackCost = 10f;
    [SerializeField, Min(0f)] private float specialAttackCost = 25f;
    [SerializeField, Min(0f)] private float dodgeCost = 18f;

    [Header("Action drains (per second)")]
    [SerializeField, Min(0f)] private float sprintDrainPerSecond = 15f;
    [SerializeField, Min(0f)] private float blockDrainPerSecond = 5f;

    [Header("Minimum stamina to START continuous actions")]
    [Tooltip("If CurrentStamina is below this, sprint will not start/continue when requested.")]
    [SerializeField, Min(0f)] private float minStaminaToSprint = 1f;
    [Tooltip("If CurrentStamina is below this, block will not start/continue when requested.")]
    [SerializeField, Min(0f)] private float minStaminaToBlock = 1f;

    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;
    public float Normalized => maxStamina <= 0f ? 0f : Mathf.Clamp01(CurrentStamina / maxStamina);

    public bool SprintHeld { get; private set; }
    public bool BlockHeld { get; private set; }

    public event System.Action<float, float> OnStaminaChanged;

    private float lastSpendTime;

    private void Awake()
    {
        maxStamina = Mathf.Max(0.01f, maxStamina);
        CurrentStamina = Mathf.Clamp(startingStamina, 0f, maxStamina);
        lastSpendTime = -999f;
    }

    private void OnEnable()
    {
        RaiseChanged();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (SprintHeld)
        {
            // If stamina is too low, sprint cannot be held.
            if (CurrentStamina < minStaminaToSprint || CurrentStamina <= 0f)
            {
                SprintHeld = false;
                RaiseChanged();
            }
            else if (sprintDrainPerSecond > 0f)
            {
                SpendInternal(sprintDrainPerSecond * dt);
                if (CurrentStamina <= 0f)
                    SprintHeld = false;
            }
        }

        if (BlockHeld)
        {
            // If stamina is too low, block cannot be held.
            if (CurrentStamina < minStaminaToBlock || CurrentStamina <= 0f)
            {
                BlockHeld = false;
                RaiseChanged();
            }
            else if (blockDrainPerSecond > 0f)
            {
                SpendInternal(blockDrainPerSecond * dt);
                if (CurrentStamina <= 0f)
                    BlockHeld = false;
            }
        }

        bool allowRegen = Time.time >= lastSpendTime + regenDelaySeconds;
        if (!regenWhileSprinting && SprintHeld)
            allowRegen = false;
        if (!regenWhileBlocking && BlockHeld)
            allowRegen = false;

        if (allowRegen && regenPerSecond > 0f && CurrentStamina < maxStamina)
        {
            float before = CurrentStamina;
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + regenPerSecond * dt);
            if (!Mathf.Approximately(before, CurrentStamina))
                RaiseChanged();
        }
    }

    public void SetSprintHeld(bool held)
    {
        if (!held)
        {
            SprintHeld = false;
            return;
        }

        // Requesting sprint: only allow if enough stamina
        if (CurrentStamina >= minStaminaToSprint && CurrentStamina > 0f)
            SprintHeld = true;
        else
            SprintHeld = false;
    }

    public void SetBlockHeld(bool held)
    {
        if (!held)
        {
            BlockHeld = false;
            return;
        }

        // Requesting block: only allow if enough stamina
        if (CurrentStamina >= minStaminaToBlock && CurrentStamina > 0f)
            BlockHeld = true;
        else
            BlockHeld = false;
    }

    public bool CanSpend(float cost) => cost <= CurrentStamina;

    public bool CanAffordAttack() => CanSpend(attackCost);
    public bool TrySpendAttack() => TrySpend(attackCost);
    public bool CanSpendSpecialAttack() => CanSpend(specialAttackCost);
    public bool TrySpendSpecialAttack() => TrySpend(specialAttackCost);
    public bool TrySpendDodge() => TrySpend(dodgeCost);

    public bool TrySpend(float cost)
    {
        if (cost <= 0f)
            return true;

        if (CurrentStamina < cost)
            return false;

        SpendInternal(cost);
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

    public void SetStamina(float newValue)
    {
        float before = CurrentStamina;
        CurrentStamina = Mathf.Clamp(newValue, 0f, maxStamina);
        if (!Mathf.Approximately(before, CurrentStamina))
            RaiseChanged();
    }

    private void SpendInternal(float amount)
    {
        float before = CurrentStamina;
        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        lastSpendTime = Time.time;
        if (!Mathf.Approximately(before, CurrentStamina))
            RaiseChanged();
    }

    private void RaiseChanged()
    {
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }
}

