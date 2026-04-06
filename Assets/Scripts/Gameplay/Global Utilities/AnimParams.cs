using UnityEngine;

public static class AnimParams
{
    public static readonly int Speed = Animator.StringToHash("Speed");
    public static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    public static readonly int IsDefending = Animator.StringToHash("isDefending");

    public static readonly int Jump = Animator.StringToHash("Jump");
    /// <summary>Optional extra trigger name if a controller branches on it; <c>PlayerController.controller</c> uses <see cref="Jump"/> + <see cref="Speed"/> only.</summary>
    public static readonly int JumpRun = Animator.StringToHash("JumpRun");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Dodge = Animator.StringToHash("Dodge");
    public static readonly int Hit = Animator.StringToHash("Hit");
    public static readonly int Death = Animator.StringToHash("Death");
    public static readonly int GetEnergy = Animator.StringToHash("GetEnergy");
    public static readonly int Special = Animator.StringToHash("Special");

    public static readonly int AttackTag = Animator.StringToHash("Attack");

    public static readonly int DodgeState = Animator.StringToHash("Dodge");
}