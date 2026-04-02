using UnityEngine;

/// <summary>
/// Put this on the SAME GameObject that has the Animator playing the clip.
/// Forwards Animation Events to <see cref="PlayerCombat"/> on this object or a parent.
/// </summary>
public class AnimatorEventReceiver_PlayerCombat : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponent<PlayerCombat>();
        if (combat == null)
            combat = GetComponentInParent<PlayerCombat>();
    }

    // Animation Event: SpacialAttack
    public void PerformSpecialHit()
    {
        if (combat != null)
            combat.PerformSpecialHit();
    }

    // Animation Event: SpacialAttack
    public void ResetAttack()
    {
        if (combat != null)
            combat.ResetAttack();
    }
}

