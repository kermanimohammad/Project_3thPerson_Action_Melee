using UnityEngine;

public class AttackManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Attack[] attacks;
    
    [Header("Buffering")]
    [SerializeField] private bool allowBuffering = true;
    [SerializeField] private float inputBufferTime = 0.2f;

    private static readonly int AttackTagHash = AnimParams.AttackTag;

    // ---- Internal State ----
    private int nextAttackIndex = 0;
    private int activeAttackIndex = -1;

    private bool comboWindowOpen = false;
    private bool queuedNextAttack = false;
    private float queuedTime;

    private float lastAttackTime = -999f;

    public bool InAttackState()
    {
        if (animator == null)
            return false;

        var current = animator.GetCurrentAnimatorStateInfo(0);
        return current.tagHash == AttackTagHash;
    }

    public int TryAttack()
    {
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

    private bool CanAttack()
    {
        if (attacks == null || attacks.Length == 0 || animator == null)
            return false;

        if (InAttackState())
        {
            if (!comboWindowOpen)
            {
                if (allowBuffering)
                {
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

    private bool IsValidActiveAttack() => activeAttackIndex >= 0 && activeAttackIndex < attacks.Length;

}