using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attack-only forward motion driven by Animation Events.
/// Designed for CharacterController movement via <see cref="PlayerMotor.ForceMove"/>.
/// </summary>
public class PlayerAttackMotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor motor;

    [Header("Tuning")]
    [Tooltip("If true, motion uses transform.forward. If false, uses last planar move direction when available.")]
    [SerializeField] private bool useCharacterForward = true;

    [Tooltip("Optional easing for dash (0..1). If empty, movement is linear.")]
    [SerializeField] private AnimationCurve dashEase;

    [Header("Events")]
    [Tooltip("Invoked when an attack move finishes (Impulse applied, Dash completed, or Dash stopped).")]
    [SerializeField] private UnityEvent onAttackMoveFinished;

    public event System.Action AttackMoveFinished;

    private Coroutine dashRoutine;
    private Coroutine continuousMoveRoutine;
    private bool isContinuousMoving;
    private float continuousMoveSpeed;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// Animation Event name: <b>AttackMoveImpulse</b>
    /// Starts continuous forward movement until <see cref="AttackMoveStop"/> is called.
    /// - Event.floatParameter = speed (meters/second)
    /// - (Optional) Event.intParameter = durationMs (milliseconds). If &gt; 0, auto-stops after duration.
    /// </summary>
    public void AttackMoveImpulse(AnimationEvent e)
    {
        if (motor == null)
            return;

        float speed = Mathf.Max(0f, e.floatParameter);
        if (speed <= 0f)
            return;

        StartContinuousMove(speed, e.intParameter / 1000f);
    }

    /// <summary>
    /// Animation Event name: <b>AttackMoveDash</b>
    /// - Event.floatParameter = distance (meters)
    /// - Event.intParameter = durationMs (milliseconds)
    /// Moves forward over time (good for lunge / slide).
    /// </summary>
    public void AttackMoveDash(AnimationEvent e)
    {
        float distance = Mathf.Max(0f, e.floatParameter);
        float duration = Mathf.Max(0f, e.intParameter / 1000f);

        StartDash(distance, duration);
    }

    /// <summary>
    /// Animation Event name: <b>AttackMoveStop</b>
    /// Stops continuous move or cancels an active dash.
    /// </summary>
    public void AttackMoveStop()
    {
        StopContinuousMoveInternal(notifyFinished: true);

        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
            dashRoutine = null;
            NotifyFinished();
        }
    }

    private void StartContinuousMove(float speed, float durationSeconds)
    {
        if (motor == null)
            return;

        // Stop any existing motion first (without firing finished).
        StopContinuousMoveInternal(notifyFinished: false);
        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
            dashRoutine = null;
        }

        isContinuousMoving = true;
        continuousMoveSpeed = speed;

        if (durationSeconds > 0.0001f)
        {
            continuousMoveRoutine = StartCoroutine(ContinuousMoveForDuration(durationSeconds));
        }
        else
        {
            continuousMoveRoutine = StartCoroutine(ContinuousMoveUntilStopped());
        }
    }

    private IEnumerator ContinuousMoveUntilStopped()
    {
        while (isContinuousMoving)
        {
            motor.ForceMove(ResolveDashDirection() * (continuousMoveSpeed * Time.deltaTime));
            yield return null;
        }

        continuousMoveRoutine = null;
    }

    private IEnumerator ContinuousMoveForDuration(float durationSeconds)
    {
        float t = 0f;
        while (isContinuousMoving && t < durationSeconds)
        {
            t += Time.deltaTime;
            motor.ForceMove(ResolveDashDirection() * (continuousMoveSpeed * Time.deltaTime));
            yield return null;
        }

        continuousMoveRoutine = null;

        if (isContinuousMoving)
        {
            // Auto-stop
            StopContinuousMoveInternal(notifyFinished: true);
        }
    }

    private void StopContinuousMoveInternal(bool notifyFinished)
    {
        if (!isContinuousMoving && continuousMoveRoutine == null)
            return;

        isContinuousMoving = false;
        continuousMoveSpeed = 0f;

        if (continuousMoveRoutine != null)
        {
            StopCoroutine(continuousMoveRoutine);
            continuousMoveRoutine = null;
        }

        if (notifyFinished)
            NotifyFinished();
    }

    private void StartDash(float distance, float durationSeconds)
    {
        if (motor == null)
            return;

        AttackMoveStop();

        if (distance <= 0f)
            return;

        if (durationSeconds <= 0.0001f)
        {
            motor.ForceMove(ResolveDashDirection() * distance);
            return;
        }

        dashRoutine = StartCoroutine(DashRoutine(distance, durationSeconds, ResolveDashDirection()));
    }

    private IEnumerator DashRoutine(float distance, float durationSeconds, Vector3 dir)
    {
        float t = 0f;
        float lastAlpha = 0f;

        while (t < durationSeconds)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / durationSeconds);

            float eased = dashEase != null && dashEase.length >= 2
                ? Mathf.Clamp01(dashEase.Evaluate(alpha))
                : alpha;

            float deltaAlpha = eased - lastAlpha;
            lastAlpha = eased;

            motor.ForceMove(dir * (distance * deltaAlpha));

            yield return null;
        }

        dashRoutine = null;
        NotifyFinished();
    }

    private void NotifyFinished()
    {
        onAttackMoveFinished?.Invoke();
        AttackMoveFinished?.Invoke();
    }

    private Vector3 ResolveDashDirection()
    {
        if (useCharacterForward)
            return transform.forward.normalized;

        if (motor != null)
        {
            Vector3 v = motor.PlanarVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.0001f)
                return v.normalized;
        }

        return transform.forward.normalized;
    }
}

