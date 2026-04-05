using UnityEngine;
public class AttackState : AbstractState<EnemyAIBase>
{
	public AttackState(EnemyAIBase owner, StateMachine<EnemyAIBase> stateMachine) : base(StateID.Attack, owner, stateMachine)
	{
	}

	public override bool CanTransitionTo(StateID stateID)
	{
		return !owner.GetAttackManager().InAttackState();
	}

	public override void Tick()
	{
		TryRotateTowardsAimTarget(owner.CurrentTarget); 
		owner.GetAttackManager().TryAttack();
	}

	private void TryRotateTowardsAimTarget(Transform aimTarget)
	{
		if (aimTarget == null)
		{
			return;
		}

		Vector3 direction = aimTarget.position - owner.transform.position;
		direction.y = 0f;

		if (direction.sqrMagnitude > 0.001f)
		{
			Quaternion targetRotation = Quaternion.LookRotation(direction);
			owner.transform.rotation = targetRotation;
		}
	}
}