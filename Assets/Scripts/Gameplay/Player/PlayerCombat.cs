using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private PlayerInputRouter input;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private AttackManager attackManager;
    [SerializeField] private CharacterDefense characterDefense;

    public bool IsDefending => characterDefense.IsDefending;
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

        UpdateDefense();
    }

    private void UpdateDefense()
    {
        if (input == null || motor == null)
        {
            return;
        }

        bool shouldDefend = input.DefendHeld && motor.IsGrounded && !motor.MovementLocked;

        if (shouldDefend)
        {
            characterDefense.StartDefend();
        }
        else if (IsDefending)
        {
            characterDefense.StopDefend();
        }
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