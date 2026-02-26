using UnityEngine;
using UnityEngine.InputSystem;

public class AOETest : MonoBehaviour
{
    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            AreaOfEffectService.Instance.CreateSphereAOE(
            transform.position,
            3f,
            0.2f,
            DamageService.Instance.DealDamage
            );
        }
    }
}