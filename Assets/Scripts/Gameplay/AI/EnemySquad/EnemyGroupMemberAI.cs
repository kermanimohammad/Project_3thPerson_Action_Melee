using System.Text;
using UnityEngine;
using TMPro;
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
    [SerializeField] private EnemyStamina stamina;
    [SerializeField] private EnemyHitMove enemyHitMove;
    [SerializeField] private CharacterDefense characterDefense;
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
    [Header("Low stamina walk")]
    [Tooltip("If EnemyStamina.Normalized is below this, enemies will walk instead of run.")]
    [SerializeField, Range(0f, 1f)] private float lowStaminaWalkThreshold01 = 0.2f;
    [Tooltip("Speed multiplier applied while walking due to low stamina.")]
    [SerializeField, Min(0f)] private float lowStaminaWalkSpeedMultiplier = 0.45f;

    [Header("Engage player: HP-based pace & defense")]
    [Tooltip("Above this HP fraction: move at run speed toward player.")]
    [SerializeField, Range(0f, 1f)] private float engageHealthyHpThreshold = 0.5f;
    [Tooltip("At or below this HP fraction: mostly defend in melee, with short attack bursts.")]
    [SerializeField, Range(0f, 1f)] private float engageDefensiveHpThreshold = 0.2f;
    [SerializeField, Min(0.01f)] private float engageRunSpeedMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float engageCautiousWalkSpeedMultiplier = 0.45f;
    [SerializeField, Min(0.1f)] private float engageLowHpDefendPhaseMinSeconds = 1f;
    [SerializeField, Min(0.1f)] private float engageLowHpDefendPhaseMaxSeconds = 2.2f;
    [SerializeField, Min(0.05f)] private float engageLowHpAttackPhaseMinSeconds = 0.28f;
    [SerializeField, Min(0.05f)] private float engageLowHpAttackPhaseMaxSeconds = 0.55f;

    [SerializeField] private float separationRadius = 2.2f;
    [SerializeField] private float separationWeight = 0.85f;
    [Tooltip("Smooth squad separation hint to reduce rapid heading flips when allies jostle.")]
    [SerializeField, Min(0f)] private float separationResponseSmoothTime = 0.12f;
#if UNITY_AI_NAVIGATION
    [Tooltip("How often NavMeshAgent destination may change (reduces spin from jittery goals).")]
    [SerializeField, Min(0.05f)] private float navMeshGoalRefreshInterval = 0.18f;
    [SerializeField, Min(0f)] private float navMeshGoalMinPlanarDelta = 0.45f;
    [Tooltip("Scales how much separation shifts the NavMesh destination (smaller = stabler path).")]
    [SerializeField, Min(0f)] private float navMeshSeparationDestinationScale = 0.35f;
