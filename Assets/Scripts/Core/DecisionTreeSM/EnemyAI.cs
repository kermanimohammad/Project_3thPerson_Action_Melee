using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyGroupAI groupAI;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private EnemyMoverBase mover;

    [Header("Perception")]
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float detectionRange = 18f;
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float wallBreakRange = 2.4f;
    [SerializeField] private float losePlayerAfter = 4f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Combat")]
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float wallDamage = 5f;
    [SerializeField] private float flankRadius = 3f;
    [SerializeField] private float defendDuration = 0.8f;
    [SerializeField] private float fleeHealthThreshold = 0.25f;
    [SerializeField] private float defendHealthThreshold = 0.5f;

    [Header("Debug Test Inputs")]
    [SerializeField] private bool debugPlayerDetected;
    [SerializeField] private bool debugLowHealth;
    [SerializeField] private bool debugShouldDefend;
    [SerializeField] private bool debugShouldFlank;
    [SerializeField] private bool debugPlayerLost;
    [SerializeField] private bool debugWallBroken;
    [SerializeField] private bool debugReachedDestination;
    [SerializeField] private bool debugInAttackRange = true;

    [Header("Debug Wall Simulation")]
    [SerializeField] private int debugWallHitsToBreak = 3;
    [SerializeField] private int debugWallHitCount = 0;

    private float lastAttackTime = -999f;
    private float lastSeenPlayerTime = -999f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DefendHash = Animator.StringToHash("isDefending");

    public StateMachine<EnemyAI, MainStateID> MainStateMachine { get; private set; }

    public EnemyGroupAI GroupAI => groupAI;
    public Transform Player => player;
    public Animator Animator => animator;
    public EnemyMoverBase Mover => mover;

    public float AttackRange => attackRange;
    public float WallBreakRange => wallBreakRange;
    public float FlankRadius => flankRadius;
    public float DefendDuration => defendDuration;
    public float LosePlayerAfter => losePlayerAfter;
    public float TimeSinceLastSeenPlayer => Time.time - lastSeenPlayerTime;

    public bool CanAttackNow => Time.time >= lastAttackTime + attackCooldown;

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

    private void OnEnable()
    {
        if (groupAI != null)
            groupAI.Register(this);
    }

    private void OnDisable()
    {
        if (groupAI != null)
            groupAI.Unregister(this);
    }

    private void Start()
    {
        if (groupAI != null && player == null)
            player = groupAI.Player;

        MainStateMachine = new StateMachine<EnemyAI, MainStateID>(this);
        MainStateMachine.SetStates(StateFactory.GetMainStates(this, MainStateMachine));
        MainStateMachine.SetState(MainStateID.Seek);
    }

    private void Update()
    {
        MainStateMachine?.Tick();
        UpdateAnimatorSpeed();
    }

    public bool ShouldFlee()
    {
        if (IsTestMode)
            return debugLowHealth;

        return health != null && health.Normalized01 <= fleeHealthThreshold;
    }

    public bool ShouldEnterCombat()
    {
        if (IsTestMode)
            return debugPlayerDetected || (groupAI != null && groupAI.GroupAlerted);

        if (CanSeePlayer())
            return true;

        return groupAI != null && groupAI.GroupAlerted;
    }

    public bool ShouldDefend()
    {
        if (IsTestMode)
            return debugShouldDefend;

        bool lowHealthInMelee = health != null && health.Normalized01 <= defendHealthThreshold && InAttackRange(0.75f);

        bool recoveringInMelee = !CanAttackNow && InAttackRange(0.75f);

        return lowHealthInMelee || recoveringInMelee;
    }

    public bool ShouldFlank()
    {
        if (IsTestMode)
            return debugShouldFlank;

        return groupAI != null && groupAI.ShouldUnitFlank(this, attackRange * 1.8f);
    }

    public bool HasLostPlayer()
    {
        if (IsTestMode)
            return debugPlayerLost;

        return TimeSinceLastSeenPlayer > losePlayerAfter;
    }

    public bool IsWallBroken()
    {
        if (IsTestMode)
            return debugWallBroken;

        return groupAI != null && groupAI.WallBroken;
    }

    public bool CanSeePlayer()
    {
        if (IsTestMode)
            return debugPlayerDetected;

        if (player == null)
            return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 targetEye = player.position + Vector3.up * eyeHeight;
        float dist = Vector3.Distance(eye, targetEye);

        if (dist > detectionRange)
            return false;

        bool visible = true;

        if (Physics.Linecast(eye, targetEye, out RaycastHit hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            visible = hit.transform == player || hit.transform.IsChildOf(player);
        }

        if (visible)
        {
            lastSeenPlayerTime = Time.time;

            if (groupAI != null)
                groupAI.AlertGroup(player.position);
        }

        return visible;
    }

    public bool InAttackRange(float extraRange = 0f)
    {
        if (IsTestMode)
            return debugInAttackRange;

        if (player == null)
            return false;

        float range = attackRange + extraRange;
        return (transform.position - player.position).sqrMagnitude <= range * range;
    }

    public bool IsNear(Vector3 point, float threshold)
    {
        if (mover != null)
            return mover.HasReachedDestination(threshold);

        if (IsTestMode)
            return debugReachedDestination;

        return (transform.position - point).sqrMagnitude <= threshold * threshold;
    }

    public void MoveTo(Vector3 destination, float speedMultiplier = 1f)
    {
        if (VerboseLogs)
        {
            string mode = IsTestMode ? "[AI Debug]" : "[AI Runtime]";
            Debug.Log($"{name} {mode} MoveTo -> {destination}, speed x{speedMultiplier}");
        }

        if (mover != null)
        {
            mover.MoveTo(destination, speedMultiplier);
            return;
        }

        if (VerboseLogs)
            Debug.LogWarning($"{name} has no EnemyMoverBase assigned.");
    }

    public void StopMoving()
    {
        if (VerboseLogs)
        {
            string mode = IsTestMode ? "[AI Debug]" : "[AI Runtime]";
            Debug.Log($"{name} {mode} StopMoving");
        }

        if (mover != null)
        {
            mover.StopMoving();
            return;
        }

        if (VerboseLogs)
            Debug.LogWarning($"{name} has no EnemyMoverBase assigned.");
    }

    public void FaceTarget(Vector3 target)
    {
        if (VerboseLogs)
        {
            string mode = IsTestMode ? "[AI Debug]" : "[AI Runtime]";
            Debug.Log($"{name} {mode} FaceTarget -> {target}");
        }

        if (mover != null)
        {
            mover.FaceTowards(target);
            return;
        }

        if (IsTestMode)
            return;

        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
    }

    public Vector3 GetSearchDestination()
    {
        if (groupAI == null)
            return transform.position;

        if (groupAI.GroupAlerted)
            return groupAI.LastKnownPlayerPosition;

        return groupAI.GetSearchPoint(this);
    }

    public Vector3 GetRetreatDestination()
    {
        if (groupAI != null && groupAI.RegroupPoint != null)
            return groupAI.RegroupPoint.position;

        if (player == null)
            return transform.position;

        Vector3 away = (transform.position - player.position).normalized;
        return transform.position + away * 6f;
    }

    public Vector3 GetFlankDestination()
    {
        if (groupAI != null)
            return groupAI.GetFlankPoint(this, flankRadius);

        if (player == null)
            return transform.position;

        Vector3 side = Vector3.Cross(Vector3.up, (transform.position - player.position).normalized);
        return player.position + side * flankRadius;
    }

    public bool TryAttackPlayer()
    {
        if (!CanAttackNow)
        {
            if (IsTestMode && VerboseLogs)
                Debug.Log($"{name} [AI Debug] TryAttackPlayer blocked by cooldown");
            return false;
        }

        if (IsTestMode)
        {
            lastAttackTime = Time.time;

            if (VerboseLogs)
                Debug.Log($"{name} [AI Debug] TryAttackPlayer SUCCESS");

            return true;
        }

        if (player == null)
            return false;

        StopMoving();
        FaceTarget(player.position);

        if (animator != null)
            animator.SetTrigger(AttackHash);

        lastAttackTime = Time.time;
        return true;
    }

    public bool TryAttackWall()
    {
        if (!CanAttackNow)
        {
            if (IsTestMode && VerboseLogs)
                Debug.Log($"{name} [AI Debug] TryAttackWall blocked by cooldown");
            return false;
        }

        if (IsTestMode)
        {
            lastAttackTime = Time.time;
            debugWallHitCount++;

            if (VerboseLogs)
                Debug.Log($"{name} [AI Debug] TryAttackWall -> hit {debugWallHitCount}/{debugWallHitsToBreak}");

            if (!debugWallBroken && debugWallHitCount >= debugWallHitsToBreak)
            {
                debugWallBroken = true;

                if (VerboseLogs)
                    Debug.Log($"{name} [AI Debug] Wall now considered broken");
            }

            return true;
        }

        if (groupAI == null || groupAI.WallTarget == null)
            return false;

        StopMoving();
        FaceTarget(groupAI.WallTarget.position);

        if (animator != null)
            animator.SetTrigger(AttackHash);

        BreakableWall wall = groupAI.WallTarget.GetComponent<BreakableWall>();
        if (wall != null)
            wall.TakeHit(wallDamage);

        lastAttackTime = Time.time;
        return true;
    }

    public void SetDefending(bool value)
    {
        if (animator != null)
            animator.SetBool(DefendHash, value);

        if (IsTestMode && VerboseLogs)
            Debug.Log($"{name} [AI Debug] SetDefending -> {value}");
    }

    public void AnimationEvent_DealDamage()
    {
        if (IsTestMode)
        {
            if (VerboseLogs)
                Debug.Log($"{name} [AI Debug] AnimationEvent_DealDamage");
            return;
        }

        if (player == null)
            return;

        if (!InAttackRange(0.75f))
            return;

        IDamageable damageable = player.GetComponentInParent<IDamageable>();
        if (damageable == null)
            damageable = player.GetComponent<IDamageable>();

        damageable?.TakeDamage(attackDamage);
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator == null)
            return;

        float speed = mover != null ? mover.CurrentSpeed : 0f;
        animator.SetFloat(SpeedHash, speed);
    }

    public void InitializeGroup(EnemyGroupAI newGroup)
    {
        if (groupAI == newGroup)
            return;

        if (groupAI != null)
            groupAI.Unregister(this);

        groupAI = newGroup;

        if (groupAI != null)
        {
            groupAI.Register(this);

            if (player == null)
                player = groupAI.Player;
        }
    }
}