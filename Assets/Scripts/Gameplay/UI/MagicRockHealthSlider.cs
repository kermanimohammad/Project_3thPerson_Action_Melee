using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a <see cref="Slider"/> (0–1) to a <see cref="MagicRockBreakable"/> rock's current/max health.
/// </summary>
public sealed class MagicRockHealthSlider : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private MagicRockBreakable rock;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [Tooltip("Optional. Shows current/max HP, e.g. 12/50. If empty, tries GetComponentInChildren<TextMeshProUGUI>.")]
    [SerializeField] private TextMeshProUGUI healthValueLabel;
    [Tooltip("String.Format: {0} = current, {1} = max")]
    [SerializeField] private string healthLabelFormat = "{0}/{1}";

    private void Awake()
    {
        if (healthValueLabel == null)
            healthValueLabel = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (rock != null)
            rock.HealthChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (rock != null)
            rock.HealthChanged -= Refresh;
    }

    private void Refresh()
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = ComputeNormalized01();
        }

        ApplyLabel();
    }

    private float ComputeNormalized01()
    {
        if (rock == null)
            return 0f;

        if (!rock.isActiveAndEnabled || rock.IsBroken)
            return 0f;

        float max = rock.MaxHealth;
        if (max <= 0f)
            return 0f;

        return Mathf.Clamp01(rock.CurrentHealth / max);
    }

    private void ApplyLabel()
    {
        if (healthValueLabel == null || string.IsNullOrEmpty(healthLabelFormat))
            return;

        if (rock == null)
        {
            healthValueLabel.text = string.Format(healthLabelFormat, 0, 0);
            return;
        }

        float cur = rock.IsBroken ? 0f : Mathf.Max(0f, rock.CurrentHealth);
        float max = rock.MaxHealth;
        healthValueLabel.text = string.Format(healthLabelFormat, Mathf.RoundToInt(cur), Mathf.RoundToInt(max));
    }
}
