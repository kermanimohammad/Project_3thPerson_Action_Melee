using UnityEngine;

/// <summary>
/// Hit-reaction displacement driven by Animation Events:
/// - Hit_Move_Start
/// - Hit_Move_End
/// Direction is taken from the last hit direction computed by <see cref="Health"/> (HitX/HitY).
/// Place this on the player root (same object as <see cref="PlayerMotor"/>).
/// </summary>
public sealed class PlayerHitMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private Health health;

    [Header("Hit displacement")]
    [Tooltip("Total planar distance to move during the hit reaction.")]
    [SerializeField, Min(0f)] private float moveDistance = 0.75f;
    [Tooltip("Seconds to distribute Move Distance over (speed = distance / duration).")]
    [SerializeField, Min(0.01f)] private float moveDurationSeconds = 0.12f;
    [Tooltip("If true, locks movement input while hit move is active.")]
    [SerializeField] private bool lockMovementWhileMoving = true;
    [Tooltip("If true, moves AWAY from the hit source direction. If false, moves toward it.")]
    [SerializeField] private bool moveAwayFromSource = true;

    private bool _active;
    private float _endTime;
    private float _speed;
    private bool _lockedByThis;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
        if (health == null)
            health = GetComponent<Health>();

        EnsureAnimatorEventRelay();
    }

    private void Update()
    {
        if (!_active)
            return;

        if (Time.time >= _endTime)
        {
            StopMove();
            return;
        }

        Vector3 dir = ResolveWorldPlanarDirection();
        if (dir.sqrMagnitude < 1e-6f)
            return;

        motor?.ForceMove(dir * (_speed * Time.deltaTime));
    }

    // Animation Event (Hit clips): function name must be exactly "Hit_Move_Start"
    public void Hit_Move_Start() => StartMove();
    public void Hit_Move_Start(AnimationEvent _) => StartMove();

    // Animation Event (Hit clips): function name must be exactly "Hit_Move_End"
    public void Hit_Move_End() => StopMove();
    public void Hit_Move_End(AnimationEvent _) => StopMove();

    private void StartMove()
    {
        if (motor == null)
            return;

        if (moveDistance <= 0f)
            return;

        float dur = Mathf.Max(0.01f, moveDurationSeconds);
        _speed = moveDistance / dur;
        _endTime = Time.time + dur;
        _active = true;

        if (lockMovementWhileMoving && !motor.MovementLocked)
        {
            motor.SetMovementLocked(true);
            _lockedByThis = true;
        }
    }

    private void StopMove()
    {
        if (!_active)
            return;

        _active = false;
        if (_lockedByThis && motor != null)
        {
            motor.SetMovementLocked(false);
            _lockedByThis = false;
        }
    }

    private Vector3 ResolveWorldPlanarDirection()
    {
        Vector2 local01 = health != null ? health.LastHitDirLocal01 : Vector2.up;
        Vector3 local = new Vector3(local01.x, 0f, local01.y);

        if (local.sqrMagnitude < 1e-6f)
            local = Vector3.forward;

        Vector3 world = transform.TransformDirection(local.normalized);
        world.y = 0f;
        if (world.sqrMagnitude < 1e-6f)
            world = transform.forward;

        if (moveAwayFromSource)
            world = -world;

        return world.normalized;
    }

    private void EnsureAnimatorEventRelay()
    {
        var animator = GetComponentInChildren<Animator>(includeInactive: true);
        if (animator == null)
            return;

        // Events fire on the Animator GameObject; ensure a relay exists there.
        var relay = animator.GetComponent<PlayerHitMoveEventRelay>();
        if (relay == null)
            relay = animator.gameObject.AddComponent<PlayerHitMoveEventRelay>();

        relay.Bind(this);
    }
}

