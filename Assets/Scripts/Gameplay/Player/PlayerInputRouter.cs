using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerInputRouter : MonoBehaviour
{
    [SerializeField] private bool logInputDebug = false;

    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction defendAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction dodgeAction;

    private const string _MoveActionName = "Move";
    private const string _LookActionName = "Look";
    private const string _SprintActionName = "Sprint";
    private const string _DefendActionName = "Defend";
    private const string _JumpActionName = "Jump";
    private const string _AttackActionName = "Attack";
    private const string _DodgeActionName = "Dodge";

    private const string _KeyboardScheme = "KeyboardMouse";
    private const string _GamepadScheme = "Gamepad";

    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool DefendHeld { get; private set; }
    public bool JumpHeld { get; private set; }

    public event Action JumpPressed;
    public event Action AttackPressed;
    public event Action DodgePressed;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions[_MoveActionName];
        lookAction = playerInput.actions[_LookActionName];
        sprintAction = playerInput.actions[_SprintActionName];
        defendAction = playerInput.actions[_DefendActionName];
        jumpAction = playerInput.actions[_JumpActionName];
        attackAction = playerInput.actions[_AttackActionName];
        dodgeAction = playerInput.actions[_DodgeActionName];
    }

    private void OnEnable()
    {
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;

        lookAction.performed += OnLook;
        lookAction.canceled += OnLook;

        sprintAction.performed += OnSprint;
        sprintAction.canceled += OnSprint;

        defendAction.performed += OnDefend;
        defendAction.canceled += OnDefend;

        jumpAction.performed += OnJumpHeld;
        jumpAction.canceled += OnJumpHeld;

        jumpAction.started += OnJumpPressed;
        attackAction.started += OnAttack;
        dodgeAction.started += OnDodge;
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;

        lookAction.performed -= OnLook;
        lookAction.canceled -= OnLook;

        sprintAction.performed -= OnSprint;
        sprintAction.canceled -= OnSprint;

        defendAction.performed -= OnDefend;
        defendAction.canceled -= OnDefend;

        jumpAction.performed -= OnJumpHeld;
        jumpAction.canceled -= OnJumpHeld;

        jumpAction.started -= OnJumpPressed;
        attackAction.started -= OnAttack;
        dodgeAction.started -= OnDodge;
    }

    public bool UsingGamepad => playerInput != null && playerInput.currentControlScheme == _GamepadScheme;
    public bool UsingMouseKeyboard => playerInput != null && playerInput.currentControlScheme == _KeyboardScheme;

    private void OnMove(InputAction.CallbackContext ctx)
    {
        Move = ctx.ReadValue<Vector2>();
        if (logInputDebug) Debug.Log($"Move={Move} ({ctx.phase})");
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        Look = ctx.ReadValue<Vector2>();
        if (logInputDebug) Debug.Log($"Look={Look} ({ctx.phase})");
    }

    private void OnSprint(InputAction.CallbackContext ctx)
    {
        SprintHeld = ReadAsHeld(ctx);
        if (logInputDebug) Debug.Log($"SprintHeld={SprintHeld} ({ctx.phase})");
    }

    private void OnDefend(InputAction.CallbackContext ctx)
    {
        DefendHeld = ReadAsHeld(ctx);
        if (logInputDebug) Debug.Log($"DefendHeld={DefendHeld} ({ctx.phase})");
    }

    private void OnJumpHeld(InputAction.CallbackContext ctx)
    {
        JumpHeld = ReadAsHeld(ctx);
        if (logInputDebug) Debug.Log($"JumpHeld={JumpHeld} ({ctx.phase})");
    }

    private static bool ReadAsHeld(InputAction.CallbackContext ctx)
    {
        if (ctx.control is ButtonControl btn)
            return btn.isPressed;

        float v = 0f;
        try { v = ctx.ReadValue<float>(); } catch { }
        return v > 0.5f;
    }

    private void OnJumpPressed(InputAction.CallbackContext ctx) => JumpPressed?.Invoke();
    private void OnAttack(InputAction.CallbackContext ctx) => AttackPressed?.Invoke();
    private void OnDodge(InputAction.CallbackContext ctx) => DodgePressed?.Invoke();
}