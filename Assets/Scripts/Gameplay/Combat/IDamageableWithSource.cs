using UnityEngine;

/// <summary>
/// Optional extension for <see cref="IDamageable"/> when the receiver needs the damage source
/// (e.g., to apply knockback/break force direction).
/// </summary>
public interface IDamageableWithSource : IDamageable
{
    void TakeDamage(GameObject source, float amount);
}

