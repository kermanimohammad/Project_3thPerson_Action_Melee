using UnityEngine;

/// <summary>
/// Put this on the same GameObject as the Animator that fires the Animation Events.
/// Forwards "jump" events to <see cref="PlayerJumpSfx"/> on the Player root.
/// </summary>
public sealed class AnimatorEventReceiver_JumpSfx : MonoBehaviour
{
    [SerializeField] private PlayerJumpSfx jumpSfx;

    private void Awake()
    {
        if (jumpSfx == null)
            jumpSfx = GetComponentInParent<PlayerJumpSfx>();
    }

    // Animation Event (exact name expected by clips)
    public void jump()
    {
        if (jumpSfx != null)
            jumpSfx.jump();
    }

    // Optional AnimationEvent signature support
    public void jump(AnimationEvent e)
    {
        if (jumpSfx != null)
            jumpSfx.jump(e);
    }
}

