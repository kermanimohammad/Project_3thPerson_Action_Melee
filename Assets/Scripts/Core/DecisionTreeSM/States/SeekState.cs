public class SeekState : AbstractState<EnemyAIBase>
{
	public SeekState(EnemyAIBase owner, StateMachine<EnemyAIBase> stateMachine) : base(StateID.Seek, owner, stateMachine)
	{
	}

	public override void Enter()
	{
	}

	public override void Exit()
	{
	}

	public override void Tick()
	{
		owner.GetMover().MoveTo(owner.CurrentTarget.position);
	}
}