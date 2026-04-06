using UnityEngine;

/// <summary>
/// Put this on the same GameObject as the Animator that fires the Animation Events.
/// Forwards:
/// - foot_sound
/// - jump
/// - kickRun
/// - s_attack / S_Attack
/// - camera_vib (forwards to <see cref="CameraVibration"/> on the main camera)
/// to <see cref="PlayerAnimationSfx"/> on the Player root.
/// </summary>
public sealed class AnimatorEventReceiver_PlayerAnimationSfx : MonoBehaviour
{
    [SerializeField] private PlayerAnimationSfx sfx;
    [Tooltip("Optional; if empty, uses Camera.main.")]
    [SerializeField] private CameraVibration cameraVibration;

    private void Awake()
    {
        if (sfx == null)
            sfx = GetComponentInParent<PlayerAnimationSfx>();
        EnsureCameraVibration();
    }

    private void EnsureCameraVibration()
    {
        if (cameraVibration != null)
            return;
        if (Camera.main != null)
            cameraVibration = Camera.main.GetComponent<CameraVibration>();
    }

    // Animation Event
    public void foot_sound()
    {
        if (sfx != null)
            sfx.foot_sound();
    }

    public void foot_sound(AnimationEvent e)
    {
        if (sfx != null)
            sfx.foot_sound(e);
    }

    // Animation Event
    public void jump()
    {
        if (sfx != null)
            sfx.jump();
    }

    public void jump(AnimationEvent e)
    {
        if (sfx != null)
            sfx.jump(e);
    }

    // Animation Event
    public void kickRun()
    {
        if (sfx != null)
            sfx.kickRun();
    }

    public void kickRun(AnimationEvent e)
    {
        if (sfx != null)
            sfx.kickRun(e);
    }

    public void s_attack()
    {
        if (sfx != null)
            sfx.s_attack();
    }

    public void s_attack(AnimationEvent e)
    {
        if (sfx != null)
            sfx.s_attack(e);
    }

    public void S_Attack()
    {
        if (sfx != null)
            sfx.S_Attack();
    }

    public void S_Attack(AnimationEvent e)
    {
        if (sfx != null)
            sfx.S_Attack(e);
    }

    /// <summary>Animation Event — camera shake (S_Attack, explosions, etc.).</summary>
    public void camera_vib()
    {
        EnsureCameraVibration();
        if (cameraVibration != null)
            cameraVibration.camera_vib();
    }

    public void camera_vib(AnimationEvent e)
    {
        EnsureCameraVibration();
        if (cameraVibration != null)
            cameraVibration.camera_vib(e);
    }
}

