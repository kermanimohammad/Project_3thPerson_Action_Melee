using UnityEngine;

/// <summary>
/// Put this on the same GameObject as the Animator that fires the Animation Events.
/// Forwards "foot_sound" events to <see cref="PlayerFootstepSfx"/> on the Player root.
/// </summary>
public sealed class AnimatorEventReceiver_Footsteps : MonoBehaviour
{
    [SerializeField] private PlayerFootstepSfx footsteps;

    private void Awake()
    {
        if (footsteps == null)
            footsteps = GetComponentInParent<PlayerFootstepSfx>();
    }

    // Animation Event (exact name expected by clips)
    public void foot_sound()
    {
        if (footsteps != null)
            footsteps.foot_sound();
    }

    // Optional AnimationEvent signature support
    public void foot_sound(AnimationEvent e)
    {
        if (footsteps != null)
            footsteps.foot_sound(e);
    }
}

