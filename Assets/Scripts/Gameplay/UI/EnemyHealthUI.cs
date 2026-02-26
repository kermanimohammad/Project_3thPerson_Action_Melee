using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;

    private Health health;

    private void Awake()
    {
        health = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar(int current, int max)
    {
        healthSlider.maxValue = max;
        healthSlider.value = current;
    }
}