using UnityEngine;

public class SwordAttack : MonoBehaviour
{
    [Header("Normal Attack")]
    [SerializeField] private Vector3 normalBoxSize = new Vector3(2f, 1.5f, 2f);
    [SerializeField] private float normalLifetime = 0.15f;

    [Header("Special Attack")]
    [SerializeField] private Vector3 specialBoxSize = new Vector3(4f, 2f, 4f);
    [SerializeField] private float specialLifetime = 0.3f;

    [SerializeField] private float forwardOffset = 1.5f;

    private PlayerInputActions inputActions;
    private Animator animator;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Attack.performed += ctx => StartNormalAttack();
        inputActions.Player.S_Attack.performed += ctx => StartSpecialAttack();
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void StartNormalAttack()
    {
        animator.SetBool("Attack", true);
    }

    private void StartSpecialAttack()
    {
        animator.SetBool("S_Attack", true);
    }

    // Animation Event
    public void PerformNormalHit()
    {
        Vector3 center = transform.position + transform.forward * forwardOffset;

        AreaOfEffectService.Instance.CreateBoxAOE(
            center,
            normalBoxSize,
            transform.rotation,
            normalLifetime,
            DamageService.Instance.DealDamage
        );
    }

    // Animation Event
    public void PerformSpecialHit()
    {
        Vector3 center = transform.position + transform.forward * forwardOffset;

        AreaOfEffectService.Instance.CreateBoxAOE(
            center,
            specialBoxSize,
            transform.rotation,
            specialLifetime,
            DamageService.Instance.DealDamage
        );
    }

    // Animation Event at the end of attack
    public void EndAttack()
    {
        animator.SetBool("Attack", false);
        animator.SetBool("S_Attack", false);
    }
}