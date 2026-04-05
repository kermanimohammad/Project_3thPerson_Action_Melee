using System.Collections.Generic;

public class StateMachine<TOwner>
{
	public StateID CurrentStateId { get; private set; }
	public AbstractState<TOwner> CurrentState { get; private set; }
	public TOwner Owner { get; }

	private Dictionary<StateID, AbstractState<TOwner>> states = new Dictionary<StateID, AbstractState<TOwner>>();

	public StateMachine(TOwner owner, Dictionary<StateID, AbstractState<TOwner>> states)
	{
		Owner = owner;
		this.states = states;
	}

	public void SetState(StateID stateId)
	{
		if (CurrentState != null && CurrentState.ID == stateId)
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