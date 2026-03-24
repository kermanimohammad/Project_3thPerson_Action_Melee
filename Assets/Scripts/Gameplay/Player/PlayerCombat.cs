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

    private const string _EnemyTag = "Enemy";

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

        LayerMask enemyLayer = LayerMask.GetMask(_EnemyTag);
        Transform aimTarget = transform.GetClosestNearbyEnemy(enemyLayer);
        TryRotateTowardsAimTarget(aimTarget);

        attackManager.TryAttack();
    }

    private void TryRotateTowardsAimTarget(Transform aimTarget)
	{
        if (aimTarget == null)
		{
            return;
		}

        Vector3 direction = aimTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = targetRotation;
        }
    }

}