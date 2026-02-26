using UnityEngine;

public class LocalDamageService : MonoBehaviour, IDamageService
{
    private void Awake()
    {
        DamageService.Register(this);
    }

    public void DealDamage(GameObject target)
    {
        if (target.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(25);
        }
    }
}