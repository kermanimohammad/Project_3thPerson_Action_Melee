using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    public enum InPlaceJumpLiftTiming
    {
        Immediate,
        AfterDelay,
        AnimationEvent
    }
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float moveDeadzone = 0.1f;
    [Tooltip("Scales CharacterController displacement only. Animator Speed (blend tree) still uses Walk/Sprint Speed so stride/rate stay matched to those values.")]
    [SerializeField, Min(0.01f)] private float locomotionDisplacementMultiplier = 1f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float groundStickForce = -2f;
    [SerializeField] private float fallGravityMultiplier = 2.2f;
    [SerializeField] private float lowJumpGravityMultiplier = 2.0f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerInputRouter input;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerStamina stamina;

    [Header("Combat Movement")]
    [SerializeField] private float attackMoveSpeedMultiplier = 0.35f;

    [Header("Temporary safety switches")]
    [Tooltip("If enabled, disables ALL planar displacement (including AnimationEvent-driven AttackMove) while an attack animation is playing/transitioning (grounded only). Gravity/vertical motion still applies.")]
    [SerializeField] private bool disableAllPlanarDisplacementDuringAttacks = true;

    [Header("Jump animation")]
    [Tooltip("If true, 'moving jump' uses planar speed; if false, uses move input (WASD) vs deadzone.")]
    [SerializeField] private bool jumpBranchUsesPlanarSpeed;
    [Tooltip("Minimum planar speed to treat jump as moving (when Jump Branch Uses Planar Speed is on).")]
    [SerializeField] private float jumpMoveSpeedThreshold = 0.15f;
    [Tooltip("In-place jump (JumpStart → JumpInPlace): when vertical impulse is applied. Moving jump (JumpRun) is always immediate.")]
    [SerializeField] private InPlaceJumpLiftTiming inPlaceJumpLiftTiming = InPlaceJumpLiftTiming.Immediate;
    [Tooltip("Used when In-Place Lift Timing is After Delay.")]
    [SerializeField] private float inPlaceJumpLiftDelaySeconds = 0.12f;

    [Header("Animation events")]
    [Tooltip("Optional: JumpLand clip — Animation Event function name 'OnLand'. In-place jump — on the same GameObject as this script, add Animation Event function 'JumpApplyInPlaceLift' when using Animation Event lift timing.")]
    [SerializeField] private AudioClip landingAudioClip;
    [SerializeField, Range(0f, 1f)] private float landingAudioVolume = 0.5f;

    private CharacterController controller;

    private Vector3 verticalVelocity;
    private Vector3 planarVelocity;

    private float turnSmoothVelocity;

    /// <summary>Snapshot of planar speed when <see cref="MovementLocked"/> became true (dodge); keeps blend tree walk/run through transitions.</summary>
    private float lockedLocomotionAnimSpeed;

    private bool expectsDeferredInPlaceLift;
    private Coroutine inPlaceJumpLiftCoroutine;
    private float inPlaceJumpLiftSafetyReleaseTime;

    public bool IsGrounded => controller.isGrounded;
    public Vector3 PlanarVelocity => planarVelocity;
    public bool MovementLocked { get; private set; }

    public bool IsPlanarDisplacementSuppressed =>
        disableAllPlanarDisplacementDuringAttacks
        && controller != null
        && controller.isGrounded
        && IsInAttackAnimation();

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        TryResolveCameraTransform();
        if (stamina == null)
            stamina = GetComponent<PlayerStamina>();
    }

    private void LateUpdate()
    {
        // Scene transitions (MainMenu -> BattleArea) can change which Camera is tagged MainCamera
        // or create the gameplay camera after this component's Awake. Keep the reference valid.
        if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
            TryResolveCameraTransform();
    }

    private void TryResolveCameraTransform()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            return;
        }

        // Fallback: find any enabled camera (useful if tag is missing).
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].isActiveAndEnabled)
            {
                cameraTransform = cams[i].transform;
                return;
            }
        }
    }

    private void OnEnable()
    {
        if (input != null)
            input.JumpPressed += TryJump;
    }

    private void OnDisable()
    {
        if (input != null)
            input.JumpPressed -= TryJump;

        if (inPlaceJumpLiftCoroutine != null)
        {
            StopCoroutine(inPlaceJumpLiftCoroutine);
            inPlaceJumpLiftCoroutine = null;
        }

        expectsDeferredInPlaceLift = false;
    }

    private void Update()
    {
        bool defending = combat != null && combat.IsDefending;
        bool suppressLocomotion = combat != null && combat.SuppressLocomotionFromInput && controller != null && controller.isGrounded;
        bool suppressAllPlanar = IsPlanarDisplacementSuppressed;

        if (stamina != null && input != null && controller != null)
        {
            bool hasMoveInput = input.Move.sqrMagnitude >= moveDeadzone * moveDeadzone;
            bool sprintingNow =
                controller.isGrounded
                && !MovementLocked
                && !defending
                && !suppressLocomotion
                && !suppressAllPlanar
                && hasMoveInput
                && input.SprintHeld;
            stamina.SetSprintHeld(sprintingNow);
        }

        Vector3 planar;

        if (!MovementLocked && !defending && !suppressLocomotion && !suppressAllPlanar)
        {
            planar = ComputePlanarVelocity();
            planarVelocity = planar;
        }
        else
        {
            planar = Vector3.zero;
            planarVelocity = Vector3.zero;
            if (animator != null)
            {
                if (defending)
                    animator.SetFloat(AnimParams.Speed, 0f, 0.1f, Time.deltaTime);
                else if (MovementLocked)
                    ApplyAnimatorSpeedWhileMovementLocked();
                else
                    animator.SetFloat(AnimParams.Speed, 0f, 0.1f, Time.deltaTime);
            }
        }

        ApplyGravity();

        Vector3 total = planar + verticalVelocity;
        controller.Move(total * Time.deltaTime);

        if (animator != null)
            animator.SetBool(AnimParams.IsGrounded, controller.isGrounded);

       // Debug.Log($"Defending? {(combat != null && combat.IsDefending)}");
    }

    private Vector3 ComputePlanarVelocity()
    {
        if (input == null || cameraTransform == null)
            return Vector3.zero;

        Vector2 move = input.Move;

        bool hasMoveInput = move.sqrMagnitude >= moveDeadzone * moveDeadzone;

        if (!hasMoveInput)
        {
            if (animator != null) animator.SetFloat(AnimParams.Speed, 0f, 0.1f, Time.deltaTime);
            return Vector3.zero;
        }

        Vector3 direction = new Vector3(move.x, 0f, move.y).normalized;

        bool attacking = IsInAttackAnimation();

        // During attack: allow aiming/turning from input, but do not apply locomotion displacement.
        bool canRotate = true;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;

        if (canRotate)
        {
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        if (attacking)
        {
            // Ground attacks: locomotion displacement is driven by Animation Events.
            // Air attacks (jump-run -> kick, etc.): keep airborne movement so we don't "freeze" mid-air.
            if (animator != null) animator.SetFloat(AnimParams.Speed, 0f, 0.1f, Time.deltaTime);

            if (controller != null && controller.isGrounded)
                return Vector3.zero;

            Vector3 airMoveDir = (Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward);
            float airSpeed = input.SprintHeld ? sprintSpeed : walkSpeed;
            return airMoveDir.normalized * (airSpeed * locomotionDisplacementMultiplier);
        }

        Vector3 moveDir = (Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward);

        float speed = input.SprintHeld ? sprintSpeed : walkSpeed;

        if (animator != null) animator.SetFloat(AnimParams.Speed, speed, 0.1f, Time.deltaTime);

        return moveDir.normalized * (speed * locomotionDisplacementMultiplier);
    }

    private bool IsInAttackAnimation()
    {
        // Primary source of truth (your gameplay combat system)
        if (combat != null && combat.SuppressLocomotionFromInput)
            return true;

        if (combat != null && combat.InAttackState)
            return true;

        // Fallback: if the Animator controller isn't tagging states consistently,
        // detect by tag/name so movement doesn't leak during attacks.
        if (animator == null)
            return false;

        const int layer = 0;

        var s = animator.GetCurrentAnimatorStateInfo(layer);
        var n = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer) : default;

        bool isAttackState =
            s.IsTag("Attack")
            || s.IsName("Attack1")
            || s.IsName("Attack2")
            || s.IsName("Attack3")
            || s.IsName("SpacialAttack")
            || s.IsName("KickRun")
            || s.IsName("Kick Run Ort Fnt Leg");

        if (isAttackState)
            return true;

        if (!animator.IsInTransition(layer))
            return false;

        // During crossfade, Current may still be Blend Tree. Treat as attacking if Next is an attack state.
        return n.IsTag("Attack")
               || n.IsName("Attack1")
               || n.IsName("Attack2")
               || n.IsName("Attack3")
               || n.IsName("SpacialAttack")
               || n.IsName("KickRun")
               || n.IsName("Kick Run Ort Fnt Leg");
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = groundStickForce;

        float g = gravity;

        if (verticalVelocity.y < 0f)
        {
            g *= fallGravityMultiplier;
        }
        else if (input != null && !input.JumpHeld)
        {
            g *= lowJumpGravityMultiplier;
        }

        verticalVelocity.y += g * Time.deltaTime;
    }

    private void TryJump()
    {
        if (MovementLocked || input == null)
            return;

        if (!controller.isGrounded)
            return;

        if (combat != null && combat.IsDefending)
            return;

        if (expectsDeferredInPlaceLift)
            return;

        bool movingJump = EvaluateMovingJump();

        if (animator != null)
            ApplyJumpAnimationTrigger(movingJump);

        if (movingJump)
        {
            ApplyJumpVerticalImpulse();
            return;
        }

        switch (inPlaceJumpLiftTiming)
        {
            case InPlaceJumpLiftTiming.Immediate:
                ApplyJumpVerticalImpulse();
                break;
            case InPlaceJumpLiftTiming.AfterDelay:
                expectsDeferredInPlaceLift = true;
                inPlaceJumpLiftSafetyReleaseTime = Time.time + Mathf.Max(3f, inPlaceJumpLiftDelaySeconds + 1f);
                inPlaceJumpLiftCoroutine = StartCoroutine(InPlaceJumpLiftAfterDelay());
                break;
            case InPlaceJumpLiftTiming.AnimationEvent:
                expectsDeferredInPlaceLift = true;
                inPlaceJumpLiftSafetyReleaseTime = Time.time + 3f;
                break;
        }
    }

    /// <summary>
    /// Call from the JumpStart (in-place) clip via Animation Event when <see cref="inPlaceJumpLiftTiming"/> is Animation Event.
    /// Can also fire during After Delay to lift earlier than the timer.
    /// </summary>
    public void JumpApplyInPlaceLift()
    {
        if (!expectsDeferredInPlaceLift)
            return;

        if (inPlaceJumpLiftCoroutine != null)
        {
            StopCoroutine(inPlaceJumpLiftCoroutine);
            inPlaceJumpLiftCoroutine = null;
        }

        ApplyJumpVerticalImpulse();
    }

    private IEnumerator InPlaceJumpLiftAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, inPlaceJumpLiftDelaySeconds));
        inPlaceJumpLiftCoroutine = null;
        if (expectsDeferredInPlaceLift)
            ApplyJumpVerticalImpulse();
    }

    private void CancelDeferredInPlaceJumpLift()
    {
        expectsDeferredInPlaceLift = false;
        if (inPlaceJumpLiftCoroutine != null)
        {
            StopCoroutine(inPlaceJumpLiftCoroutine);
            inPlaceJumpLiftCoroutine = null;
        }
    }

    private void ApplyJumpVerticalImpulse()
    {
        CancelDeferredInPlaceJumpLift();

        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Small peel off the ground so CharacterController stops reporting grounded on the same step.
        controller.Move(Vector3.up * (controller.skinWidth * 2f + 0.01f));
    }

    private bool EvaluateMovingJump()
    {
        return jumpBranchUsesPlanarSpeed
            ? planarVelocity.magnitude > jumpMoveSpeedThreshold
            : input.Move.sqrMagnitude >= moveDeadzone * moveDeadzone;
    }

    /// <summary>
    /// Animator: Any State → JumpRun if Speed &gt; 0.5, → JumpStart if Speed &lt; 0.5 (in-place chain).
    /// Fire <see cref="AnimParams.Jump"/> after setting Speed for the branch.
    /// </summary>
    private void ApplyJumpAnimationTrigger(bool movingJump)
    {
        float speedParam = 0f;
        if (movingJump)
            speedParam = input.SprintHeld ? sprintSpeed : walkSpeed;

        animator.SetFloat(AnimParams.Speed, speedParam, 0f, 0f);
        animator.ResetTrigger(AnimParams.Jump);
        animator.SetTrigger(AnimParams.Jump);
    }

    public void SetMovementLocked(bool locked)
    {
        if (locked && !MovementLocked)
        {
            if (input != null && input.Move.sqrMagnitude >= moveDeadzone * moveDeadzone)
                lockedLocomotionAnimSpeed = input.SprintHeld ? sprintSpeed : walkSpeed;
            else
                lockedLocomotionAnimSpeed = planarVelocity.magnitude;
        }

        MovementLocked = locked;

        if (locked)
            planarVelocity = Vector3.zero;
        else
            lockedLocomotionAnimSpeed = 0f;
    }

    private void ApplyAnimatorSpeedWhileMovementLocked()
    {
        float targetSpeed;

        if (input != null && input.Move.sqrMagnitude >= moveDeadzone * moveDeadzone)
        {
            targetSpeed = input.SprintHeld ? sprintSpeed : walkSpeed;
        }
        else if (lockedLocomotionAnimSpeed > 0.01f)
        {
            targetSpeed = lockedLocomotionAnimSpeed;
        }
        else
        {
            targetSpeed = 0f;
        }

        animator.SetFloat(AnimParams.Speed, targetSpeed, 0.1f, Time.deltaTime);
    }

    public void ForceMove(Vector3 worldDisplacement)
    {
        if (combat != null && combat.IsDefending)
            worldDisplacement = new Vector3(0f, worldDisplacement.y, 0f);

        if (IsPlanarDisplacementSuppressed)
            return;

        controller.Move(worldDisplacement);
    }

    /// <summary>Called from JumpLand (or other clips) via Animation Event; must live on the same GameObject as the Animator.</summary>
    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f)
            return;
        if (landingAudioClip == null)
            return;
        AudioSource.PlayClipAtPoint(landingAudioClip, transform.TransformPoint(controller.center), landingAudioVolume);
    }
}