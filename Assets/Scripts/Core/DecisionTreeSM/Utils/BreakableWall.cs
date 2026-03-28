using UnityEngine;

public class BreakableWall : MonoBehaviour
{
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private EnemyGroupAI groupAI;

    private float currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeHit(float damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0f)
            Break();
    }

    private void Break()
    {
        if (groupAI != null)
            groupAI.SetWallBroken();

        Destroy(gameObject);
    }
}