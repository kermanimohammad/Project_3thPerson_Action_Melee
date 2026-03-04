using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerInputRouter : MonoBehaviour
{
    [SerializeField] private bool logInputDebug = false;

    private PlayerInputActions input;

    public Vector2 Move { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool DefendHeld { get; private set; }
    public bool JumpHeld { get; private set; }

    public event Action JumpPressed;
    public event Action AttackPressed;
    public event Action DodgePressed;

    private void Awake()
    {
        input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        input.Player.Enable();

        input.Player.Move.performed += OnMove;
        input.Player.Move.canceled += OnMove;

        input.Player.Sprint.performed += OnSprint;
        input.Player.Sprint.canceled += OnSprint;

        input.Player.Defend.performed += OnDefend;
        input.Player.Defend.canceled += OnDefend;

        input.Player.Jump.performed += OnJumpHeld;
        input.Player.Jump.canceled += OnJumpHeld;

        input.Player.Jump.started += OnJumpPressed;
        input.Player.Attack.started += OnAttack;
        input.Player.Dodge.started += OnDodge;
    }

    private void OnDisable()
    {
        input.Player.Move.performed -= OnMove;
        input.Player.Move.canceled -= OnMove;

        input.Player.Sprint.performed -= OnSprint;
        input.Player.Sprint.canceled -= OnSprint;

        input.Player.Defend.performed -= OnDefend;
        input.Player.Defend.canceled -= OnDefend;

        input.Player.Jump.performed -= OnJumpHeld;
        input.Player.Jump.canceled -= OnJumpHeld;

        input.Player.Jump.started -= OnJumpPressed;
        input.Player.Attack.started -= OnAttack;
        input.Player.Dodge.started -= OnDodge;

        input.Player.Disable();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        Move = ctx.ReadValue<Vector2>();
        if (logInputDebug) Debug.Log($"Move={Move} ({ctx.phase})");
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