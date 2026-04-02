using System;
using System.Collections.Generic;

public class StateMachine<TOwner, TStateID> where TStateID : Enum
{
    public TStateID CurrentStateId { get; private set; }
    public AbstractState<TOwner, TStateID> CurrentState { get; private set; }
    public TOwner Owner { get; }

    private Dictionary<TStateID, AbstractState<TOwner, TStateID>> states = new Dictionary<TStateID, AbstractState<TOwner, TStateID>>();

    public StateMachine(TOwner owner)
    {
        Owner = owner;
    }

    public void SetStates(Dictionary<TStateID, AbstractState<TOwner, TStateID>> states)
    {
        this.states = states;
    }

    public void SetState(TStateID stateId)
    {
        if (CurrentState != null && EqualityComparer<TStateID>.Default.Equals(CurrentStateId, stateId))
        {
            return;
        }

        CurrentState?.Exit();

        CurrentStateId = stateId;
        CurrentState = states[stateId];

        CurrentState.Enter();
    }

    public void Tick()
    {
        CurrentState?.Tick();
    }
}