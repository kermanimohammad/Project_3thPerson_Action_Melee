using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;

    [Header("Normal Attack (Box AOE)")]
    public Vector3 boxSize = new Vector3(2f, 1.5f, 2f);
    public float boxForwardOffset = 1.5f;
    public float boxLifetime = 0.15f;

    [Header("Special Attack (Sphere AOE)")]
    public float sphereRadius = 3f;
    public float sphereForwardOffset = 1.0f;
    public float sphereLifetime = 0.3f;

    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        InputRebindPersistence.LoadAndApply(inputActions.asset);
        InputBindingRuntimeSync.Register(inputActions.asset);
    }

    private void OnDestroy()
    {
        if (inputActions != null)
            InputBindingRuntimeSync.Unregister(inputActions.asset);
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Attack.performed += OnAttack;
        inputActions.Player.S_Attack.performed += OnSAttack;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();

        inputActions.Player.Attack.performed -= OnAttack;
        inputActions.Player.S_Attack.performed -= OnSAttack;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        animator.ResetTrigger("Attack1");
        animator.SetTrigger("Attack1");
    }

    private void OnSAttack(InputAction.CallbackContext context)
    {
        animator.SetBool("S_Attack", true);
    }

    // Called via Animation Event at impact frame
    public void PerformNormalHit()
    {
        Vector3 center = transform.position + transform.forward * boxForwardOffset;

        AreaOfEffectService.Instance.CreateBoxAOE(
        gameObject,
        center,
        boxSize,
        transform.rotation,
        boxLifetime,
        (target) => DamageService.Instance.DealDamage(gameObject, target, 0.25f)
        );
    }

    // Called via Animation Event at impact frame
    public void PerformSpecialHit()
    {
        Vector3 center = transform.position + transform.forward * sphereForwardOffset;

        AreaOfEffectService.Instance.CreateSphereAOE(
        gameObject,
        center,
        sphereRadius,
        sphereLifetime,
        (target) => DamageService.Instance.DealDamage(gameObject, target, 0.40f)
        );
    }

    // Called at end of attack animation
    public void ResetAttack()
    {
        animator.SetBool("Attack", false);
        animator.SetBool("S_Attack", false);
    }
}