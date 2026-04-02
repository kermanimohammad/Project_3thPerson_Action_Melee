using System.Collections.Generic;

public static class StateFactory
{
    public static Dictionary<MainStateID, AbstractState<EnemyAI, MainStateID>> GetMainStates(EnemyAI owner, StateMachine<EnemyAI, MainStateID> machine)
    {
        return new Dictionary<MainStateID, AbstractState<EnemyAI, MainStateID>>
        {
            { MainStateID.Seek, new SeekState(owner, machine) },
            { MainStateID.Flee, new FleeState(owner, machine) },
            { MainStateID.Combat, new CombatState(owner, machine) }
        };
    }

    public static Dictionary<CombatStateID, AbstractState<EnemyAI, CombatStateID>> GetCombatSubStates(EnemyAI owner, StateMachine<EnemyAI, CombatStateID> machine)
    {
        return new Dictionary<CombatStateID, AbstractState<EnemyAI, CombatStateID>>
        {
            { CombatStateID.Attack, new AttackState(owner, machine) },
            { CombatStateID.Defend, new DefendState(owner, machine) },
            { CombatStateID.Flank, new FlankState(owner, machine) }
        };
    }
}