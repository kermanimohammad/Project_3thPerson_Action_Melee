//using UnityEngine;
//public class CombatState : AbstractState<EnemyAI, StateID>
//{
//    public CombatState(EnemyAI owner, StateMachine<EnemyAI> stateMachine) : base(StateID.Attack, owner, stateMachine)
//    {
//        subStateMachine = new StateMachine<EnemyAI, CombatStateID>(owner);
//        subStateMachine.SetStates(StateFactory.GetCombatSubStates(owner, subStateMachine));
//    }

//    public override void Enter()
//    {
//        if (owner.VerboseLogs)
//            Debug.Log($"{owner.name} ENTER -> Combat");

//        subStateMachine.SetState(CombatStateID.Attack);
//    }

//    public override void Exit()
//    {
//        if (owner.VerboseLogs)
//            Debug.Log($"{owner.name} EXIT -> Combat");

//        base.Exit();
//    }

//    public override void Tick()
//    {
//        if (owner.VerboseLogs)
//            Debug.Log($"{owner.name} TICK -> Combat");

//        if (owner.ShouldFlee())
//        {
//            if (owner.VerboseLogs)
//                Debug.Log($"{owner.name} Combat -> Flee");

//            stateMachine.SetState(StateID.Flee);
//            return;
//        }

//        bool canSee = owner.CanSeePlayer();

//        if (owner.VerboseLogs)
//            Debug.Log($"{owner.name} Combat visibility check -> canSeePlayer = {canSee}");

//        if (!canSee && owner.HasLostPlayer())
//        {
//            if (owner.VerboseLogs)
//                Debug.Log($"{owner.name} Combat -> Seek (player lost)");

//            stateMachine.SetState(StateID.Seek);
//            return;
//        }

//        CombatStateID desiredSubState;

//        if (owner.ShouldDefend())
//            desiredSubState = CombatStateID.Defend;
//        else if (owner.ShouldFlank())
//            desiredSubState = CombatStateID.Flank;
//        else
//            desiredSubState = CombatStateID.Attack;

//        if (owner.VerboseLogs)
//            Debug.Log($"{owner.name} Combat desired substate -> {desiredSubState}");


//        if (subStateMachine.CurrentState == null || subStateMachine.CurrentStateId != desiredSubState) {
//            if (owner.VerboseLogs)
//                Debug.Log($"{owner.name} Combat substate switch -> {desiredSubState}");

//            subStateMachine.SetState(desiredSubState);
//        }

//        subStateMachine.Tick();
//    }
//}