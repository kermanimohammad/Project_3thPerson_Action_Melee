using UnityEngine;

public class LocalDamageService : MonoBehaviour, IDamageService
{
    private const string EnemyTag = "Enemy";

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

        if (IsEnemyFriendlyFire(source, target))
            return;

        if (source != null && HierarchyHasTag(source.transform, EnemyTag))
            amount *= BattleProgression.CappedMultiplier;

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

    /// <summary>
    /// Melee/AOE from one enemy must not damage another enemy (same tag on self or any parent).
    /// </summary>
    private static bool IsEnemyFriendlyFire(GameObject source, GameObject target)
    {
        if (source == null)
            return false;

        return HierarchyHasTag(source.transform, EnemyTag) && HierarchyHasTag(target.transform, EnemyTag);
    }

    private static bool HierarchyHasTag(Transform t, string tag)
    {
        while (t != null)
        {
            if (t.gameObject.CompareTag(tag))
                return true;
            t = t.parent;
        }

        return false;
    }
}