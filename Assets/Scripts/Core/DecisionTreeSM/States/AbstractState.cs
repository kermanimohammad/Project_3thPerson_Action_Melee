using System;

public abstract class AbstractState<TOwner, TStateID> where TStateID : Enum
{
    public TStateID ID { get; }
    protected readonly TOwner owner;
    protected readonly StateMachine<TOwner, TStateID> stateMachine;

    protected AbstractState(TStateID id, TOwner owner, StateMachine<TOwner, TStateID> stateMachine)
    {
        ID = id;
        this.owner = owner;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Tick() { }
}