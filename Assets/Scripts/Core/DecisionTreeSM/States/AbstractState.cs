using System;

public abstract class AbstractState<TOwner>
{
    public StateID ID { get; }
    protected readonly TOwner owner;
    protected readonly StateMachine<TOwner> stateMachine;

    protected AbstractState(StateID id, TOwner owner, StateMachine<TOwner> stateMachine)
    {
        ID = id;
        this.owner = owner;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Tick() { }
    public virtual bool CanTransitionTo(StateID stateID) { return true; }
}