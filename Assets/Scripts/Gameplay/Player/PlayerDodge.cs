using System.Collections;
using UnityEngine;

public class PlayerDodge : MonoBehaviour
{
    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 10f;
    [SerializeField] private float dodgeDuration = 0.25f;
    [SerializeField] private float dodgeCooldown = 0.6f;
    [SerializeField] private float moveDeadzone = 0.1f;

    [Header("References")]
    [SerializeField] private PlayerInputRouter input;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    private bool isDodging;
    private float nextDodgeTime;

    private void OnEnable()
    {
        if (input != null)
            input.DodgePressed += TryDodge;
    }

    private void OnDisable()
    {
        if (input != null)
            input.DodgePressed -= TryDodge;
    }

    private void TryDodge()
    {
        if (motor == null || input == null || animator == null)
            return;

        if (Time.time < nextDodgeTime)
            return;

        if (isDodging || motor.MovementLocked)
            return;

        if (!motor.IsGrounded)
            return;

        if (combat != null && combat.IsDefending)
            return;

        nextDodgeTime = Time.time + dodgeCooldown;
        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        isDodging = true;
        motor.SetMovementLocked(true);

        animator.ResetTrigger(AnimParams.Dodge);
        animator.SetTrigger(AnimParams.Dodge);

        yield return new WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).shortNameHash == AnimParams.DodgeState);

        Vector3 dodgeDir = ResolveDodgeDirection();

        float t = 0f;
        while (t < dodgeDuration)
        {
            t += Time.deltaTime;
            motor.ForceMove(dodgeDir * (dodgeSpeed * Time.deltaTime));
            yield return null;
        }

        yield return new WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).shortNameHash != AnimParams.DodgeState);

        motor.SetMovementLocked(false);
        isDodging = false;
    }

    private Vector3 ResolveDodgeDirection()
    {
        Vector2 move = input.Move;

        if (cameraTransform != null && move.sqrMagnitude >= moveDeadzone * moveDeadzone)
        {
            Vector3 dir = new Vector3(move.x, 0f, move.y).normalized;
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            return (Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward).normalized;
        }

        return transform.forward.normalized;
    }
}