using UnityEngine;
using UnityEngine.InputSystem;

public class DebugKillEnemy : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Key killKey = Key.K;

    private void Reset()
    {
        health = GetComponent<Health>();
    }

    private void Update()
    {
        if (Keyboard.current == null || health == null)
            return;

        if (Keyboard.current[killKey].wasPressedThisFrame)
        {
            health.TakeDamage(9999);
        }
    }
}