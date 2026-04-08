using UnityEngine;

/// <summary>
/// Animation Events fire on the Animator GameObject; forwards Hit_Move_Start/End to <see cref="EnemyHitMove"/> on the enemy root.
/// </summary>
public sealed class EnemyHitMoveEventRelay : MonoBehaviour
{
    [SerializeField] private EnemyHitMove target;

    public void Bind(EnemyHitMove hitMove)
    {
        target = hitMove;
    }

    public void Hit_Move_Start()
    {
        if (target != null) target.Hit_Move_Start();
    }

    public void Hit_Move_Start(AnimationEvent e)
    {
        if (target != null) target.Hit_Move_Start(e);
    }

    public void Hit_Move_End()
    {
        if (target != null) target.Hit_Move_End();
    }

    public void Hit_Move_End(AnimationEvent e)
    {
        if (target != null) target.Hit_Move_End(e);
    }
}
