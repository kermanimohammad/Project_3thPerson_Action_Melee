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

        if (!target.TryGetComponent(out CharacterDefense targetDefense))
        {
            throw new System.InvalidOperationException($"{target.name} is damageable but has no CharacterDefense component.");
        }

        if (targetDefense.IsDefending)
        {
            amount *= (1 - targetDefense.DamageReductionPercentage);
        }

        damageable.TakeDamage(amount);

        Debug.Log($"{source.name} dealt {amount} damage to {target.name}");

    }
}