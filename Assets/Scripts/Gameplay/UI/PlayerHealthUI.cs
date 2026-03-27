using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Health playerHealth;

    private void OnEnable()
    {
        playerHealth.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDisable()
    {
        playerHealth.OnHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar(float current, float max)
    {
        healthSlider.value = (float)current / max;
    }
}