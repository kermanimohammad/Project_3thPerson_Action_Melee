using UnityEngine;

public class LocalDamageService : MonoBehaviour, IDamageService
{
    private void Awake()
    {
        if (DamageService.Instance != null)
        {
            Debug.LogWarning("Multiple DamageService instances detected.");
        }

        DamageService.Register(this);
    }

    public void DealDamage(GameObject source, GameObject target, float amount)
    {
        if (target == null)
            return;

        if (!target.TryGetComponent<IDamageable>(out var damageable))
        {
            return;
        }

        // Defense is optional (e.g., breakable props like doors).
        if (target.TryGetComponent(out CharacterDefense targetDefense) && targetDefense.IsDefending)
            amount *= (1 - targetDefense.DamageReductionPercentage);

        if (damageable is IDamageableWithSource damageableWithSource)
        {
            damageableWithSource.TakeDamage(source, amount);
        }
        else
        {
            damageable.TakeDamage(amount);
        }

        Debug.Log($"{source.name} dealt {amount} damage to {target.name}");

    }
}