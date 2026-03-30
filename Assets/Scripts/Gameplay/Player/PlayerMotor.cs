using UnityEngine;
using UnityEngine.Windows;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float moveDeadzone = 0.1f;

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

    [Header("Combat Movement")]
    [SerializeField] private float attackMoveSpeedMultiplier = 0.35f;

    private const float _threshold = 0.01f;
    public GameObject CinemachineCameraTarget;
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private CharacterController controller;

    private Vector3 verticalVelocity;
    private Vector3 planarVelocity;

    private float turnSmoothVelocity;

    public bool IsGrounded => controller.isGrounded;
    public Vector3 PlanarVelocity => planarVelocity;
    public bool MovementLocked { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
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
    }

    private void Update()
    {
        bool defending = combat != null && combat.IsDefending;

        Vector3 planar;

        if (!MovementLocked && !defending)
        {
            planar = ComputePlanarVelocity();
            planarVelocity = planar;
        }
        else
        {
            planar = Vector3.zero;
            planarVelocity = Vector3.zero;
            if (animator != null) animator.SetFloat(AnimParams.Speed, 0f, 0.1f, Time.deltaTime);
        }

        ApplyGravity();

        Vector3 total = planar + verticalVelocity;
        controller.Move(total * Time.deltaTime);

        if (animator != null)
            animator.SetBool(AnimParams.IsGrounded, controller.isGrounded);

        // Debug.Log($"Defending? {(combat != null && combat.IsDefending)}");
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void CameraRotation()
    {
        // if there is an input and camera position is not fixed
        if (input.Look.sqrMagnitude >= _threshold)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = input.UsingMouseKeyboard ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += input.Look.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += input.Look.y * deltaTimeMultiplier;
        }

        // clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // Cinemachine will follow this target
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch,
            _cinemachineTargetYaw, 0.0f);
    }


    private Vector3 ComputePlanarVelocity()
    {
        if (input == null || cameraTransform == null)
            return Vector3.zero;

        Vector2 move = input.Move;

        if (move.sqrMagnitude < moveDeadzone * moveDeadzone)
        {
            if (animator != null) animator.SetFloat(AnimParams.Speed, 0f, 0.1f, Time.deltaTime);
            return Vector3.zero;
        }

        Vector3 direction = new Vector3(move.x, 0f, move.y).normalized;

        bool attacking = combat != null && combat.InAttackState;

        bool canRotate = !attacking;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;

        if (canRotate)
        {
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        Vector3 moveDir = attacking
            ? transform.forward
            : (Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward);

        float speed = input.SprintHeld ? sprintSpeed : walkSpeed;

        if (attacking)
            speed *= attackMoveSpeedMultiplier;

        if (animator != null) animator.SetFloat(AnimParams.Speed, speed, 0.1f, Time.deltaTime);

        return moveDir.normalized * speed;
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

        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (animator != null)
            animator.SetTrigger(AnimParams.Jump);
    }

    public void SetMovementLocked(bool locked)
    {
        MovementLocked = locked;
        if (locked)
            planarVelocity = Vector3.zero;
    }

    public void ForceMove(Vector3 worldDisplacement)
    {
        controller.Move(worldDisplacement);
    }
}