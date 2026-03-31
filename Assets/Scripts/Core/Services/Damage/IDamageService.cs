using UnityEngine;

public interface IDamageService
{
    void DealDamage(GameObject source, GameObject target, float amount);
}