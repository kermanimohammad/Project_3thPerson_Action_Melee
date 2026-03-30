using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Tracks every runtime <see cref="InputActionAsset"/> instance (e.g. from <c>new PlayerInputActions()</c>)
/// so binding changes in Settings can be reloaded from <see cref="InputRebindPersistence"/> immediately.
/// </summary>
public static class InputBindingRuntimeSync
{
    private static readonly List<InputActionAsset> RegisteredAssets = new List<InputActionAsset>(8);

    public static void Register(InputActionAsset asset)
    {
        if (asset == null || RegisteredAssets.Contains(asset))
            return;
        RegisteredAssets.Add(asset);
    }

    public static void Unregister(InputActionAsset asset)
    {
        if (asset == null)
            return;
        RegisteredAssets.Remove(asset);
    }

    /// <summary>Re-applies persisted JSON to all registered gameplay assets (after rebind or reset).</summary>
    public static void ApplySavedBindingsToAllRegistered()
    {
        for (int i = RegisteredAssets.Count - 1; i >= 0; i--)
        {
            var a = RegisteredAssets[i];
            if (a == null)
            {
                RegisteredAssets.RemoveAt(i);
                continue;
            }

            InputRebindPersistence.LoadAndApply(a);
        }
    }
}
