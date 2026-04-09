using UnityEngine;

/// <summary>
/// Runtime battle progression values shared across systems (stamina, enemy damage, etc.).
/// Level scaling: +10% per level, capped at +100% total (x2).
/// </summary>
public static class BattleProgression
{
    public const float PerLevelBonus = 0.10f;
    public const float MaxTotalBonus = 1.00f; // +100%

    private static int _level = 1;

    public static int Level => _level;

    /// <summary>0..1 where 1 means +100% total bonus.</summary>
    public static float TotalBonus01 => Mathf.Clamp((Mathf.Max(1, _level) - 1) * PerLevelBonus, 0f, MaxTotalBonus);

    /// <summary>1..2 (capped) used for stamina regen and enemy damage.</summary>
    public static float CappedMultiplier => 1f + TotalBonus01;

    public static void SetLevel(int level)
    {
        _level = Mathf.Max(1, level);
    }
}

