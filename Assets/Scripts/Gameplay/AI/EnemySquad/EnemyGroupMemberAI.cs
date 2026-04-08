using UnityEngine;
#if UNITY_AI_NAVIGATION
using UnityEngine.AI;
#endif

/// <summary>
/// Per-enemy fuzzy-driven squad behaviour: fight player, flee when hurt, break doors, attack magic stone,
/// delegate when others already near player, separation from allies, search toward last known player.
/// Requires <see cref="EnemySquadCoordinator"/> in scene; wire the same coordinator on all 3 members.
/// </summary>
[DisallowMultipleComponent]
public class EnemyGroupMemberAI : MonoBehaviour
{
    public enum SquadRole
    {
        EngagePlayer,
        AttackDoor,
        AttackStone,
        Flee,
        Search
    }

    [Header("Setup")]
    [SerializeField] private EnemySquadCoordinator coordinator;
    [SerializeField] private Health selfHealth;
    [SerializeField] private CharacterController characterController;
#if UNITY_AI_NAVIGATION
    [SerializeField] private NavMeshAgent navMeshAgent;
#endif
    [SerializeField] private Animator animator;
    [SerializeField] private string animatorSpeedParam = "Speed";
    [Tooltip("Must match Animator Controller bool (Brute uses 'IsGrounded' on Attack transitions).")]
    [SerializeField] private bool driveAnimatorGroundedBool = true;
    [SerializeField] private string animatorIsGroundedParameter = "IsGrounded";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float fleeSpeedMultiplier = 1.35f;
    [SerializeField] private float separationRadius = 2.2f;
    [SerializeField] private float separationWeight = 0.85f;
    [SerializeField] private float arriveDistance = 1.35f;
    [SerializeField] private float searchMoveDistance = 22f;

    [Header("Custom pathfinding (optional)")]
    [Tooltip("If assigned (or auto-found), movement uses the project's NodeGraph-based mover instead of direct steering/NavMesh.")]
    [SerializeField] private EnemyMover customMover;
    [Tooltip("How often to recompute a path while chasing a goal using the custom mover.")]
    [SerializeField, Min(0.05f)] private float customPathRecalcInterval = 0.5f;
    [SerializeField, Min(0f)] private float customPathRecalcDistance = 1.0f;

    [Header("Fuzzy / behaviour")]
    [Tooltip("If missing HP fraction is above this, flee desire is high.")]
    [SerializeField] private float fleeHpMissingPeak = 0.55f;
    [SerializeField] private float delegateWhenNearPlayerCount = 2f;
    [Tooltip("Seconds before re-evaluating role (hysteresis).")]
    [SerializeField] private float roleSwitchCooldown = 0.35f;

    [Header("Wave squad role override (optional)")]
    [Tooltip("If enabled, this member will always try to fight the player (ignores fuzzy role switching).")]
    [SerializeField] private bool forceEngagePlayerRole;

    [Tooltip("If enabled, this member will always try objective logic: if palace is breached and stone exists -> AttackStone, else AttackDoor (if any), otherwise fight player.")]
    [SerializeField] private bool forceObjectiveRole;

    [Header("Objectives (DPS)")]
    [SerializeField] private float damagePerSecondToObjectives = 18f;
    [SerializeField] private float objectiveDamageRange = 2.1f;

    [Header("Melee chain (player-style)")]
    [Tooltip("Same stack as Player: configure Attack[] + Animation Events OpenComboWindow / CreateAOE on this enemy's Animator object.")]
    [SerializeField] private AttackManager attackManager;
    [Tooltip("Horizontal distance to current goal (player / door / stone) at or below which we face the target and feed the AttackManager combo (TryAttack), same damage pipeline as player AOE.")]
    [SerializeField] private float meleeEngageRange = 2.25f;
    [SerializeField, Min(1f)] private float faceTargetTurnSpeedDegrees = 540f;
    [Tooltip("How often to call TryAttack while in range (buffers next combo hits like repeated player input).")]
    [SerializeField, Min(0.02f)] private float attackInputRepeatInterval = 0.12f;
    [Tooltip("If true, door/stone damage uses melee AOE only when AttackManager is set; if false, keep tick damage outside melee range or as fallback inside range.")]
    [SerializeField] private bool useMeleeComboForObjectives = true;
    [Tooltip("While a melee animation is playing (AttackManager.InAttackState), do not locomote.")]
    [SerializeField] private bool pauseLocomotionDuringAttackAnim = true;
    [Tooltip("If true, once within meleeEngageRange the enemy stops advancing (prevents sticking to the target).")]
    [SerializeField] private bool stopAtMeleeRange = true;
    [Tooltip("If no AttackManager is present, still apply tick damage to the player when within meleeEngageRange (so AI is not harmless).")]
    [SerializeField] private bool fallbackTickDamageToPlayerWithoutAttackManager = true;

