using UnityEngine;

/// <summary>
/// Temporary no-op receivers for AnimationEvents that exist on imported clips
/// but are not yet implemented in gameplay scripts. Prevents "has no receiver" warnings.
/// </summary>
public sealed class AnimationEventNoopReceiver : MonoBehaviour
{
    // Unity AnimationEvent can call methods with 0 or 1 parameter (float/int/string/Object).
    // Provide overloads so any event signature resolves.
    public void foot_sound() { }
    public void foot_sound(float _) { }
    public void foot_sound(int _) { }
    public void foot_sound(string _) { }
    public void foot_sound(Object _) { }

    // Shared attack clips (player + enemy) often fire weapon-trail events. Player handles these via
    // <see cref="AnimatorEventReceiver_WeaponTrail"/>; enemies use this no-op so Unity does not warn.
    public void TrailOn() { }
    public void TrailOn(float _) { }
    public void TrailOff() { }
    public void TrailOff(float _) { }

    // Shared locomotion / jump clips (player uses PlayerAnimationSfx / jump receivers).
    public void jump() { }
    public void jump(AnimationEvent _) { }
    public void jump(float _) { }
    public void jump(int _) { }

    // Shared attack clips (player uses <see cref="PlayerAttackMotion"/>).
    public void AttackMoveImpulse() { }
    public void AttackMoveImpulse(AnimationEvent _) { }
    public void AttackMoveImpulse(float _) { }

    // Typo alias (same as AttackMoveImpulse) for shared/enemy clips.
    public void AttachMoveImpulse() { }
    public void AttachMoveImpulse(AnimationEvent _) { }
    public void AttachMoveImpulse(float _) { }
    public void AttackMoveStop() { }
    public void AttackMoveStop(AnimationEvent _) { }
    public void AttackMoveStop(float _) { }

    public void AttackMoveDash() { }
    public void AttackMoveDash(AnimationEvent _) { }
    public void AttackMoveDash(float _) { }
}

