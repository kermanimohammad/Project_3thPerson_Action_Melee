using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private PlayerInputRouter input;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private AttackManager attackManager;

    public bool IsDefending { get; private set; }
    public bool InAttackState => attackManager.InAttackState();

    private void OnEnable()
    {
        if (input != null)
            input.AttackPressed += TryAttack;
    }

    private void OnDisable()
    {
        if (input != null)
            input.AttackPressed -= TryAttack;
    }

    private void Update()
    {
        if (animator == null)
            return;

        bool canDefend = input != null && motor != null && motor.IsGrounded && !motor.MovementLocked;
        IsDefending = canDefend && input.DefendHeld;
        animator.SetBool(AnimParams.IsDefending, IsDefending);
    }

    private void TryAttack()
    {
        if (IsDefending)
            return;

        if (attackManager == null)
            return;

        attackManager.TryAttack();
    }
}