    public SquadRole CurrentRole { get; private set; } = SquadRole.Search;

    private SquadRole _desiredRole;
    private float _nextRoleSwitchTime;
    private float _nextMeleeInputTime;
    private Vector3 _velocity;
    private int _isGroundedParamHash;
    private float _nextCustomPathRecalcTime;
    private Vector3 _lastCustomPathGoal;

    private void Awake()
    {
        if (selfHealth == null)
            selfHealth = GetComponent<Health>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
#if UNITY_AI_NAVIGATION
        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();
#endif
        if (animator == null)
            animator = GetComponentInChildren<Animator>(includeInactive: true);
        if (customMover == null)
            customMover = GetComponent<EnemyMover>();
        if (attackManager == null)
            attackManager = GetComponentInChildren<AttackManager>(true);

        _isGroundedParamHash = Animator.StringToHash(animatorIsGroundedParameter);
    }

    private void OnEnable()
    {
        if (coordinator != null)
            coordinator.RegisterMember(this);
    }

    private void OnDisable()
    {
        attackManager?.CancelBufferedComboNow();
        if (coordinator != null)
            coordinator.UnregisterMember(this);
    }

    private void Update()
    {
        if (coordinator == null || selfHealth == null)
            return;
        if (selfHealth.IsDead)
            return;

        if (forceEngagePlayerRole || forceObjectiveRole)
            EvaluateForcedRole();
        else
            EvaluateFuzzyRole();
        ExecuteCurrentRole();
        UpdateAnimatorGrounded();
    }

    /// <summary>Called by wave spawner to wire a runtime-created squad coordinator and optionally lock a role.</summary>
    public void InitializeForWaveSquad(EnemySquadCoordinator squadCoordinator, bool engagePlayer, bool objectiveMember)
    {
        coordinator = squadCoordinator;
        forceEngagePlayerRole = engagePlayer;
        forceObjectiveRole = objectiveMember;
    }

    private void EvaluateForcedRole()
    {
        if (coordinator == null)
            return;

        if (forceEngagePlayerRole && !forceObjectiveRole)
        {
            CurrentRole = SquadRole.EngagePlayer;
            return;
        }

        // Objective member: stone if possible, else door, else player.
        bool palaceOpen = coordinator.IsPalaceBreached();
        DoorBreakable door = coordinator.GetBestDoorToAttack(transform.position);
        Transform stone = coordinator.GetMagicStoneTransform();

        if (palaceOpen && stone != null)
        {
            CurrentRole = SquadRole.AttackStone;
            return;
        }

        if (!palaceOpen && door != null && !door.IsBroken)
        {
            CurrentRole = SquadRole.AttackDoor;
            return;
        }

        // Fallback: if no door/stone is configured, just fight player.
        CurrentRole = SquadRole.EngagePlayer;
    }

    private void UpdateAnimatorGrounded()
    {
        if (animator == null || !driveAnimatorGroundedBool)
            return;

        bool grounded = ComputeIsGroundedForAnimator();

        // Always drive the project's canonical grounded bool name ("isGrounded") too,
        // because animator parameter name casing is case-sensitive in Unity.
        animator.SetBool(AnimParams.IsGrounded, grounded);

        // Additionally drive the configured parameter (in case this enemy controller uses a different bool name).
        if (_isGroundedParamHash != 0 && _isGroundedParamHash != AnimParams.IsGrounded)
            animator.SetBool(_isGroundedParamHash, grounded);
    }

