using UnityEngine;
public class FleeState : AbstractState<EnemyAIBase>
{
	public FleeState(EnemyAIBase owner, StateMachine<EnemyAIBase> stateMachine) : base(StateID.Flee, owner, stateMachine)
	{
	}

	public override void Tick()
	{
		Vector3 away = (owner.transform.position - owner.CurrentTarget.position).normalized * 6f;
		owner.GetMover().MoveTo(away);
	}
}