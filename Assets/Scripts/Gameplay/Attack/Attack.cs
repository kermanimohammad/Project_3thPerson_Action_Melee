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
    [SerializeField] private float damageToCharacters = 10f;
    [SerializeField] private float damageToDoors = 0f;
    public float timeToResetCombo = 2f;

    public string AnimationTrigger => animationTrigger;

    [Header("AOE Data")]
    [SerializeField] private ColliderType colliderType;
    [SerializeField] private float forwardOffset = 1.0f;
    [SerializeField] private float lifeTime = 0.15f;
    [SerializeField] private Vector3 boxSize = new Vector3(2f, 1.5f, 2f);

    private static bool s_loggedMissingServices;

    public void TriggerAttackAnimation(Animator ownerAnimator)
    {
        ownerAnimator.ResetTrigger(animationTrigger);
        ownerAnimator.SetBool("isGrounded", true);
        ownerAnimator.SetTrigger(animationTrigger);
    }

    public void CreateAOE(Transform owner)
    {
        if (owner == null)
            return;

        EnsureServicesExist();

        if (AreaOfEffectService.Instance == null || DamageService.Instance == null)
        {
            if (!s_loggedMissingServices)
            {
                s_loggedMissingServices = true;
                Debug.LogError(
                    "AOE/Damage services are not available. " +
                    "Add `LocalAreaOfEffectService` and `LocalDamageService` to the scene, " +
                    "or ensure they are created before attacks can trigger AOE."
                );
            }
            return;
        }

        Vector3 center = owner.position + owner.forward * forwardOffset;

        AreaOfEffectService.Instance.CreateBoxAOE(
            owner.gameObject,
            center,
            boxSize,
            owner.rotation,
            lifeTime,
            (target) => DamageService.Instance.DealDamage(owner.gameObject, target, damageToCharacters)
            );
    }

    private static void EnsureServicesExist()
    {
        if (AreaOfEffectService.Instance == null)
        {
            var existing = Object.FindFirstObjectByType<LocalAreaOfEffectService>();
            if (existing != null)
            {
                AreaOfEffectService.Register(existing);
            }
            else
            {
                new GameObject("LocalAreaOfEffectService").AddComponent<LocalAreaOfEffectService>();
            }
        }

        if (DamageService.Instance == null)
        {
            var existing = Object.FindFirstObjectByType<LocalDamageService>();
            if (existing != null)
            {
                DamageService.Register(existing);
            }
            else
            {
                new GameObject("LocalDamageService").AddComponent<LocalDamageService>();
            }
        }
    }
}