    private bool ComputeIsGroundedForAnimator()
    {
        if (characterController != null && characterController.enabled)
            return characterController.isGrounded;

#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            if (navMeshAgent.velocity.y > 0.35f)
                return false;
            Vector3 o = transform.position + Vector3.up * 0.2f;
            if (Physics.Raycast(o, Vector3.down, 0.55f, ~0, QueryTriggerInteraction.Ignore))
                return true;
            return true;
        }
#endif

        const float probe = 0.45f;
        Vector3 origin = transform.position + Vector3.up * 0.15f;
        return Physics.Raycast(origin, Vector3.down, probe, ~0, QueryTriggerInteraction.Ignore);
    }

    private void EvaluateFuzzyRole()
    {
        if (Time.time < _nextRoleSwitchTime)
            return;

        Transform player = coordinator.PlayerTransform;
        float hpNorm = selfHealth.MaxHealth > 0 ? selfHealth.CurrentHealth / selfHealth.MaxHealth : 0f;
        float missingHp = 1f - Mathf.Clamp01(hpNorm);

        float fleeScore = FuzzyMath.Tri(missingHp, 0.25f, fleeHpMissingPeak, 0.95f);

        float distPlayer = player != null ? HorizontalDist(transform.position, player.position) : searchMoveDistance;
        float playerClose = Mathf.Clamp01(1f - distPlayer / 12f);

        bool palaceOpen = coordinator.IsPalaceBreached();
        int othersNearPlayer = coordinator.CountMembersNearPlayerExcluding(this);
        float engageR = coordinator.EngagePlayerRadius;
        bool imEngagingPlayer = player != null && distPlayer <= engageR;
        float delegateScore = FuzzyMath.Tri(othersNearPlayer, delegateWhenNearPlayerCount - 0.6f, delegateWhenNearPlayerCount, delegateWhenNearPlayerCount + 0.6f);
        if (imEngagingPlayer)
            delegateScore *= 0.12f;

        DoorBreakable door = coordinator.GetBestDoorToAttack(transform.position);
        Transform stone = coordinator.GetMagicStoneTransform();
        bool hasDoor = door != null && !door.IsBroken;
        bool hasStone = stone != null;

        float fightScore = playerClose * (1f - fleeScore) * (palaceOpen ? 0.85f : 1f);
        if (othersNearPlayer >= 2 && !palaceOpen && !imEngagingPlayer)
            fightScore *= 0.35f;
        if (othersNearPlayer >= 2 && palaceOpen && hasStone && !imEngagingPlayer)
            fightScore *= 0.35f;

        float doorScore = FuzzyMath.And(palaceOpen ? 0f : 1f, hasDoor ? 1f : 0f, delegateScore, 1f - fleeScore * 0.85f);
        float stoneScore = FuzzyMath.And(palaceOpen ? 1f : 0f, hasStone ? 1f : 0f, delegateScore, 1f - fleeScore * 0.85f);

        float searchScore = (1f - playerClose) * 0.55f;
        if (player == null)
            searchScore = 0.9f;

        float best = -1f;
        _desiredRole = SquadRole.Search;

        if (fleeScore > best) { best = fleeScore; _desiredRole = SquadRole.Flee; }
        if (fightScore > best) { best = fightScore; _desiredRole = SquadRole.EngagePlayer; }
        if (doorScore > best) { best = doorScore; _desiredRole = SquadRole.AttackDoor; }
        if (stoneScore > best) { best = stoneScore; _desiredRole = SquadRole.AttackStone; }
        if (searchScore > best) { best = searchScore; _desiredRole = SquadRole.Search; }

        if (_desiredRole != CurrentRole)
        {
            if (IsOffensiveRole(CurrentRole) && !IsOffensiveRole(_desiredRole))
                attackManager?.CancelBufferedComboNow();

            CurrentRole = _desiredRole;
            _nextRoleSwitchTime = Time.time + roleSwitchCooldown;
        }
    }

    private static bool IsOffensiveRole(SquadRole r) =>
        r == SquadRole.EngagePlayer || r == SquadRole.AttackDoor || r == SquadRole.AttackStone;

    private void ExecuteCurrentRole()
    {
        Vector3 sep = coordinator.GetSeparationHint(transform.position, separationRadius, separationWeight);
        Transform player = coordinator.PlayerTransform;

        switch (CurrentRole)
        {
            case SquadRole.Flee:
                if (player != null)
                    MoveAwayFrom(player.position, moveSpeed * fleeSpeedMultiplier, sep);
                break;
            case SquadRole.EngagePlayer:
                if (player != null)
                {
                    // Spread attackers around the player (avoid all stacking on one angle).
                    float approachRadius = Mathf.Max(meleeEngageRange * 1.05f, 2.2f);
                    Vector3 movePoint = coordinator.GetEngageApproachPoint(this, approachRadius, 45f);
                    ExecuteCombatAt(movePoint, player.position, null, moveSpeed, sep, player.gameObject);
                }
                break;
            case SquadRole.AttackDoor:
                {
                    DoorBreakable d = coordinator.GetBestDoorToAttack(transform.position);
                    if (d != null)
                        ExecuteCombatAt(d.transform.position, d.transform.position, d.gameObject, moveSpeed, sep);
                    break;
                }
            case SquadRole.AttackStone:
                {
                    Transform stone = coordinator.GetMagicStoneTransform();
                    if (stone != null)
                        ExecuteCombatAt(stone.position, stone.position, stone.gameObject, moveSpeed, sep);
                    break;
                }
            case SquadRole.Search:
                if (coordinator.HasLastKnownPlayer)
                    MoveToward(coordinator.LastKnownPlayerPosition, moveSpeed * 0.85f, sep);
                break;
        }

        // If we are using the project's custom mover, it already drives Speed/IsGrounded.
        if (customMover != null)
            return;

        if (animator != null && !string.IsNullOrEmpty(animatorSpeedParam))
        {
#if UNITY_AI_NAVIGATION
            float spd = navMeshAgent != null && navMeshAgent.isOnNavMesh ? navMeshAgent.velocity.magnitude : _velocity.magnitude;
#else
            float spd = _velocity.magnitude;
#endif
            animator.SetFloat(animatorSpeedParam, spd);
        }
    }

    /// <summary>
    /// Move toward a point, but aim/attack a (potentially different) target point.
    /// This lets us spread attackers around the player while still facing/attacking the player.
    /// <paramref name="damageTickTarget"/> — door/stone tick DPS when appropriate.
    /// <paramref name="playerFallbackMelee"/> — when engaging the player without <see cref="attackManager"/>, optional tick damage.
    /// </summary>
    private void ExecuteCombatAt(Vector3 moveTarget, Vector3 aimTarget, GameObject damageTickTarget, float speed, Vector3 sep, GameObject playerFallbackMelee = null)
    {
        float dist = HorizontalDist(transform.position, aimTarget);
        bool inMeleeBand = dist <= meleeEngageRange;
        bool useAttackManager = attackManager != null;

        if (inMeleeBand)
        {
            FaceTowards(aimTarget);
            if (useAttackManager)
                TryFeedMeleeCombo();

            // When within melee range we typically stop advancing to avoid sticking to the target.
            bool shouldStop = stopAtMeleeRange || (useAttackManager && pauseLocomotionDuringAttackAnim && attackManager.InAttackState());
            if (shouldStop)
            {
                _velocity = Vector3.zero;
#if UNITY_AI_NAVIGATION
                if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.stoppingDistance = Mathf.Max(0.05f, meleeEngageRange * 0.9f);
                    navMeshAgent.isStopped = true;
                    navMeshAgent.ResetPath();
                }
#endif
            }
            else
            {
#if UNITY_AI_NAVIGATION
                if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
                    navMeshAgent.isStopped = false;
#endif
                MoveToward(moveTarget, speed, sep);
            }

            if (damageTickTarget != null && (!useMeleeComboForObjectives || !useAttackManager))
                TryDamageTarget(damageTickTarget);

            if (playerFallbackMelee != null && !useAttackManager && fallbackTickDamageToPlayerWithoutAttackManager)
                TryDamageTarget(playerFallbackMelee);
        }
        else
        {
#if UNITY_AI_NAVIGATION
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.stoppingDistance = 0f;
                navMeshAgent.isStopped = false;
            }