#endif
    [SerializeField] private float arriveDistance = 1.35f;
    [SerializeField] private float searchMoveDistance = 22f;

    [Header("Custom pathfinding (optional)")]
    [Tooltip("If assigned (or auto-found), movement uses the project's NodeGraph-based mover instead of direct steering/NavMesh.")]
    [SerializeField] private EnemyMover customMover;
    [Tooltip("How often to recompute a path while chasing a goal using the custom mover.")]
    [SerializeField, Min(0.05f)] private float customPathRecalcInterval = 0.5f;
    [SerializeField, Min(0f)] private float customPathRecalcDistance = 1.0f;
    [Tooltip("Smooth the world position fed into graph pathfinding. Engage slots orbit the player and jitter each frame; smoothing avoids constant path resets and in-place spinning.")]
    [SerializeField, Min(0f)] private float customPathGoalSmoothTime = 0.22f;
    [Tooltip("If the raw move goal jumps farther than this (m), snap the smoothed path goal (role switch / teleport).")]
    [SerializeField, Min(1f)] private float customPathGoalTeleportSnapDistance = 14f;

    [Header("Fuzzy / behaviour")]
    [Tooltip("If missing HP fraction is above this, flee desire is high.")]
    [SerializeField] private float fleeHpMissingPeak = 0.55f;
    [SerializeField] private float delegateWhenNearPlayerCount = 2f;
    [Tooltip("Seconds before re-evaluating role (hysteresis).")]
    [SerializeField] private float roleSwitchCooldown = 0.35f;
    [Tooltip("Fuzzy AI: new role must lead current role's score by at least this much to switch (stops tight loops between fight / door / stone / search).")]
    [SerializeField, Range(0f, 0.45f)] private float fuzzyRoleSwitchMinScoreAdvantage = 0.12f;

    [Header("Steering (decision vs separation)")]
    [Tooltip("Smooth final planar heading after goal + separation. Reduces spinning when ally avoidance and chase direction fight each frame.")]
    [SerializeField, Min(0f)] private float moveDirectionSmoothTime = 0.22f;

    [Header("Wave squad role override (optional)")]
    [Tooltip("If enabled, this member will always try to fight the player (ignores fuzzy role switching).")]
    [SerializeField] private bool forceEngagePlayerRole;

    [Tooltip("If enabled, this member will always try objective logic: if palace is breached and stone exists -> AttackStone, else AttackDoor (if any), otherwise fight player.")]
    [SerializeField] private bool forceObjectiveRole;

    [Header("Objective: interrupt for nearby player")]
    [Tooltip("Horizontal distance at which an enemy focused on a door/stone switches to EngagePlayer. If 0, uses coordinator EngagePlayerRadius * 1.5.")]
    [SerializeField, Min(0f)] private float objectivePlayerInterruptRadius;
    [Tooltip("Extra distance beyond interrupt radius before returning to the door/stone (reduces oscillation).")]
    [SerializeField, Min(0f)] private float objectivePlayerInterruptReleaseMargin = 1.25f;

    [Header("Objectives (DPS)")]
    [SerializeField] private float damagePerSecondToObjectives = 18f;
    [SerializeField] private float objectiveDamageRange = 2.1f;

    [Header("Melee chain (player-style)")]
    [Tooltip("Same stack as Player: configure Attack[] + Animation Events OpenComboWindow / CreateAOE on this enemy's Animator object.")]
    [SerializeField] private AttackManager attackManager;
    [Tooltip("Horizontal distance to current goal (player / door / stone) at or below which we face the target and feed the AttackManager combo (TryAttack), same damage pipeline as player AOE.")]
    [SerializeField] private float meleeEngageRange = 2.25f;
    [Tooltip("Max degrees per second when rotating to face the aim target in melee (FaceTowards).")]
    [SerializeField, Min(1f)] private float faceTargetTurnSpeedDegrees = 540f;
    [Tooltip("When moving with CharacterController, how fast yaw blends toward velocity direction (Slerp t = this * deltaTime). Higher = snappier; lower reduces twitching when velocity jitters.")]
    [SerializeField, Min(0.1f)] private float moveFacingSlerpSharpness = 10f;
    [Tooltip("How often to call TryAttack while in range (buffers next combo hits like repeated player input).")]
    [SerializeField, Min(0.02f)] private float attackInputRepeatInterval = 0.12f;
    [Tooltip("If true, door/stone damage uses melee AOE only when AttackManager is set; if false, keep tick damage outside melee range or as fallback inside range.")]
    [SerializeField] private bool useMeleeComboForObjectives = true;
    [Tooltip("While a melee animation is playing (AttackManager.InAttackState), do not locomote.")]
    [SerializeField] private bool pauseLocomotionDuringAttackAnim = true;
    [Tooltip("If true, once within meleeEngageRange the enemy stops advancing (prevents sticking to the target).")]
    [SerializeField] private bool stopAtMeleeRange = true;
    [Tooltip("When stopAtMeleeRange is on for EngagePlayer: keep holding position until the player is this much farther than meleeEngageRange before chasing again. Stops move/idle animation jitter at the edge.")]
    [SerializeField, Min(0f)] private float meleeChaseResumeHysteresis = 0.42f;
    [Tooltip("Smooth animator Speed toward the real locomotion speed (reduces twitch when speed hovers near zero). 0 = no smoothing.")]
    [SerializeField, Min(0f)] private float animatorLocomotionSpeedSmoothTime = 0.1f;
    [Tooltip("If no AttackManager is present, still apply tick damage to the player when within meleeEngageRange (so AI is not harmless).")]
    [SerializeField] private bool fallbackTickDamageToPlayerWithoutAttackManager = true;

    [Header("Debug display (optional)")]
    [Tooltip("World-space TextMeshPro or UI TMP_Text. Shows target, role, and locomotion each frame, e.g. \"Player - EngagePlayer - Run - NavMesh\".")]
    [SerializeField] private TMP_Text decisionDebugLabel;
    [SerializeField] private bool showDecisionDebug = true;

    public SquadRole CurrentRole { get; private set; } = SquadRole.Search;

    private SquadRole _desiredRole;
    private float _nextRoleSwitchTime;
    private float _nextMeleeInputTime;
    private Vector3 _velocity;
    private int _isGroundedParamHash;
    private float _nextCustomPathRecalcTime;
    private Vector3 _lastCustomPathGoal;
    private Vector3 _customPathGoalSmoothed;
    private Vector3 _customPathGoalSmoothVelocity;
    private bool _customPathGoalSmoothActive;
    private Vector3 _smoothedSeparation;
    private Vector3 _sepSmoothVelocity;
    private Vector3 _smoothedPlanarMoveDirXZ;
    private Vector3 _moveDirSmoothVelocity;
#if UNITY_AI_NAVIGATION
    private Vector3 _lastNavMeshDestination;
    private bool _hasNavMeshDestination;
    private float _nextNavMeshGoalTime;
