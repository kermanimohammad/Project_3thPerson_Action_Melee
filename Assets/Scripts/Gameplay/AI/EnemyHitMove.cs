using UnityEngine;

/// <summary>
/// Same idea as <see cref="PlayerHitMove"/>: displacement during hit reaction clips via Animation Events
/// <c>Hit_Move_Start</c> / <c>Hit_Move_End</c>, using <see cref="Health.LastHitDirLocal01"/> (HitX/HitY blend tree).
/// Place on enemy root (same GameObject as <see cref="CharacterDefense"/> / <see cref="Health"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyHitMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Health health;

    [Header("Hit displacement")]
    [SerializeField, Min(0f)] private float moveDistance = 0.85f;
    [SerializeField, Min(0.01f)] private float moveDurationSeconds = 0.12f;
    [Tooltip("If true, moves away from the incoming hit direction (knockback).")]
    [SerializeField] private bool moveAwayFromSource = true;

    private bool _active;
    private float _endTime;
    private float _speed;

    public bool IsDisplacementActive => _active;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (health == null)
            health = GetComponent<Health>();

        EnsureAnimatorEventRelay();
    }

    private void LateUpdate()
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

        Vector3 planar = dir * (_speed * Time.deltaTime);
        if (characterController != null && characterController.enabled)
        {
            Vector3 motion = planar;
            motion.y = Physics.gravity.y * Time.deltaTime;
            characterController.Move(motion);
        }
        else
        {
            transform.position += planar;
        }
    }

    public void Hit_Move_Start() => StartMove();
    public void Hit_Move_Start(AnimationEvent _) => StartMove();

    public void Hit_Move_End() => StopMove();
    public void Hit_Move_End(AnimationEvent _) => StopMove();

    private void StartMove()
    {
        if (moveDistance <= 0f)
            return;

        float dur = Mathf.Max(0.01f, moveDurationSeconds);
        _speed = moveDistance / dur;
        _endTime = Time.time + dur;
        _active = true;
    }

    private void StopMove()
    {
        _active = false;
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

        var relay = animator.GetComponent<EnemyHitMoveEventRelay>();
        if (relay == null)
            relay = animator.gameObject.AddComponent<EnemyHitMoveEventRelay>();

        relay.Bind(this);
    }
}