#endif
            MoveToward(moveTarget, speed, sep);

            bool tickDamage = damageTickTarget != null &&
                              (!useAttackManager || !useMeleeComboForObjectives);
            if (tickDamage)
                TryDamageTarget(damageTickTarget);
        }
    }

    private void FaceTowards(Vector3 worldTarget)
    {
        Vector3 d = worldTarget - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-6f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(d.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, faceTargetTurnSpeedDegrees * Time.deltaTime);
    }

    private void TryFeedMeleeCombo()
    {
        if (attackManager == null)
            return;

        if (Time.time < _nextMeleeInputTime)
            return;

        _nextMeleeInputTime = Time.time + attackInputRepeatInterval;
        attackManager.TryAttack();
    }

    private void TryDamageTarget(GameObject target)
    {
        if (target == null || DamageService.Instance == null)
            return;
        float dist = HorizontalDist(transform.position, target.transform.position);
        if (dist > objectiveDamageRange)
            return;
        float dmg = damagePerSecondToObjectives * Time.deltaTime;
        DamageService.Instance.DealDamage(gameObject, target, dmg);
    }

    private void MoveToward(Vector3 world, float speed, Vector3 separation)
    {
        if (TryMoveWithCustomPathfinding(world))
            return;

        Vector3 flat = world - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < arriveDistance * arriveDistance)
        {
            _velocity = Vector3.zero;
#if UNITY_AI_NAVIGATION
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
                navMeshAgent.ResetPath();
#endif
            return;
        }

        Vector3 dir = flat.normalized + separation;
        if (dir.sqrMagnitude < 0.001f)
            dir = flat.normalized;
        else
            dir.Normalize();

        _velocity = dir * speed;

