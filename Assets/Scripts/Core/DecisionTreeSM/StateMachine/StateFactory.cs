using System.Collections.Generic;

public static class StateFactory
{
	public static Dictionary<StateID, AbstractState<EnemyAIBase>> GetMainStates(EnemyAIBase owner, StateMachine<EnemyAIBase> machine)
	{
		return new Dictionary<StateID, AbstractState<EnemyAIBase>>
		{
			{StateID.Seek, new SeekState(owner, machine) },
			{StateID.Flee, new FleeState(owner, machine) },
			{StateID.Attack, new AttackState(owner, machine) },
			{StateID.Defend, new DefendState(owner, machine) },
			{StateID.Flank, new FlankState(owner, machine) },
		};
	}
}