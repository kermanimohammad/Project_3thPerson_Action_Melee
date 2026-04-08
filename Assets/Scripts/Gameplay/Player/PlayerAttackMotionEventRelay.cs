using UnityEngine;

/// <summary>
/// Unity Animation Events are dispatched to components on the same GameObject as the Animator.
/// This relay forwards attack-move events to the main <see cref="PlayerAttackMotion"/> that may
/// live on a different GameObject (typically the player root).
/// </summary>
public sealed class PlayerAttackMotionEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerAttackMotion target;

    public void Bind(PlayerAttackMotion attackMotion)
    {
        target = attackMotion;
    }

    public void AttackMoveImpulse(AnimationEvent e)
    {
        if (target != null) target.AttackMoveImpulse(e);
    }

    /// <summary>Typo alias for <see cref="AttackMoveImpulse"/> (clips named <c>AttachMoveImpulse</c>).</summary>
    public void AttachMoveImpulse(AnimationEvent e)
    {
        if (target != null) target.AttackMoveImpulse(e);
    }

    public void AttachMoveImpulse(float speed)
    {
        if (target != null) target.AttachMoveImpulse(speed);
    }

    public void AttachMoveImpulse()
    {
        if (target != null) target.AttachMoveImpulse();
    }

    public void AttackMoveDash(AnimationEvent e)
    {
        if (target != null) target.AttackMoveDash(e);
    }

    public void AttackMoveStop()
    {
        if (target != null) target.AttackMoveStop();
    }
}

