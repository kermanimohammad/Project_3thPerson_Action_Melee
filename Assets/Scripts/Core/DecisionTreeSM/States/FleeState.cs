using UnityEngine;
public class FleeState : AbstractState<EnemyAI, MainStateID>
{
    public FleeState(EnemyAI owner, StateMachine<EnemyAI, MainStateID> stateMachine) : base(MainStateID.Flee, owner, stateMachine)
    {
    }

    public override void Enter()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} ENTER -> Flee");
    }

    public override void Exit()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} EXIT -> Flee");
    }

    public override void Tick()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} TICK -> Flee");

        owner.MoveTo(owner.GetRetreatDestination(), 1.1f);

        if (!owner.ShouldFlee())
        {
            if (owner.ShouldEnterCombat())
                stateMachine.SetState(MainStateID.Combat);
            else
                stateMachine.SetState(MainStateID.Seek);
        }
    }
}