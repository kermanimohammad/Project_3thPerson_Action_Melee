using UnityEngine;

public class SeekState : AbstractState<EnemyAIBase>
{
	public SeekState(EnemyAIBase owner, StateMachine<EnemyAIBase> stateMachine) : base(StateID.Seek, owner, stateMachine)
	{
	}

	float timer = 0f;
	const float repeat = 0.5f;

	public override void Enter()
	{
		if (owner.CurrentTarget == GlobalReferences.Instance.GetPlayer())
		{
			// generate path every tick
		}
		else
		{
			// generate path once
		}
	}

	public override void Exit()
	{
	}

	public override void Tick()
	{
		// repeat only if chasing the player
		if (timer <= 0 && owner.CurrentTarget == GlobalReferences.Instance.GetPlayer())
		{
			timer = repeat;
			(owner.GetMover() as ConcreteEnemyMover).RecalculatePathFinding(owner.CurrentTarget.position);
		}

		(owner.GetMover() as ConcreteEnemyMover).Move();

		timer -= Time.deltaTime;		
	}

}
