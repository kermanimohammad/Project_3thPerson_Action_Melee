using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Persists Input System binding overrides to PlayerPrefs (JSON).
/// Store paths like "&lt;Keyboard&gt;/space" so it is layout/language independent and "English" by definition.
/// </summary>
public static class InputRebindPersistence
{
    private const string KeyPrefix = "InputRebinds_";

    public static void Save(InputActionAsset asset)
    {
        if (asset == null) return;
        string json = asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(KeyPrefix + asset.name, json);
        PlayerPrefs.Save();
    }

    public static void LoadAndApply(InputActionAsset asset)
    {
        if (asset == null) return;
        string key = KeyPrefix + asset.name;
        if (!PlayerPrefs.HasKey(key)) return;

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return;
        asset.LoadBindingOverridesFromJson(json);
    }

    public static void Clear(InputActionAsset asset)
    {
        if (asset == null) return;
        PlayerPrefs.DeleteKey(KeyPrefix + asset.name);
        PlayerPrefs.Save();
        asset.RemoveAllBindingOverrides();
    }
}

