//using System;

//public abstract class AbstractCompositeState<TOwner, TStateID, TSubStateID> : AbstractState<TOwner, TStateID> where TStateID : Enum where TSubStateID : Enum
//{
//    protected StateMachine<TOwner, TSubStateID> subStateMachine;

//    protected AbstractCompositeState(
//        TStateID id,
//        TOwner owner,
//        StateMachine<TOwner, TStateID> stateMachine)
//        : base(id, owner, stateMachine)
//    {
//    }

//    public override void Exit()
//    {
//        subStateMachine?.CurrentState?.Exit();
//    }
//}