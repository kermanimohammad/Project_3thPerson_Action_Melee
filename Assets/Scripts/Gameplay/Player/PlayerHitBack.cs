using UnityEngine;

/// <summary>
/// Knockback/backstep for the Player, driven by Animation Events on the Hit reaction clip:
/// - back_start
/// - back_end
/// Place this on the same GameObject as PlayerMotor (Player root).
/// </summary>
public sealed class PlayerHitBack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor motor;

    [Header("Back motion")]
    [Tooltip("Total planar distance to move backward while back is active.")]
    [SerializeField, Min(0f)] private float backDistance = 1.25f;
    [Tooltip("Seconds to distribute Back Distance over (speed = distance / duration).")]
    [SerializeField, Min(0.01f)] private float backDurationSeconds = 0.14f;
    [Tooltip("If true, locks movement input while back motion is active.")]
    [SerializeField] private bool lockMovementWhileBacking = true;
    [Tooltip("If true, use the Player's facing (-forward). If false, use a fixed world direction.")]
    [SerializeField] private bool useFacingDirection = true;
    [SerializeField] private Vector3 worldDirection = Vector3.back;

    private bool _active;
    private float _endTime;
    private float _speed;
    private bool _lockedByThis;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        if (!_active)
            return;

        if (Time.time >= _endTime)
        {
            StopBack();
            return;
        }

        Vector3 dir = ResolveDirection();
        Vector3 planar = dir * (_speed * Time.deltaTime);
        motor?.ForceMove(planar);
    }

    // Animation Event (Hit clip): function name must be exactly "back_start"
    public void back_start() => StartBack();
    public void back_start(AnimationEvent _) => StartBack();

    // Animation Event (Hit clip): function name must be exactly "back_end"
    public void back_end() => StopBack();
    public void back_end(AnimationEvent _) => StopBack();

    private void StartBack()
    {
        if (motor == null)
            return;

        if (backDistance <= 0f)
            return;

        float dur = Mathf.Max(0.01f, backDurationSeconds);
        _speed = backDistance / dur;
        _endTime = Time.time + dur;
        _active = true;

        if (lockMovementWhileBacking && !motor.MovementLocked)
        {
            motor.SetMovementLocked(true);
            _lockedByThis = true;
        }
    }

    private void StopBack()
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

    private Vector3 ResolveDirection()
    {
        Vector3 d = useFacingDirection ? -transform.forward : worldDirection;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-6f)
            return Vector3.zero;
        return d.normalized;
    }
}

