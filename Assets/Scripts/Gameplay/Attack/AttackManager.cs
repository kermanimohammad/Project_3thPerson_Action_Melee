using UnityEngine;

public class AttackManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Attack[] attacks;
    
    [Header("Buffering")]
    [SerializeField] private bool allowBuffering = true;
    [SerializeField] private float inputBufferTime = 0.2f;
    [Tooltip("If no new attack input occurs for this many seconds, clear any buffered/queued combo so the character returns to locomotion after the current attack ends.")]
    [SerializeField] private float cancelBufferedComboAfterSeconds = 1.0f;

    private static readonly int AttackTagHash = AnimParams.AttackTag;

    // ---- Internal State ----
    private int nextAttackIndex = 0;
    private int activeAttackIndex = -1;

    private bool comboWindowOpen = false;
    private bool queuedNextAttack = false;
    private float queuedTime;

    private float lastAttackTime = -999f;
    private float lastAttackInputTime = -999f;

    private void Update()
    {
        if (!allowBuffering)
            return;

        if (!queuedNextAttack)
            return;

        // If the player stopped attacking, drop the queued combo continuation.
        if (Time.time - lastAttackInputTime >= cancelBufferedComboAfterSeconds)
        {
            ClearBufferedCombo();
        }
    }

    public bool InAttackState()
    {
        if (animator == null)
            return false;

        var current = animator.GetCurrentAnimatorStateInfo(0);
        return current.tagHash == AttackTagHash;
    }

    public int TryAttack()
    {
        lastAttackInputTime = Time.time;

        if (!CanAttack())
            return -1;

        if (ShouldResetCombo())
        {
            ResetCombo();
        }

        return PerformAttack(nextAttackIndex);
    }

    // Called from animation event
    public void OpenComboWindow()
    {
        comboWindowOpen = true;

        if (allowBuffering && queuedNextAttack)
        {
            // If the queued input got stale, drop it instead of auto-continuing the chain.
            if (Time.time - lastAttackInputTime >= cancelBufferedComboAfterSeconds)
            {
                ClearBufferedCombo();
                return;
            }

            if (Time.time - queuedTime <= inputBufferTime)
            {
                queuedNextAttack = false;
                TryAttack();
            }
            else
            {
                queuedNextAttack = false;
            }
        }
    }

    // Called from animation event (impact frame)
    public void CreateAOE()
    {
        if (!IsValidActiveAttack())
            return;

        attacks[activeAttackIndex].CreateAOE(transform);
    }

    public bool CanAttack()
    {
        if (attacks == null || attacks.Length == 0 || animator == null)
            return false;

        if (InAttackState())
        {
            if (!comboWindowOpen)
            {
                if (allowBuffering)
                {
                    lastAttackInputTime = Time.time;
                    queuedNextAttack = true;
                    queuedTime = Time.time;
                }
                return false;
            }
        }

        return true;
    }

    private int PerformAttack(int index)
    {
        Attack attack = attacks[index];

        attack.TriggerAttackAnimation(animator);

        comboWindowOpen = false;

        activeAttackIndex = index;
        lastAttackTime = Time.time;

        nextAttackIndex = (index + 1) % attacks.Length;

        queuedNextAttack = false;

        return index;
    }

    private bool ShouldResetCombo()
    {
        if (activeAttackIndex < 0)
            return true;

        float dt = Time.time - lastAttackTime;
        float resetWindow = attacks[activeAttackIndex].timeToResetCombo;

        return dt >= resetWindow;
    }

    private void ResetCombo()
    {
        nextAttackIndex = 0;
        activeAttackIndex = -1;
        queuedNextAttack = false;
        comboWindowOpen = false;
    }

    private void ClearBufferedCombo()
    {
        queuedNextAttack = false;
        comboWindowOpen = false;
        nextAttackIndex = 0;
        activeAttackIndex = -1;

        // Defensive: clear any leftover triggers so we don't chain unexpectedly.
        if (animator != null && attacks != null)
        {
            for (int i = 0; i < attacks.Length; i++)
            {
                animator.ResetTrigger(attacks[i].AnimationTrigger);
            }
        }
    }

    private bool IsValidActiveAttack() => activeAttackIndex >= 0 && activeAttackIndex < attacks.Length;

    /// <summary>
    /// Clears any queued/buffered combo continuation and resets combo indices.
    /// Useful when another action (e.g., Defend) should cancel pending attacks.
    /// </summary>
    public void CancelBufferedComboNow()
    {
        ClearBufferedCombo();
    }

}