using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class PlayerDodge : MonoBehaviour
{
    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 10f;
    [SerializeField] private float dodgeDuration = 0.25f;
    [SerializeField] private float dodgeCooldown = 0.6f;
    [SerializeField] private float moveDeadzone = 0.1f;
    [Tooltip("When true (default), ForceMove starts only after the Animator enters the Dodge state — transition time can make movement feel late. When false, movement begins next frame after the dodge trigger (tighter to input / wind-up).")]
    [SerializeField] private bool waitForDodgeStateBeforeMoving = true;
    [Tooltip("How much pre-dodge walk/run velocity is added each frame while dodging (world space). 1 = full carry; 0 = only scripted dodge impulse.")]
    [SerializeField, Range(0f, 2f)] private float dodgeLocomotionCarryScale = 1f;
    [Tooltip("After dodge impulse ends, keep applying locomotion carry for this many seconds while the Animator blends back to the locomotion blend tree (matches exit transition ~0.25). Prevents a one-frame stop before walk resumes.")]
    [SerializeField] private float dodgeExitBlendCarrySeconds = 0.26f;

    [Header("Dodge collision")]
    [Tooltip("Scales CharacterController and CapsuleCollider height during Dodge. 0.5 = half height.")]
    [SerializeField, Range(0.25f, 1f)] private float dodgeHeightMultiplier = 0.5f;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private CapsuleCollider capsuleCollider;

    [Header("Dodge SFX")]
    [Tooltip("Optional: plays when a dodge starts.")]
    [SerializeField] private AudioClip dodgeStartClip;
    [SerializeField, Range(0f, 1f)] private float dodgeStartClipVolume = 1f;
    [SerializeField] private AudioMixerGroup dodgeSfxOutputGroup;

    [Header("References")]
    [SerializeField] private PlayerInputRouter input;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    private bool isDodging;
    private float nextDodgeTime;

    private float originalCcHeight;
    private Vector3 originalCcCenter;
    private float originalCapsuleHeight;
    private Vector3 originalCapsuleCenter;
    private bool colliderScaled;
    private AudioSource _dodgeSfxSource;

    private void Awake()
    {
        if (stamina == null)
            stamina = GetComponent<PlayerStamina>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (capsuleCollider == null)
            capsuleCollider = GetComponent<CapsuleCollider>();

        _dodgeSfxSource = gameObject.AddComponent<AudioSource>();
        _dodgeSfxSource.playOnAwake = false;
        _dodgeSfxSource.loop = false;
        _dodgeSfxSource.spatialBlend = 0f;
        if (dodgeSfxOutputGroup == null)
            dodgeSfxOutputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _dodgeSfxSource.outputAudioMixerGroup = dodgeSfxOutputGroup;
    }

    private void OnEnable()
    {
        if (input != null)
            input.DodgePressed += TryDodge;
    }

    private void OnDisable()
    {
        if (input != null)
            input.DodgePressed -= TryDodge;
    }

    private void TryDodge()
    {
        if (motor == null || input == null || animator == null)
            return;

        if (Time.time < nextDodgeTime)
            return;

        if (isDodging || motor.MovementLocked)
            return;

        if (!motor.IsGrounded)
            return;

        if (combat != null && combat.IsDefending)
            return;

        if (stamina != null && !stamina.TrySpendDodge())
            return;

        nextDodgeTime = Time.time + dodgeCooldown;
        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        isDodging = true;

        Vector3 locomotionCarry = motor.PlanarVelocity;

        motor.SetMovementLocked(true);
        ApplyDodgeCollisionScale();

        animator.ResetTrigger(AnimParams.Dodge);
        animator.SetTrigger(AnimParams.Dodge);

        if (dodgeStartClip != null && _dodgeSfxSource != null)
        {
            if (dodgeSfxOutputGroup == null)
                dodgeSfxOutputGroup = GameAudioSettings.FindMixerGroup("SFX");
            _dodgeSfxSource.outputAudioMixerGroup = dodgeSfxOutputGroup;
            _dodgeSfxSource.PlayOneShot(dodgeStartClip, Mathf.Clamp01(dodgeStartClipVolume));
        }

        try
        {
            if (waitForDodgeStateBeforeMoving)
            {
                while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != AnimParams.DodgeState)
                {
                    if (dodgeLocomotionCarryScale > 0f)
                        motor.ForceMove(locomotionCarry * dodgeLocomotionCarryScale * Time.deltaTime);
                    yield return null;
                }
            }
            else
            {
                if (dodgeLocomotionCarryScale > 0f)
                    motor.ForceMove(locomotionCarry * dodgeLocomotionCarryScale * Time.deltaTime);
                yield return null;
            }

            Vector3 dodgeDir = ResolveDodgeDirection();

            float t = 0f;
            Vector3 carryDelta = locomotionCarry * dodgeLocomotionCarryScale;
            while (t < dodgeDuration)
            {
                t += Time.deltaTime;
                motor.ForceMove(carryDelta * Time.deltaTime + dodgeDir * (dodgeSpeed * Time.deltaTime));
                yield return null;
            }

            // Tail of dodge clip + crossfade to locomotion: movement is still locked in Motor so we must keep pushing
            // or the body freezes until the blend finishes.
            while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == AnimParams.DodgeState)
            {
                ApplyDodgeTailCarryStep(carryDelta);
                yield return null;
            }

            if (dodgeExitBlendCarrySeconds > 0f && dodgeLocomotionCarryScale > 0f)
            {
                float exitCarryT = 0f;
                while (exitCarryT < dodgeExitBlendCarrySeconds)
                {
                    ApplyDodgeTailCarryStep(carryDelta);
                    exitCarryT += Time.deltaTime;
                    yield return null;
                }
            }
        }
        finally
        {
            RestoreDodgeCollisionScale();
            motor.SetMovementLocked(false);
            isDodging = false;
        }
    }

    private void ApplyDodgeTailCarryStep(Vector3 carryDelta)
    {
        if (dodgeLocomotionCarryScale <= 0f)
            return;
        motor.ForceMove(carryDelta * Time.deltaTime);
    }

    private Vector3 ResolveDodgeDirection()
    {
        Vector2 move = input.Move;

        if (cameraTransform != null && move.sqrMagnitude >= moveDeadzone * moveDeadzone)
        {
            Vector3 dir = new Vector3(move.x, 0f, move.y).normalized;
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            return (Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward).normalized;
        }

        return transform.forward.normalized;
    }

    private void ApplyDodgeCollisionScale()
    {
        if (colliderScaled)
            return;

        float m = Mathf.Clamp(dodgeHeightMultiplier, 0.25f, 1f);
        if (m >= 0.999f)
            return;

        if (characterController != null)
        {
            originalCcHeight = characterController.height;
            originalCcCenter = characterController.center;

            float newHeight = originalCcHeight * m;
            float delta = (originalCcHeight - newHeight) * 0.5f;
            characterController.height = newHeight;
            characterController.center = new Vector3(originalCcCenter.x, originalCcCenter.y - delta, originalCcCenter.z);
        }

        if (capsuleCollider != null)
        {
            originalCapsuleHeight = capsuleCollider.height;
            originalCapsuleCenter = capsuleCollider.center;

            float newHeight = originalCapsuleHeight * m;
            float delta = (originalCapsuleHeight - newHeight) * 0.5f;
            capsuleCollider.height = newHeight;
            capsuleCollider.center = new Vector3(originalCapsuleCenter.x, originalCapsuleCenter.y - delta, originalCapsuleCenter.z);
        }

        colliderScaled = true;
    }

    private void RestoreDodgeCollisionScale()
    {
        if (!colliderScaled)
            return;

        if (characterController != null)
        {
            characterController.height = originalCcHeight;
            characterController.center = originalCcCenter;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.height = originalCapsuleHeight;
            capsuleCollider.center = originalCapsuleCenter;
        }

        colliderScaled = false;
    }
}