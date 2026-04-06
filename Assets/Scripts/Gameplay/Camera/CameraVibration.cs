using System.Collections;
using UnityEngine;

/// <summary>
/// Short camera shake (explosion-style). Put on the same GameObject as the Camera (or its direct camera child).
/// Call from Animation Events as <c>camera_vib</c> only if this script lives on the Animator receiver's target;
/// usually you trigger via <see cref="AnimatorEventReceiver_PlayerAnimationSfx"/> which forwards here.
/// </summary>
public sealed class CameraVibration : MonoBehaviour
{
    [Header("Default shake")]
    [SerializeField, Min(0.01f)] private float defaultDurationSeconds = 0.22f;
    [SerializeField, Min(0f)] private float defaultPositionMagnitude = 0.12f;
    [Tooltip("Extra degrees of random euler shake.")]
    [SerializeField, Min(0f)] private float defaultRotationDegrees = 2.5f;

    [Header("Time")]
    [SerializeField] private bool useUnscaledTime;

    private Coroutine _routine;
    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;

    private void Awake()
    {
        CacheBaseTransform();
    }

    private void OnEnable()
    {
        CacheBaseTransform();
    }

    private void CacheBaseTransform()
    {
        _baseLocalPosition = transform.localPosition;
        _baseLocalRotation = transform.localRotation;
    }

    /// <summary>Animation Event: optional floatParameter = duration override (seconds).</summary>
    public void camera_vib()
    {
        Trigger(defaultDurationSeconds, defaultPositionMagnitude, defaultRotationDegrees);
    }

    /// <summary>Animation Event: floatParameter &gt; 0 overrides duration (seconds).</summary>
    public void camera_vib(AnimationEvent e)
    {
        float d = e.floatParameter > 0f ? e.floatParameter : defaultDurationSeconds;
        Trigger(d, defaultPositionMagnitude, defaultRotationDegrees);
    }

    /// <summary>Programmatic shake.</summary>
    public void Trigger(float durationSeconds, float positionMagnitude, float rotationDegrees)
    {
        if (durationSeconds <= 0f)
            return;

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ShakeRoutine(durationSeconds, positionMagnitude, rotationDegrees));
    }

    private IEnumerator ShakeRoutine(float durationSeconds, float positionMagnitude, float rotationDegrees)
    {
        Vector3 startLocalPos = transform.localPosition;
        Quaternion startLocalRot = transform.localRotation;

        float t = 0f;
        while (t < durationSeconds)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float damper = 1f - Mathf.Clamp01(t / durationSeconds);

            Vector3 posShake = Random.insideUnitSphere * positionMagnitude * damper;
            Vector3 rotShake = Random.insideUnitSphere * rotationDegrees * damper;

            transform.localPosition = startLocalPos + posShake;
            transform.localRotation = startLocalRot * Quaternion.Euler(rotShake);

            yield return null;
        }

        transform.localPosition = startLocalPos;
        transform.localRotation = startLocalRot;
        _routine = null;
    }
}
