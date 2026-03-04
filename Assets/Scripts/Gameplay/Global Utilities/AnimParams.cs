using UnityEngine;

public static class AnimParams
{
    public static readonly int Speed = Animator.StringToHash("Speed");
    public static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    public static readonly int IsDefending = Animator.StringToHash("isDefending");

    public static readonly int Jump = Animator.StringToHash("Jump");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Dodge = Animator.StringToHash("Dodge");

    public static readonly int AttackTag = Animator.StringToHash("Attack");

    public static readonly int DodgeState = Animator.StringToHash("Dodge");
}