#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            Vector3 sep = new Vector3(separation.x, 0f, separation.z);
            navMeshAgent.speed = speed;
            navMeshAgent.updateRotation = true;
            navMeshAgent.SetDestination(world + sep * 0.6f);
            return;
        }
#endif
        ApplyMove(dir * speed, speed);
    }

    private void MoveAwayFrom(Vector3 world, float speed, Vector3 separation)
    {
#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            navMeshAgent.isStopped = false;
#endif
        Vector3 flat = transform.position - world;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f)
            flat = transform.forward * -1f;
        Vector3 dir = flat.normalized + separation;
        dir.Normalize();
        _velocity = dir * speed;

#if UNITY_AI_NAVIGATION
        // (custom mover pathfinding is used below for non-navmesh)
#endif
        if (TryMoveWithCustomPathfinding(transform.position + dir * 8f))
            return;

#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.speed = speed;
            navMeshAgent.updateRotation = true;
            navMeshAgent.SetDestination(transform.position + dir * 8f);
            return;
        }
#endif
        ApplyMove(dir * speed, speed);
    }

    private bool TryMoveWithCustomPathfinding(Vector3 goal)
    {
        if (customMover == null)
            return false;

        // Keep the custom mover's speed aligned to this AI's speed (EnemyMover exposes serialized speed; no setter).
        // We just drive path and let EnemyMover handle actual displacement/animator.

        float dist = HorizontalDist(transform.position, goal);
        if (dist <= arriveDistance)
        {
            _velocity = Vector3.zero;
            return true;
        }

        if (Time.time >= _nextCustomPathRecalcTime || Vector3.Distance(_lastCustomPathGoal, goal) >= customPathRecalcDistance)
        {
            _nextCustomPathRecalcTime = Time.time + customPathRecalcInterval;
            _lastCustomPathGoal = goal;
            customMover.RecalculatePathFinding(goal);
        }

        customMover.Move();
        return true;
    }

    private void ApplyMove(Vector3 planarVelocity, float speed)
    {
        if (characterController != null)
        {
            Vector3 motion = planarVelocity * Time.deltaTime;
            motion.y = Physics.gravity.y * Time.deltaTime;
            characterController.Move(motion);
            if (planarVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 look = planarVelocity;
                look.y = 0f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), 10f * Time.deltaTime);
            }
        }
        else
        {
            transform.position += planarVelocity * Time.deltaTime;
        }
    }

    private static float HorizontalDist(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