#endif
    /// <summary>Extra speed scale for <see cref="EnemyMover"/> (engage run vs walk).</summary>
    private float _engagePathSpeedScale = 1f;

    private bool _engageLowHpCombatInitialized;
    private bool _engageLowHpDefendPhase = true;
    private float _engageLowHpPhaseEndTime;

    /// <summary>True while we temporarily switched from door/stone to fighting the player until they leave range or die.</summary>
    private bool _objectiveInterruptLatch;

    /// <summary>When true, <see cref="EnemyMover.Move"/> ran this frame and drove animator Speed; otherwise squad AI may set Speed from <see cref="_velocity"/>.</summary>
    private bool _customMoverDroveAnimThisFrame;

    /// <summary>Sticky melee band for EngagePlayer + stopAtMeleeRange (Schmitt trigger vs raw dist &lt;= meleeEngageRange).</summary>
    private bool _engageMeleeBandLatch;
    private float _animatorSpeedSmoothVelocity;

    private void Awake()
    {
        if (selfHealth == null)
            selfHealth = GetComponent<Health>();
        if (stamina == null)
            stamina = GetComponent<EnemyStamina>();
        if (enemyHitMove == null)
            enemyHitMove = GetComponent<EnemyHitMove>();
        if (characterDefense == null)
            characterDefense = GetComponent<CharacterDefense>();
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

        Vector3 f = transform.forward;
        f.y = 0f;
        _smoothedPlanarMoveDirXZ = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
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
        {
            RefreshDecisionDebugLabelUnavailable("NoCoordinator");
            return;
        }

        if (selfHealth.IsDead)
        {
            RefreshDecisionDebugLabelUnavailable("Dead");
            return;
        }

        if (forceEngagePlayerRole || forceObjectiveRole)
            EvaluateForcedRole();
        else
            EvaluateFuzzyRole();
        _engagePathSpeedScale = 1f;
        ExecuteCurrentRole();
        UpdateStamina();
        UpdateAnimatorGrounded();
        RefreshDecisionDebugLabel();
    }

    private void UpdateStamina()
    {
        if (stamina == null)
            return;

        // Drain while moving. We approximate speed01 from our internal velocity magnitude.
        float speed01 = moveSpeed > 0.01f ? Mathf.Clamp01(_velocity.magnitude / moveSpeed) : 0f;
        stamina.SpendMove(speed01, Time.deltaTime);
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

        bool hasPlayer = coordinator.PlayerTransform != null;

        // Engage members should ALWAYS prioritize fighting the player while the player exists.
        // Only the objective member should be diverted to doors/stone.
        if (forceEngagePlayerRole && !forceObjectiveRole)
        {
            CurrentRole = hasPlayer ? SquadRole.EngagePlayer : SquadRole.Search;
            return;
        }

        if (forceObjectiveRole)
        {
            if (MaintainObjectivePlayerInterrupt(hasPlayer) || TryBeginObjectivePlayerInterrupt(hasPlayer, eligible: true))
            {
                CurrentRole = SquadRole.EngagePlayer;
                return;
            }
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

        // Objective member fallback: if no door/stone is configured, fight player if possible; otherwise search.
        CurrentRole = hasPlayer ? SquadRole.EngagePlayer : SquadRole.Search;
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

    /// <summary>
    /// While playing GetHit (directional hit blend tree) or during scripted hit knockback, do not steer/navigate.
    /// </summary>
    private bool ShouldSuppressLocomotionForHitReaction()
    {
        if (enemyHitMove != null && enemyHitMove.IsDisplacementActive)
            return true;

        if (animator == null)
            return false;

        const int layer = 0;
        var cur = animator.GetCurrentAnimatorStateInfo(layer);
        if (cur.IsName("GetHit"))
            return true;

        if (animator.IsInTransition(layer))
        {
            var next = animator.GetNextAnimatorStateInfo(layer);
            if (next.IsName("GetHit"))
                return true;
        }

        return false;
    }

    private void EvaluateFuzzyRole()
    {
        if (Time.time < _nextRoleSwitchTime)
            return;

        Transform player = coordinator.PlayerTransform;
        bool hasPlayer = player != null;

        if (MaintainObjectivePlayerInterrupt(hasPlayer))
        {
            SquadRole prev = CurrentRole;
            CurrentRole = SquadRole.EngagePlayer;
            _desiredRole = SquadRole.EngagePlayer;
            if (prev != SquadRole.EngagePlayer)
                _nextRoleSwitchTime = Time.time + roleSwitchCooldown;
            return;
        }

        // If player is dead, prioritize objectives instead of continuing to "search".
        if (player == null)
        {
            bool palaceOpenNoPlayer = coordinator.IsPalaceBreached();
            DoorBreakable doorNoPlayer = coordinator.GetBestDoorToAttack(transform.position);
            Transform stoneNoPlayer = coordinator.GetMagicStoneTransform();
            if (palaceOpenNoPlayer && stoneNoPlayer != null) { CurrentRole = SquadRole.AttackStone; return; }
            if (!palaceOpenNoPlayer && doorNoPlayer != null && !doorNoPlayer.IsBroken) { CurrentRole = SquadRole.AttackDoor; return; }
            // No objectives: keep searching/idle.
            CurrentRole = SquadRole.Search;
            return;
        }

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

        bool objectiveEligible =
            _desiredRole == SquadRole.AttackDoor
            || _desiredRole == SquadRole.AttackStone
            || CurrentRole == SquadRole.AttackDoor
            || CurrentRole == SquadRole.AttackStone;
        if (TryBeginObjectivePlayerInterrupt(hasPlayer, objectiveEligible))
            _desiredRole = SquadRole.EngagePlayer;

        float RoleScore(SquadRole r) => r switch
        {
            SquadRole.Flee => fleeScore,
            SquadRole.EngagePlayer => fightScore,
            SquadRole.AttackDoor => doorScore,
            SquadRole.AttackStone => stoneScore,
            SquadRole.Search => searchScore,
            _ => 0f
        };

        if (_desiredRole != CurrentRole
            && RoleScore(_desiredRole) - RoleScore(CurrentRole) < fuzzyRoleSwitchMinScoreAdvantage)
        {
            _desiredRole = CurrentRole;
        }

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

    /// <summary>
    /// While engaging the player with very low HP: alternate long defend windows with short attack windows.
    /// Returns whether <see cref="TryFeedMeleeCombo"/> should run.
    /// </summary>
    private bool UpdateEngagePlayerLowHpCombat(bool inMeleeBand)
    {
        if (characterDefense == null)
            return true;

        float hp = GetSelfHpNormalized();
        if (hp > engageDefensiveHpThreshold)
        {
            _engageLowHpCombatInitialized = false;
            if (characterDefense.IsDefending)
                characterDefense.StopDefend();
            return true;
        }

        if (!inMeleeBand)
        {
            if (characterDefense.IsDefending)
                characterDefense.StopDefend();
            return true;
        }

        if (!_engageLowHpCombatInitialized)
        {
            _engageLowHpCombatInitialized = true;
            _engageLowHpDefendPhase = true;
            _engageLowHpPhaseEndTime = Time.time + Random.Range(engageLowHpDefendPhaseMinSeconds, engageLowHpDefendPhaseMaxSeconds);
            attackManager?.CancelBufferedComboNow();
            characterDefense.StartDefend();
            return false;
        }

        if (Time.time >= _engageLowHpPhaseEndTime)
        {
            _engageLowHpDefendPhase = !_engageLowHpDefendPhase;
            if (_engageLowHpDefendPhase)
            {
                _engageLowHpPhaseEndTime = Time.time + Random.Range(engageLowHpDefendPhaseMinSeconds, engageLowHpDefendPhaseMaxSeconds);
                attackManager?.CancelBufferedComboNow();
                characterDefense.StartDefend();
                return false;
            }

            _engageLowHpPhaseEndTime = Time.time + Random.Range(engageLowHpAttackPhaseMinSeconds, engageLowHpAttackPhaseMaxSeconds);
            characterDefense.StopDefend();
            return true;
        }

        if (_engageLowHpDefendPhase)
        {
            characterDefense.StartDefend();
            return false;
        }

        characterDefense.StopDefend();
        return true;
    }

    private float GetSelfHpNormalized()
    {
        if (selfHealth == null || selfHealth.MaxHealth <= 0)
            return 1f;
        return Mathf.Clamp01(selfHealth.CurrentHealth / selfHealth.MaxHealth);
    }

    /// <summary>Run toward player when healthy; walk when hurt; used only for <see cref="SquadRole.EngagePlayer"/>.</summary>
    private float GetEngagePlayerSpeedMultiplier()
    {
        float hp = GetSelfHpNormalized();
        if (hp > engageHealthyHpThreshold)
            return engageRunSpeedMultiplier;
        return engageCautiousWalkSpeedMultiplier;
    }

    private void ExecuteCurrentRole()
    {
        if (ShouldSuppressLocomotionForHitReaction())
            return;

        _customMoverDroveAnimThisFrame = false;
        if (CurrentRole != SquadRole.EngagePlayer)
            _engageMeleeBandLatch = false;

        Vector3 rawSep = coordinator != null
            ? coordinator.GetSeparationHint(transform.position, separationRadius, separationWeight)
            : Vector3.zero;
        if (separationResponseSmoothTime > 0.0001f)
        {
            _smoothedSeparation = Vector3.SmoothDamp(
                _smoothedSeparation,
                rawSep,
                ref _sepSmoothVelocity,
                separationResponseSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }
        else
            _smoothedSeparation = rawSep;

        Vector3 sep = _smoothedSeparation;
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
                    _engagePathSpeedScale = GetEngagePlayerSpeedMultiplier();
                    float engageSpeed = moveSpeed * _engagePathSpeedScale;
                    // Spread attackers around the player (avoid all stacking on one angle).
                    float approachRadius = Mathf.Max(meleeEngageRange * 1.05f, 2.2f);
                    Vector3 movePoint = coordinator.GetEngageApproachPoint(this, approachRadius, 45f);
                    ExecuteCombatAt(movePoint, player.position, null, engageSpeed, sep, player.gameObject, applyEngagePlayerHpCombat: true);
                }
                break;
            case SquadRole.AttackDoor:
                {
                    DoorBreakable d = coordinator.GetBestDoorToAttack(transform.position);
                    if (d != null)
                        ExecuteCombatAt(d.transform.position, d.transform.position, d.gameObject, moveSpeed, sep, objectiveHoldGround: true);
                    break;
                }
            case SquadRole.AttackStone:
                {
                    Transform stone = coordinator.GetMagicStoneTransform();
                    if (stone != null)
                        ExecuteCombatAt(stone.position, stone.position, stone.gameObject, moveSpeed, sep, objectiveHoldGround: true);
                    break;
                }
            case SquadRole.Search:
                if (coordinator.HasLastKnownPlayer)
                    MoveToward(coordinator.LastKnownPlayerPosition, moveSpeed * 0.85f, sep);
                break;
        }

        // Custom mover drives Speed only when it actually stepped the path this frame; otherwise NavMesh/direct path sets _velocity.
        if (customMover != null && _customMoverDroveAnimThisFrame)
            return;

        if (animator != null && !string.IsNullOrEmpty(animatorSpeedParam))
        {
#if UNITY_AI_NAVIGATION
            float targetSpd = navMeshAgent != null && navMeshAgent.isOnNavMesh ? navMeshAgent.velocity.magnitude : _velocity.magnitude;
#else
            float targetSpd = _velocity.magnitude;
#endif
            if (animatorLocomotionSpeedSmoothTime > 1e-5f)
            {
                float smoothed = Mathf.SmoothDamp(
                    animator.GetFloat(animatorSpeedParam),
                    targetSpd,
                    ref _animatorSpeedSmoothVelocity,
                    animatorLocomotionSpeedSmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime);
                animator.SetFloat(animatorSpeedParam, smoothed);
            }
            else
            {
                _animatorSpeedSmoothVelocity = 0f;
                animator.SetFloat(animatorSpeedParam, targetSpd);
            }
        }
    }

    /// <summary>
    /// Move toward a point, but aim/attack a (potentially different) target point.
    /// This lets us spread attackers around the player while still facing/attacking the player.
    /// <paramref name="damageTickTarget"/> — door/stone tick DPS when appropriate.
    /// <paramref name="playerFallbackMelee"/> — when engaging the player without <see cref="attackManager"/>, optional tick damage.
    /// <paramref name="objectiveHoldGround"/> — door/stone: within range, do not keep moving (face and attack only).
    /// </summary>
    private void ExecuteCombatAt(Vector3 moveTarget, Vector3 aimTarget, GameObject damageTickTarget, float speed, Vector3 sep, GameObject playerFallbackMelee = null, bool applyEngagePlayerHpCombat = false, bool objectiveHoldGround = false)
    {
        float dist = HorizontalDist(transform.position, aimTarget);
        float holdRadius = objectiveHoldGround && damageTickTarget != null
            ? Mathf.Max(meleeEngageRange, objectiveDamageRange)
            : meleeEngageRange;
        bool inMeleeBand;
        if (stopAtMeleeRange && !objectiveHoldGround && applyEngagePlayerHpCombat && meleeChaseResumeHysteresis > 1e-5f)
        {
            if (_engageMeleeBandLatch)
            {
                if (dist > holdRadius + meleeChaseResumeHysteresis)
                    _engageMeleeBandLatch = false;
            }
            else if (dist <= holdRadius)
                _engageMeleeBandLatch = true;

            inMeleeBand = _engageMeleeBandLatch;
        }
        else
            inMeleeBand = dist <= holdRadius;
        bool useAttackManager = attackManager != null;
        bool allowMeleeFeed = true;

        if (applyEngagePlayerHpCombat && playerFallbackMelee != null && characterDefense != null)
            allowMeleeFeed = UpdateEngagePlayerLowHpCombat(inMeleeBand);

        if (inMeleeBand)
        {
            if (useAttackManager && allowMeleeFeed)
                TryFeedMeleeCombo();

            // Door/stone: stand in place, face objective, strike — no orbit from separation steering.
            bool shouldStop = objectiveHoldGround
                || stopAtMeleeRange
                || (useAttackManager && pauseLocomotionDuringAttackAnim && attackManager.InAttackState());
            if (shouldStop)
            {
                _velocity = Vector3.zero;
#if UNITY_AI_NAVIGATION
                if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.stoppingDistance = Mathf.Max(0.05f, holdRadius * 0.9f);
                    navMeshAgent.isStopped = true;
                    navMeshAgent.ResetPath();
                }
#endif
                if (customMover != null && animator != null && !string.IsNullOrEmpty(animatorSpeedParam))
                    animator.SetFloat(animatorSpeedParam, 0f);
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

            // After locomotion so we do not fight ApplyMove(velocity) vs aim-facing in the same frame.
            FaceTowards(aimTarget);
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

        if (stamina != null && !stamina.TrySpendAttack())
            return;

        if (Time.time < _nextMeleeInputTime)
            return;

        _nextMeleeInputTime = Time.time + attackInputRepeatInterval;
        attackManager.TryAttack();
    }

    private Vector3 ComputePlanarMoveDirection(Vector3 flatToGoal, Vector3 separation)
    {
        flatToGoal.y = 0f;
        separation.y = 0f;
        Vector3 goal = flatToGoal.sqrMagnitude > 1e-8f
            ? flatToGoal.normalized
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 combined = goal + separation;
        if (combined.sqrMagnitude < 1e-6f)
            return goal;
        return combined.normalized;
    }

    private Vector3 SmoothSteeringDirection(Vector3 rawPlanarDirection)
    {
        rawPlanarDirection.y = 0f;
        if (rawPlanarDirection.sqrMagnitude < 1e-6f)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            rawPlanarDirection = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }
        else
            rawPlanarDirection.Normalize();

        if (moveDirectionSmoothTime <= 0.0001f)
        {
            _smoothedPlanarMoveDirXZ = rawPlanarDirection;
            return rawPlanarDirection;
        }

        _smoothedPlanarMoveDirXZ = Vector3.SmoothDamp(
            _smoothedPlanarMoveDirXZ,
            rawPlanarDirection,
            ref _moveDirSmoothVelocity,
            moveDirectionSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);
        _smoothedPlanarMoveDirXZ.y = 0f;
        return _smoothedPlanarMoveDirXZ.sqrMagnitude > 1e-6f
            ? _smoothedPlanarMoveDirXZ.normalized
            : rawPlanarDirection;
    }

    private void ResetSteeringDirectionSmoothing(Vector3 planarHint)
    {
        _moveDirSmoothVelocity = Vector3.zero;
        planarHint.y = 0f;
        _smoothedPlanarMoveDirXZ = planarHint.sqrMagnitude > 1e-6f
            ? planarHint.normalized
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
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
        speed *= GetLowStaminaSpeedMultiplier();
        if (TryMoveWithCustomPathfinding(world))
            return;

        Vector3 flat = world - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < arriveDistance * arriveDistance)
        {
            _velocity = Vector3.zero;
            ResetSteeringDirectionSmoothing(flat);
#if UNITY_AI_NAVIGATION
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
                navMeshAgent.ResetPath();
#endif
            return;
        }

        Vector3 rawDir = ComputePlanarMoveDirection(flat, separation);
        Vector3 dir = SmoothSteeringDirection(rawDir);

        _velocity = dir * speed;

#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.speed = speed;
            navMeshAgent.updateRotation = true;
            Vector3 dest = world + separation * navMeshSeparationDestinationScale;
            float now = Time.time;
            float planarDelta = HorizontalDist(
                new Vector3(dest.x, 0f, dest.z),
                new Vector3(_lastNavMeshDestination.x, 0f, _lastNavMeshDestination.z));
            if (!_hasNavMeshDestination
                || now >= _nextNavMeshGoalTime
                || planarDelta >= navMeshGoalMinPlanarDelta)
            {
                navMeshAgent.SetDestination(dest);
                _lastNavMeshDestination = dest;
                _hasNavMeshDestination = true;
                _nextNavMeshGoalTime = now + navMeshGoalRefreshInterval;
            }

            return;
        }
#endif
        ApplyMove(dir * speed, speed);
    }

    private void MoveAwayFrom(Vector3 world, float speed, Vector3 separation)
    {
        speed *= GetLowStaminaSpeedMultiplier();
#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            navMeshAgent.isStopped = false;
#endif
        Vector3 flat = transform.position - world;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f)
            flat = transform.forward * -1f;
        Vector3 rawDir = ComputePlanarMoveDirection(flat, separation);
        Vector3 dir = SmoothSteeringDirection(rawDir);
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
            Vector3 fleeDest = transform.position + dir * 8f;
            float now = Time.time;
            float planarDelta = HorizontalDist(
                new Vector3(fleeDest.x, 0f, fleeDest.z),
                new Vector3(_lastNavMeshDestination.x, 0f, _lastNavMeshDestination.z));
            if (!_hasNavMeshDestination
                || now >= _nextNavMeshGoalTime
                || planarDelta >= navMeshGoalMinPlanarDelta)
            {
                navMeshAgent.SetDestination(fleeDest);
                _lastNavMeshDestination = fleeDest;
                _hasNavMeshDestination = true;
                _nextNavMeshGoalTime = now + navMeshGoalRefreshInterval;
            }

            return;
        }
