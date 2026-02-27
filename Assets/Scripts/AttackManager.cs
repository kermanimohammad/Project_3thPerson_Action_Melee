using UnityEngine;

public class AttackManager : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Attack[] attacks;

    [SerializeField] private bool resetOnEqualBoundary = true;

    [SerializeField] private bool allowBuffering = true;
    [SerializeField] private float inputBufferTime = 0.2f;

    public bool InAttackState { get; private set; }
    public bool InComboWindow { get; private set; }

    private bool queuedNextAttack;
    private float queuedTime;

    private static readonly int AttackTagHash = AnimParams.AttackTag;

    private int nextAttackIndex;          
    private int activeAttackIndex = -1;   
    private float lastAttackTime = -999f;
    private float nextAllowedAttackTime;

    private void Awake()
    {
        nextAttackIndex = 0;
    }

    public bool CanAttack()
    {
        if (attacks == null || attacks.Length == 0) return false;
        if (animator == null) return false;

        if (Time.time < nextAllowedAttackTime)
            return false;


        if (InAttackState && !InComboWindow)
            return false;

        return true;
    }

    public int Attack()
    {
        if (attacks == null || attacks.Length == 0 || animator == null)
            return -1;

        if (InAttackState && !InComboWindow)
        {
            if (allowBuffering)
            {
                queuedNextAttack = true;
                queuedTime = Time.time;
            }
            return -1;
        }

        if (Time.time < nextAllowedAttackTime)
            return -1;

        if (ShouldResetCombo())
        {
            nextAttackIndex = 0;
            activeAttackIndex = -1;
            queuedNextAttack = false;
        }

        int performed = FireAttack(nextAttackIndex);
        nextAttackIndex = (nextAttackIndex + 1) % attacks.Length;

        queuedNextAttack = false;
        return performed;
    }

    private int FireAttack(int index)
    {
        Attack atk = attacks[index];

        animator.ResetTrigger(atk.animationTrigger);
        animator.SetTrigger(atk.animationTrigger);

        activeAttackIndex = index;
        lastAttackTime = Time.time;
        nextAllowedAttackTime = Time.time + atk.lockoutTime;

        return index;
    }

    private void Update()
    {
        if (animator == null || attacks == null || attacks.Length == 0)
        {
            InAttackState = false;
            InComboWindow = false;
            return;
        }

        var current = animator.GetCurrentAnimatorStateInfo(0);
        var next = animator.GetNextAnimatorStateInfo(0);

        bool currentIsAttack = current.tagHash == AttackTagHash;
        bool nextIsAttack = next.tagHash == AttackTagHash;

        InAttackState = currentIsAttack || nextIsAttack;

        if (!InAttackState)
        {
            InComboWindow = false;

            activeAttackIndex = -1;
            queuedNextAttack = false;
            return;
        }

        float nt = currentIsAttack ? current.normalizedTime : next.normalizedTime;
        float normalized01 = nt - Mathf.Floor(nt);

        int idx = activeAttackIndex >= 0 ? activeAttackIndex : Mathf.Clamp(nextAttackIndex - 1, 0, attacks.Length - 1);

        float windowStart = attacks[idx].comboWindowStart;
        InComboWindow = normalized01 >= windowStart;

        if (allowBuffering && queuedNextAttack)
        {
            if (Time.time - queuedTime > inputBufferTime)
            {
                queuedNextAttack = false;
            }
            else if (InComboWindow && Time.time >= nextAllowedAttackTime)
            {
                int performed = FireAttack(nextAttackIndex);
                nextAttackIndex = (performed + 1) % attacks.Length;
                queuedNextAttack = false;
            }
        }
    }

    private bool ShouldResetCombo()
    {
        if (activeAttackIndex < 0)
            return true;

        float dt = Time.time - lastAttackTime;
        float window = attacks[activeAttackIndex].timeToResetCombo;

        return resetOnEqualBoundary ? (dt >= window) : (dt > window);
    }

    public void SpawnDamageAOE()
    {
        // to do for later.
    }
}