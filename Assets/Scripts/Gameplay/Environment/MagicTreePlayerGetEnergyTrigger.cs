using UnityEngine;

/// <summary>
/// When a magic tree benefit finishes (heal full, level-up, door repair), fires an Animator trigger on the player.
/// </summary>
public static class MagicTreePlayerGetEnergyTrigger
{
    public static void Fire(Animator explicitAnimator, string triggerName, string playerTag = "Player")
    {
        if (string.IsNullOrEmpty(triggerName))
            return;

        Animator anim = explicitAnimator;
        if (anim == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null)
                anim = p.GetComponentInChildren<Animator>();
        }

        if (anim == null)
            return;

        anim.enabled = true;
        int id = Animator.StringToHash(triggerName);
        anim.ResetTrigger(id);
        anim.SetTrigger(id);
    }
}
