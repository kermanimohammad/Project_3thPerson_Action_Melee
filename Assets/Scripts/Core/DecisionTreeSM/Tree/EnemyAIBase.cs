using UnityEngine;

public abstract class EnemyAIBase : MonoBehaviour
{
	protected StateMachine<EnemyAIBase> stateMachine;
	private StateID desiredState;

	[Header("References")]
	[SerializeField] protected EnemyGroup groupAI;
	[SerializeField] protected Health health;
	[SerializeField] protected EnemyMover mover;
	[SerializeField] protected EnemyAIPerception perception;
	[SerializeField] protected AttackManager attackManager;
	[SerializeField] protected CharacterDefense characterDefense;

	[Header("Combat")]
	[SerializeField] protected float flankRadius = 3f;
	[SerializeField] protected float defendDuration = 0.8f;
	[SerializeField] protected float fleeHealthThreshold = 0.25f;
	[SerializeField] protected float defendHealthThreshold = 0.5f;

	[Header("Decision Timing")]
	[SerializeField] private float mainStateDuration = 0.75f;
	private float stateTimer;

	public Transform CurrentTarget { get; private set; }

	public EnemyMover GetMover() => mover;
	public AttackManager GetAttackManager() => attackManager;
	public CharacterDefense GetDefense() => characterDefense;

	public bool IsTestMode
	{
		get
		{
			return AITestMode.Instance != null && AITestMode.Instance.TestMode;
		}
	}

	public bool VerboseLogs
	{
		get
		{
			return AITestMode.Instance != null && AITestMode.Instance.VerboseLogs;
		}
	}

	protected virtual void Awake()
	{
		stateMachine = new StateMachine<EnemyAIBase>(this, StateFactory.GetMainStates(this, stateMachine));
		stateMachine.SetState(StateID.Seek);
	}

	protected void Update()
	{
		if (health != null && health.IsDead)
			return;

		CurrentTarget = GetUpdatedTarget();

		stateMachine.Tick();

		stateTimer -= Time.deltaTime;

		if (stateTimer > 0f)
			return;


		desiredState = GetUpdatedDesiredState();

		if (desiredState != stateMachine.CurrentStateId && stateMachine.CurrentState.CanTransitionTo(desiredState))
		{
			stateMachine.SetState(desiredState);
			stateTimer = mainStateDuration;
		}

	}

	protected virtual Transform GetUpdatedTarget() => groupAI.GetAssignedTarget(this);

	protected abstract StateID GetUpdatedDesiredState();
	protected abstract float GetFleeScore();
	protected abstract float GetSeekScore();
	protected abstract float GetAttackScore();
	protected abstract float GetDefendScore();
	protected abstract float GetFlankScore();

}