#endif
        ApplyMove(dir * speed, speed);
    }

    private float GetLowStaminaSpeedMultiplier()
    {
        if (stamina == null)
            return 1f;

        if (stamina.Normalized < lowStaminaWalkThreshold01)
            return lowStaminaWalkSpeedMultiplier;

        return 1f;
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
            ResetSteeringDirectionSmoothing(goal - transform.position);
            return true;
        }

        Vector3 pathGoal = goal;
        if (!_customPathGoalSmoothActive || HorizontalDist(_customPathGoalSmoothed, goal) > customPathGoalTeleportSnapDistance)
        {
            _customPathGoalSmoothed = goal;
            _customPathGoalSmoothVelocity = Vector3.zero;
            _customPathGoalSmoothActive = true;
            pathGoal = goal;
        }
        else if (customPathGoalSmoothTime > 1e-5f)
        {
            _customPathGoalSmoothed = Vector3.SmoothDamp(
                _customPathGoalSmoothed,
                goal,
                ref _customPathGoalSmoothVelocity,
                customPathGoalSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
            pathGoal = _customPathGoalSmoothed;
            pathGoal.y = goal.y;
        }
        else
            pathGoal = goal;

        if (Time.time >= _nextCustomPathRecalcTime
            || HorizontalDist(_lastCustomPathGoal, pathGoal) >= customPathRecalcDistance)
        {
            _nextCustomPathRecalcTime = Time.time + customPathRecalcInterval;
            _lastCustomPathGoal = pathGoal;
            customMover.RecalculatePathFinding(pathGoal);
        }

        // Graph has no path, or all waypoints were consumed but we are still far from the real goal (e.g. door).
        if (!customMover.HasValidMovementPath())
            return false;

        // Same smoothed separation as steering / NavMesh (avoid jitter from recomputing raw hint here).
        customMover.SetGroupSeparation(_smoothedSeparation);

        // Walk when low stamina; slower approach toward player when HP is not healthy (engage scale).
        customMover.SetExternalSpeedMultiplier(GetLowStaminaSpeedMultiplier() * _engagePathSpeedScale);
        customMover.Move();
        _customMoverDroveAnimThisFrame = true;
        return true;
    }

    private void ApplyMove(Vector3 planarVelocity, float speed)
    {
        if (characterController != null)
        {
            Vector3 motion = planarVelocity * Time.deltaTime;
            motion.y = Physics.gravity.y * Time.deltaTime;
            characterController.Move(motion);
            // Ignore tiny separation jitter so CharacterController does not spin in place.
            if (planarVelocity.sqrMagnitude > 0.08f)
            {
                Vector3 look = planarVelocity;
                look.y = 0f;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(look.normalized),
                    moveFacingSlerpSharpness * Time.deltaTime);
            }
        }
        else
        {
            transform.position += planarVelocity * Time.deltaTime;
        }
    }

    private float GetObjectiveInterruptEnterRadius()
    {
        if (objectivePlayerInterruptRadius > 0.01f)
            return objectivePlayerInterruptRadius;
        return coordinator != null ? coordinator.EngagePlayerRadius * 1.5f : 6f;
    }

    private bool MaintainObjectivePlayerInterrupt(bool hasPlayer)
    {
        if (!_objectiveInterruptLatch)
            return false;
        if (!hasPlayer || coordinator == null)
        {
            _objectiveInterruptLatch = false;
            return false;
        }

        float exitR = GetObjectiveInterruptEnterRadius() + objectivePlayerInterruptReleaseMargin;
        float dist = HorizontalDist(transform.position, coordinator.PlayerTransform.position);
        if (dist > exitR)
        {
            _objectiveInterruptLatch = false;
            return false;
        }

        return true;
    }

    private bool TryBeginObjectivePlayerInterrupt(bool hasPlayer, bool eligible)
    {
        if (!eligible || !hasPlayer || coordinator == null)
            return false;

        float enterR = GetObjectiveInterruptEnterRadius();
        float dist = HorizontalDist(transform.position, coordinator.PlayerTransform.position);
        if (dist <= enterR)
        {
            _objectiveInterruptLatch = true;
            return true;
        }

        return false;
    }

    private void RefreshDecisionDebugLabelUnavailable(string reason)
    {
        if (decisionDebugLabel == null)
            return;
        if (!showDecisionDebug)
        {
            decisionDebugLabel.text = string.Empty;
            return;
        }

        decisionDebugLabel.text = reason;
    }

    private void RefreshDecisionDebugLabel()
    {
        if (decisionDebugLabel == null)
            return;
        if (!showDecisionDebug)
        {
            decisionDebugLabel.text = string.Empty;
            return;
        }

        var sb = new StringBuilder(128);
        sb.Append(BuildDecisionTargetLabel());
        sb.Append(" - ");
        sb.Append(CurrentRole);
        sb.Append(" - ");
        sb.Append(BuildDecisionLocomotionLabel());
        sb.Append(" - ");
        sb.Append(GetDecisionPathBackendLabel());
        sb.Append(" - ");
        sb.Append(BuildDecisionSquadModeLabel());
        if (_objectiveInterruptLatch && forceObjectiveRole && CurrentRole == SquadRole.EngagePlayer)
            sb.Append(" - ObjInterrupt");

        decisionDebugLabel.text = sb.ToString();
    }

    private string BuildDecisionSquadModeLabel()
    {
        if (forceEngagePlayerRole && !forceObjectiveRole)
            return "WaveEngage";
        if (forceObjectiveRole)
            return "WaveObjective";
        return "Fuzzy";
    }

    private string BuildDecisionTargetLabel()
    {
        if (coordinator == null)
            return "NoCoordinator";

        switch (CurrentRole)
        {
            case SquadRole.EngagePlayer:
            case SquadRole.Flee:
                return coordinator.PlayerTransform != null ? "Player" : "NoPlayer";
            case SquadRole.AttackDoor:
            {
                DoorBreakable d = coordinator.GetBestDoorToAttack(transform.position);
                return d != null ? $"Door:{d.gameObject.name}" : "NoDoor";
            }
            case SquadRole.AttackStone:
            {
                Transform stone = coordinator.GetMagicStoneTransform();
                return stone != null ? $"Stone:{stone.name}" : "NoStone";
            }
            case SquadRole.Search:
                return coordinator.HasLastKnownPlayer ? "LastKnown" : "NoTrack";
            default:
                return "?";
        }
    }

    private string BuildDecisionLocomotionLabel()
    {
        if (ShouldSuppressLocomotionForHitReaction())
            return "HitReact";

        bool staminaLow = stamina != null && stamina.Normalized < lowStaminaWalkThreshold01;
        string TiredSuffix() => staminaLow ? "+Tired" : string.Empty;

        switch (CurrentRole)
        {
            case SquadRole.Flee:
                return "Flee" + TiredSuffix();
            case SquadRole.EngagePlayer:
            {
                if (coordinator == null || coordinator.PlayerTransform == null)
                    return "Idle";

                if (pauseLocomotionDuringAttackAnim && attackManager != null && attackManager.InAttackState())
                    return "MeleeAnim" + TiredSuffix();

                Transform player = coordinator.PlayerTransform;
                float dist = HorizontalDist(transform.position, player.position);
                if (dist <= meleeEngageRange && stopAtMeleeRange)
                    return "MeleeHold" + TiredSuffix();

                float hp = GetSelfHpNormalized();
                if (hp > engageHealthyHpThreshold)
                    return "Run" + TiredSuffix();
                return "CautiousWalk" + TiredSuffix();
            }
            case SquadRole.AttackDoor:
            {
                if (coordinator == null)
                    return "Idle";
                DoorBreakable d = coordinator.GetBestDoorToAttack(transform.position);
                if (d == null)
                    return "Idle";
                float dist = HorizontalDist(transform.position, d.transform.position);
                float hold = Mathf.Max(meleeEngageRange, objectiveDamageRange);
                if (dist <= hold)
                {
                    if (pauseLocomotionDuringAttackAnim && attackManager != null && attackManager.InAttackState())
                        return "StrikeAnim" + TiredSuffix();
                    return "AtObjective" + TiredSuffix();
                }

                return "ToObjective" + TiredSuffix();
            }
            case SquadRole.AttackStone:
            {
                if (coordinator == null)
                    return "Idle";
                Transform stone = coordinator.GetMagicStoneTransform();
                if (stone == null)
                    return "Idle";
                float dist = HorizontalDist(transform.position, stone.position);
                float hold = Mathf.Max(meleeEngageRange, objectiveDamageRange);
                if (dist <= hold)
                {
                    if (pauseLocomotionDuringAttackAnim && attackManager != null && attackManager.InAttackState())
                        return "StrikeAnim" + TiredSuffix();
                    return "AtObjective" + TiredSuffix();
                }

                return "ToObjective" + TiredSuffix();
            }
            case SquadRole.Search:
                return "SearchMove" + TiredSuffix();
            default:
                return "Idle";
        }
    }

    private string GetDecisionPathBackendLabel()
    {
        if (customMover != null)
            return "CustomPath";
#if UNITY_AI_NAVIGATION
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            return "NavMesh";
#endif
        return "Direct";
    }

    private static float HorizontalDist(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
