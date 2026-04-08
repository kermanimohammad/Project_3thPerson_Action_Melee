using UnityEngine;

/// <summary>
/// Animation Events are dispatched to components on the same GameObject as the Animator.
/// This relay forwards Hit_Move_Start/End to <see cref="PlayerHitMove"/> that typically
/// lives on the player root.
/// </summary>
public sealed class PlayerHitMoveEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerHitMove target;

    public void Bind(PlayerHitMove hitMove)
    {
        target = hitMove;
    }

    // Animation Event: Hit_Move_Start
    public void Hit_Move_Start()
    {
        if (target != null) target.Hit_Move_Start();
    }

    public void Hit_Move_Start(AnimationEvent e)
    {
        if (target != null) target.Hit_Move_Start(e);
    }

    // Animation Event: Hit_Move_End
    public void Hit_Move_End()
    {
        if (target != null) target.Hit_Move_End();
    }

    public void Hit_Move_End(AnimationEvent e)
    {
        if (target != null) target.Hit_Move_End(e);
    }
}

