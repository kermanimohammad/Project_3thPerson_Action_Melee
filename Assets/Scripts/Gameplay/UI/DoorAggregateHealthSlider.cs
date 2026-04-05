using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows combined health of all matching <see cref="DoorBreakable"/> as a slider (0–1).
/// Use the same tag filter as <see cref="MagicTreeDoorRestorer.onlyDoorsWithTag"/> when scoping to puzzle doors.
/// Optional <see cref="healthValueLabel"/> shows e.g. "200/300" (current sum / max sum).
/// </summary>
public class DoorAggregateHealthSlider : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [Tooltip("Optional. Shows current/max aggregate HP, e.g. 200/300. If empty, tries GetComponentInChildren<TextMeshProUGUI>.")]
    [SerializeField] private TextMeshProUGUI healthValueLabel;
    [Tooltip("Match MagicTreeDoorRestorer.onlyDoorsWithTag. Empty = all DoorBreakable in the scene.")]
    [SerializeField] private string onlyDoorsWithTag = "";
    [Tooltip("When no matching DoorBreakable exists (e.g. after repair replaced them with intact doors), treat as 100% full.")]
    [SerializeField] private bool fullWhenNoBreakables = true;
    [Tooltip("Optional: while this tree is repairing, skip Refresh so the tree can animate the same slider without events snapping it back.")]
    [SerializeField] private MagicTreeDoorRestorer magicTreeDoorRestorer;

    private void Awake()
    {
        if (healthValueLabel == null)
            healthValueLabel = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        DoorBreakable.OnAnyDoorHealthChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        DoorBreakable.OnAnyDoorHealthChanged -= Refresh;
    }

    private void Refresh()
    {
        if (magicTreeDoorRestorer != null && magicTreeDoorRestorer.IsRepairCharging)
            return;
        if (healthSlider == null && healthValueLabel == null)
            return;

        DoorBreakable[] doors = Object.FindObjectsByType<DoorBreakable>(FindObjectsSortMode.None);
        float totalMax = 0f;
        float totalCurrent = 0f;

        for (int i = 0; i < doors.Length; i++)
        {
            DoorBreakable d = doors[i];
            if (d == null || !d.isActiveAndEnabled)
                continue;
            if (!DoorMatchesFilter(d))
                continue;

            float maxH = d.DoorMaxHealth;
            if (maxH <= 0f)
                continue;

            totalMax += maxH;
            totalCurrent += Mathf.Clamp(d.DoorCurrentHealth, 0f, maxH);
        }

        if (totalMax <= 0f)
        {
            if (healthSlider != null)
                healthSlider.value = fullWhenNoBreakables ? 1f : 0f;
            ApplyLabel(0f, 0f, hasValidAggregate: false);
            return;
        }

        if (healthSlider != null)
            healthSlider.value = totalCurrent / totalMax;
        ApplyLabel(totalCurrent, totalMax, hasValidAggregate: true);
    }

    private void ApplyLabel(float totalCurrent, float totalMax, bool hasValidAggregate)
    {
        if (healthValueLabel == null)
            return;

        if (!hasValidAggregate || totalMax <= 0f)
        {
            healthValueLabel.text = "-";
            return;
        }

        int cur = Mathf.RoundToInt(totalCurrent);
        int max = Mathf.RoundToInt(totalMax);
        healthValueLabel.text = $"{cur}/{max}";
    }

    private bool DoorMatchesFilter(DoorBreakable door)
    {
        if (string.IsNullOrEmpty(onlyDoorsWithTag))
            return true;

        Transform t = door.transform;
        while (t != null)
        {
            if (t.CompareTag(onlyDoorsWithTag))
                return true;
            t = t.parent;
        }
        return false;
    }
}
