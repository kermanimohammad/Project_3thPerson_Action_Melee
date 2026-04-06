using UnityEngine;

/// <summary>
/// Animation Event receiver that forwards trail events from a child Animator object to the Player root.
/// Attach this to the same GameObject that owns the Animator which fires the events.
/// </summary>
public sealed class AnimatorEventReceiver_WeaponTrail : MonoBehaviour
{
    [SerializeField] private WeaponTrailParticleToggle trails;

    private void Awake()
    {
        if (trails == null)
            trails = GetComponentInParent<WeaponTrailParticleToggle>();
    }

    // Animation Event
    public void TrailOn()
    {
        if (trails != null)
            trails.TrailOn();
    }

    // Animation Event
    public void TrailOff()
    {
        if (trails != null)
            trails.TrailOff();
    }
}

