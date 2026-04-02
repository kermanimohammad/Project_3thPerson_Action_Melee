using UnityEngine;
public class CombatState : AbstractCompositeState<EnemyAI, MainStateID, CombatStateID>
{
    public CombatState(EnemyAI owner, StateMachine<EnemyAI, MainStateID> stateMachine) : base(MainStateID.Combat, owner, stateMachine)
    {
        subStateMachine = new StateMachine<EnemyAI, CombatStateID>(owner);
        subStateMachine.SetStates(StateFactory.GetCombatSubStates(owner, subStateMachine));
    }

    public override void Enter()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} ENTER -> Combat");

        subStateMachine.SetState(CombatStateID.Attack);
    }

    public override void Exit()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} EXIT -> Combat");

        base.Exit();
    }

    public override void Tick()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} TICK -> Combat");

        if (owner.ShouldFlee())
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Combat -> Flee");

            stateMachine.SetState(MainStateID.Flee);
            return;
        }

        bool canSee = owner.CanSeePlayer();

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} Combat visibility check -> canSeePlayer = {canSee}");

        if (!canSee && owner.HasLostPlayer())
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Combat -> Seek (player lost)");

            stateMachine.SetState(MainStateID.Seek);
            return;
        }

        CombatStateID desiredSubState;

        if (owner.ShouldDefend())
            desiredSubState = CombatStateID.Defend;
        else if (owner.ShouldFlank())
            desiredSubState = CombatStateID.Flank;
        else
            desiredSubState = CombatStateID.Attack;

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} Combat desired substate -> {desiredSubState}");


        if (subStateMachine.CurrentState == null || subStateMachine.CurrentStateId != desiredSubState) {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Combat substate switch -> {desiredSubState}");

            subStateMachine.SetState(desiredSubState);
        }

        subStateMachine.Tick();
    }
}