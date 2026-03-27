using UnityEngine;

/// <summary>
/// Shared logic for menu pickers: under one parent, exactly one direct child is active.
/// </summary>
public static class MenuEquipmentSingleChildUtility
{
    public static void ApplySingleActiveChild(Transform root, int desiredIndex, Object context, string rootLabel)
    {
        if (root == null)
        {
            Debug.LogWarning($"{context?.GetType().Name}: {rootLabel} is not assigned.", context);
            return;
        }

        int count = root.childCount;
        if (count == 0) return;

        int active = Mathf.Clamp(desiredIndex, 0, count - 1);
        for (int i = 0; i < count; i++)
            root.GetChild(i).gameObject.SetActive(i == active);
    }
}
