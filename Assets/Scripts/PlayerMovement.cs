using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2.0f;
    public float sprintSpeed = 6.0f;
    public float rotationSpeed = 0.1f;

    [Header("Physics Settings")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    [Header("References")]
    public CharacterController controller;
    public Animator animator;
    public Transform cameraTransform;

    private PlayerInputActions inputActions;
    private Vector3 velocity;
    private Vector3 airVelocity;
    private float turnSmoothVelocity;
    private bool isDefending = false;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Attack.performed += OnAttack;
        inputActions.Player.Dodge.performed += OnDodge;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();

        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Attack.performed -= OnAttack;
        inputActions.Player.Dodge.performed -= OnDodge;
    }

    private void Update()
    {
        HandleGravity();
        HandleMovement();
        HandleDefense();

        animator.SetBool("isGrounded", controller.isGrounded);
    }

    private void HandleMovement()
    {
        if (isDefending)
        {
            animator.SetFloat("Speed", 0);
            return;
        }

        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
        bool isSprinting = inputActions.Player.Sprint.IsPressed();

        Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

        if(controller.isGrounded)
        {
            if (direction.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSpeed);

                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

                float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

                airVelocity = moveDir.normalized * currentSpeed;

                controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);

                animator.SetFloat("Speed", currentSpeed, 0.1f, Time.deltaTime);
            }
            else
            {
                airVelocity = Vector3.zero;
                animator.SetFloat("Speed", 0, 0.1f, Time.deltaTime);
            }
        } else
        {
            controller.Move(airVelocity * Time.deltaTime);
        }
        
    }

    private void HandleDefense()
    {
        bool defenseInput = inputActions.Player.Defend.IsPressed();

        if (defenseInput != isDefending)
        {
            isDefending = defenseInput;
            animator.SetBool("isDefending", isDefending);
        }
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (controller.isGrounded && !isDefending)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!isDefending)
        {
            animator.SetTrigger("Attack");
        }
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        if (!isDefending && controller.isGrounded)
        {
            animator.SetTrigger("Dodge");
        }
    }
}