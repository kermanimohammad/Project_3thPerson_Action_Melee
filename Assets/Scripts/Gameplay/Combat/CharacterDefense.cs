using UnityEngine;

public class CharacterDefense : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [field: SerializeField] [Range(0, 1)] public float DamageReductionPercentage { get; private set; }

    public bool IsDefending { get; private set; }

    public void StartDefend()
	{
        IsDefending = true;
        animator.SetBool(AnimParams.IsDefending, IsDefending);
	}

    public void StopDefend()
    {
        IsDefending = false;
        animator.SetBool(AnimParams.IsDefending, IsDefending);
    }

}
