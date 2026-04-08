using UnityEngine;

public class CharacterDefense : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [field: SerializeField] [Range(0, 1)] public float DamageReductionPercentage { get; private set; }

    [Tooltip("If true, turns off animator root motion while defending. Use on enemies to stop block animations sliding toward the target; leave off for player if your block clip uses root motion.")]
    [SerializeField] private bool disableRootMotionWhileDefending;

    public bool IsDefending { get; private set; }

    private bool _restoreRootMotionAfterDefend;
    private bool _rootMotionBeforeDefend;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(includeInactive: true);
    }

    public void StartDefend()
    {
        IsDefending = true;

        if (disableRootMotionWhileDefending && animator != null)
        {
            _rootMotionBeforeDefend = animator.applyRootMotion;
            _restoreRootMotionAfterDefend = true;
            animator.applyRootMotion = false;
        }

        if (animator != null)
            animator.SetBool(AnimParams.IsDefending, IsDefending);
    }

    public void StopDefend()
    {
        IsDefending = false;

        if (animator != null)
            animator.SetBool(AnimParams.IsDefending, IsDefending);

        if (_restoreRootMotionAfterDefend && animator != null)
        {
            animator.applyRootMotion = _rootMotionBeforeDefend;
            _restoreRootMotionAfterDefend = false;
        }
    }

}
