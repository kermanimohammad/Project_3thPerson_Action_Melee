using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a UI Slider to an <see cref="EnemyStamina"/> component (enemy stamina bar).
/// Add this to the stamina slider GameObject inside the enemy prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStaminaUI : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [Tooltip("If empty, will auto-find EnemyStamina in parents on enable.")]
    [SerializeField] private EnemyStamina enemyStamina;

    private void Reset()
    {
        if (staminaSlider == null)
            staminaSlider = GetComponent<Slider>();
        if (enemyStamina == null)
            enemyStamina = GetComponentInParent<EnemyStamina>();
    }

    private void OnEnable()
    {
        if (staminaSlider == null)
            staminaSlider = GetComponent<Slider>();

        if (enemyStamina == null)
            enemyStamina = GetComponentInParent<EnemyStamina>();

        if (enemyStamina != null)
        {
            enemyStamina.OnStaminaChanged += UpdateBar;
            UpdateBar(enemyStamina.CurrentStamina, enemyStamina.MaxStamina);
        }
    }

    private void OnDisable()
    {
        if (enemyStamina != null)
            enemyStamina.OnStaminaChanged -= UpdateBar;
    }

    public void SetTarget(EnemyStamina stamina)
    {
        if (enemyStamina != null)
            enemyStamina.OnStaminaChanged -= UpdateBar;

        enemyStamina = stamina;

        if (enemyStamina != null && isActiveAndEnabled)
        {
            enemyStamina.OnStaminaChanged += UpdateBar;
            UpdateBar(enemyStamina.CurrentStamina, enemyStamina.MaxStamina);
        }
    }

    private void UpdateBar(float current, float max)
    {
        if (staminaSlider == null)
            return;

        float normalized = max <= 0f ? 0f : current / max;
        staminaSlider.value = normalized;
    }
}

