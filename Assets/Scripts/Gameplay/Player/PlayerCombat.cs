using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private PlayerInputRouter input;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private AttackManager attackManager;
    [SerializeField] private CharacterDefense characterDefense;
    [SerializeField] private PlayerStamina stamina;

    public bool IsDefending => characterDefense.IsDefending;
    public bool InAttackState => attackManager.InAttackState();

    [Header("Attack movement")]
    [Tooltip("Extra suppression window right after attack input (covers transition frames before the Animator enters the attack state).")]
    [SerializeField] private float attackStartSuppressSeconds = 0.12f;
    public bool SuppressLocomotionFromInput => IsInAttackAnimationOrTransition() || Time.time < suppressLocomotionUntilTime;

    /// <summary>
    /// True while an attack animation is playing, or while transitioning into one.
    /// Useful for VFX (e.g., weapon trails).
    /// </summary>
    public bool IsAttackingAnimation => IsInAttackAnimationOrTransition();
    private float suppressLocomotionUntilTime;

    [Header("Special Attack (Sphere AOE)")]
    [SerializeField] private float specialSphereRadius = 3f;
    [SerializeField] private float specialSphereForwardOffset = 1.0f;
    [SerializeField] private float specialSphereLifetime = 0.3f;
    [SerializeField] private float specialDamageAmount = 0.40f;

    private const string _EnemyTag = "Enemy";
    private bool wasDefendHeld;

    private void Awake()
    {
        if (stamina == null)
            stamina = GetComponent<PlayerStamina>();
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.AttackPressed += TryAttack;
            input.SpecialAttackPressed += TrySpecialAttack;
        }
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.AttackPressed -= TryAttack;
            input.SpecialAttackPressed -= TrySpecialAttack;
        }

        if (stamina != null)
            stamina.SetBlockHeld(false);
    }

    private void Update()
    {
        if (animator == null)
            return;

        UpdateDefense();
    }

    private void UpdateDefense()
    {
        if (input == null || motor == null)
        {
            return;
        }

        bool shouldDefend = input.DefendHeld && motor.IsGrounded && !motor.MovementLocked;
        if (stamina != null)
            stamina.SetBlockHeld(shouldDefend);

        if (shouldDefend)
        {
            if (!wasDefendHeld)
            {
                // Cancel any buffered attacks so they don't "resume" after releasing defend.
                if (attackManager != null)
                    attackManager.CancelBufferedComboNow();

                if (animator != null)
                {
                    animator.ResetTrigger(AnimParams.Special);
                    animator.ResetTrigger(AnimParams.Attack);
                }
            }
            characterDefense.StartDefend();
        }
        else if (IsDefending)
        {
            characterDefense.StopDefend();
        }

        wasDefendHeld = shouldDefend;
    }

    private void TryAttack()
    {
        if (IsDefending
        || attackManager ==  null
        || (stamina != null && !stamina.CanAffordAttack())
        || TryAirKickAfterMovingJump())
            return;

        LayerMask enemyLayer = LayerMask.GetMask(_EnemyTag);
        Transform aimTarget = transform.GetClosestNearbyEnemy(enemyLayer);
        TryRotateTowardsAimTarget(aimTarget);

        int attackIndex = attackManager.TryAttack();
        if (attackIndex >= 0 && (motor == null || motor.IsGrounded))
            suppressLocomotionUntilTime = Time.time + attackStartSuppressSeconds;
    }

    private void TrySpecialAttack()
    {
        if (IsDefending || animator == null)
            return;

        if (stamina != null && !stamina.CanSpendSpecialAttack())
            return;

        // Fire Special trigger for SpacialAttack transition in PlayerController.controller
        animator.ResetTrigger(AnimParams.Special);
        animator.SetTrigger(AnimParams.Special);

        if (motor == null || motor.IsGrounded)
            suppressLocomotionUntilTime = Time.time + attackStartSuppressSeconds;
    }

    /// <summary>
    /// Animation Event receiver for the SpacialAttack clip.
    /// Creates a spherical AOE that deals damage via <see cref="DamageService"/>.
    /// </summary>
    public void PerformSpecialHit()
    {
        if (AreaOfEffectService.Instance == null || DamageService.Instance == null)
            return;

        Vector3 center = transform.position + transform.forward * specialSphereForwardOffset;

        AreaOfEffectService.Instance.CreateSphereAOE(
            gameObject,
            center,
            specialSphereRadius,
            specialSphereLifetime,
            (target) => DamageService.Instance.DealDamage(gameObject, target, specialDamageAmount)
        );
    }

    /// <summary>
    /// Animation Event receiver for attack clips.
    /// </summary>
    public void ResetAttack()
    {
        // With trigger-based attacks, there is no persistent "Attack bool" to clear.
        // This method exists to satisfy Animation Events on legacy clips (e.g., SpacialAttack).
        if (animator == null)
            return;

        animator.ResetTrigger(AnimParams.Special);
    }

    private bool IsInAttackAnimationOrTransition()
    {
        if (animator == null)
            return false;

        const int layer = 0;

        AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(layer);
        bool curIsAttack =
            cur.IsTag("Attack")
            || cur.IsName("Attack1")
            || cur.IsName("Attack2")
            || cur.IsName("Attack3")
            || cur.IsName("SpacialAttack")
            || cur.IsName("KickRun")
            || cur.IsName("Kick Run Ort Fnt Leg");

        if (curIsAttack)
            return true;

        if (!animator.IsInTransition(layer))
            return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
        return next.IsTag("Attack")
               || next.IsName("Attack1")
               || next.IsName("Attack2")
               || next.IsName("Attack3")
               || next.IsName("SpacialAttack")
               || next.IsName("KickRun")
               || next.IsName("Kick Run Ort Fnt Leg");
    }

    /// <summary>
    /// <c>PlayerController.controller</c>: JumpRun → Kick when <see cref="AnimParams.Attack"/> fires while not grounded.
    /// Ground combo uses <c>Attack1</c>… via <see cref="AttackManager"/>, not this trigger.
    /// </summary>
    private bool TryAirKickAfterMovingJump()
    {
        if (animator == null || motor == null || motor.IsGrounded)
            return false;

        if (!IsAnimatorInStateOrTransitioningTo(animator, "JumpRun", 0))
            return false;

        animator.ResetTrigger(AnimParams.Attack);
        animator.SetTrigger(AnimParams.Attack);
        return true;
    }

    private static bool IsAnimatorInStateOrTransitioningTo(Animator anim, string stateShortName, int layer)
    {
        if (anim.IsInTransition(layer))
        {
            if (anim.GetNextAnimatorStateInfo(layer).IsName(stateShortName))
                return true;
        }

        return anim.GetCurrentAnimatorStateInfo(layer).IsName(stateShortName);
    }

    private void TryRotateTowardsAimTarget(Transform aimTarget)
	{
        if (aimTarget == null)
		{
            return;
		}

        Vector3 direction = aimTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = targetRotation;
        }
    }

}