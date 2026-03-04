using UnityEngine;

[System.Serializable]
public class Attack
{
    public enum ColliderType
    {
        Box
    }

    [Header("Attack Data")]
    [SerializeField] private string animationTrigger;
    [SerializeField] private float damage = 10f;
    public float timeToResetCombo = 2f;

    [Header("AOE Data")]
    [SerializeField] private ColliderType colliderType;
    [SerializeField] private float forwardOffset = 1.0f;
    [SerializeField] private float lifeTime = 0.15f;
    [SerializeField] private Vector3 boxSize = new Vector3(2f, 1.5f, 2f);

    public void TriggerAttackAnimation(Animator ownerAnimator)
    {
        ownerAnimator.ResetTrigger(animationTrigger);
        ownerAnimator.SetTrigger(animationTrigger);
    }

    public void CreateAOE(Transform owner)
    {
        Vector3 center = owner.position + owner.forward * forwardOffset;

        AreaOfEffectService.Instance.CreateBoxAOE(
            owner.gameObject,
            center,
            boxSize,
            owner.rotation,
            lifeTime,
            (target) => DamageService.Instance.DealDamage(owner.gameObject, target, damage)
            );
    }
}