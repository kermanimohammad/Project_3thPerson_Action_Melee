using UnityEngine;

public abstract class EnemyAIBase : MonoBehaviour
{
	protected StateMachine<EnemyAIBase> stateMachine;
	private StateID desiredState;

	[Header("References")]
	//[SerializeField] protected EnemyGroupAI groupAI;
	[SerializeField] protected Health health;
	[SerializeField] protected EnemyMoverBase mover;
	[SerializeField] protected EnemyAIPerception perception;
	[SerializeField] protected AttackManager attackManager;
	[SerializeField] protected CharacterDefense characterDefense;

	[Header("Combat")]
	[SerializeField] protected float flankRadius = 3f;
	[SerializeField] protected float defendDuration = 0.8f;
	[SerializeField] protected float fleeHealthThreshold = 0.25f;
	[SerializeField] protected float defendHealthThreshold = 0.5f;

	public Transform CurrentTarget { get; private set; }

	public EnemyMoverBase GetMover() => mover;
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
	}

	//protected virtual void OnEnable()
	//{
	//	if (groupAI != null)
	//		groupAI.Register(this);
	//}

	//protected virtual void OnDisable()
	//{
	//	if (groupAI != null)
	//		groupAI.Unregister(this);
	//}

	protected void Update()
	{
		CurrentTarget = GetUpdatedTarget();

		stateMachine.Tick();

		desiredState = UpdateDesiredState();

		if (desiredState != stateMachine.CurrentStateId && stateMachine.CurrentState.CanTransitionTo(desiredState))
		{
			stateMachine.SetState(desiredState);
		}
	}

	protected abstract StateID UpdateDesiredState();
	protected abstract Transform GetUpdatedTarget();
	protected abstract bool ShouldFlee();
	protected abstract bool ShouldSeek();
	protected abstract bool ShouldAttack();
	protected abstract bool ShouldDefend();
	protected abstract bool ShouldFlank();

}
