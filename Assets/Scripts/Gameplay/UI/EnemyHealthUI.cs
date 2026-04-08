using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;

    private Health health;

    private void Awake()
    {
        if (healthSlider == null)
            healthSlider = GetComponent<Slider>();
        health = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponentInParent<Health>();
        if (health == null || healthSlider == null)
            return;

        health.OnHealthChanged += UpdateHealthBar;
        UpdateHealthBar(health.CurrentHealth, health.MaxHealth);
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar(float current, float max)
    {
        if (healthSlider == null)
            return;
        healthSlider.maxValue = Mathf.Max(1f, max);
        healthSlider.value = Mathf.Clamp(current, healthSlider.minValue, healthSlider.maxValue);
    }
}