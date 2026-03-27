using UnityEngine;

/// <summary>
/// While an interactive rebind is active, other menu scripts should ignore gamepad/keyboard shortcuts
/// (back, tab cycle, UI Cancel) so those controls can be bound without side effects.
/// </summary>
public static class InputRebindSession
{
    public static bool IsActive { get; private set; }

    public static void Begin()
    {
        IsActive = true;
    }

    public static void End()
    {
        IsActive = false;
    }
}
