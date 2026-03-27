using System;
using UnityEngine;

/// <summary>
/// Persisted hero + equipment choices from the main menu (PlayerPrefs).
/// Read in BattleArea (or any scene) via <see cref="BattleLoadoutPersistence.TryLoad"/>.
/// </summary>
[Serializable]
public struct BattleMenuLoadout
{
    public int CharacterIndex;
    public int WeaponIndex;
    public int HelmetIndex;
    public int ShieldIndex;

    public bool IsValid =>
        CharacterIndex >= 0 && CharacterIndex <= 1;
}

/// <summary>
/// Saves / loads <see cref="BattleMenuLoadout"/> using PlayerPrefs.
/// </summary>
public static class BattleLoadoutPersistence
{
    const string KeyPrefix = "BattleLoadout_";
    const string KeyCharacter = KeyPrefix + "Character";
    const string KeyWeapon = KeyPrefix + "Weapon";
    const string KeyHelmet = KeyPrefix + "Helmet";
    const string KeyShield = KeyPrefix + "Shield";
    const string KeyHasData = KeyPrefix + "HasData";

    public static void Save(in BattleMenuLoadout loadout)
    {
        PlayerPrefs.SetInt(KeyHasData, 1);
        PlayerPrefs.SetInt(KeyCharacter, loadout.CharacterIndex);
        PlayerPrefs.SetInt(KeyWeapon, loadout.WeaponIndex);
        PlayerPrefs.SetInt(KeyHelmet, loadout.HelmetIndex);
        PlayerPrefs.SetInt(KeyShield, loadout.ShieldIndex);
        PlayerPrefs.Save();
    }

    /// <summary>Returns false if nothing was saved yet.</summary>
    public static bool TryLoad(out BattleMenuLoadout loadout)
    {
        loadout = default;
        if (PlayerPrefs.GetInt(KeyHasData, 0) == 0)
            return false;

        loadout.CharacterIndex = PlayerPrefs.GetInt(KeyCharacter, 0);
        loadout.WeaponIndex = PlayerPrefs.GetInt(KeyWeapon, 0);
        loadout.HelmetIndex = PlayerPrefs.GetInt(KeyHelmet, 0);
        loadout.ShieldIndex = PlayerPrefs.GetInt(KeyShield, 0);
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyHasData);
        PlayerPrefs.DeleteKey(KeyCharacter);
        PlayerPrefs.DeleteKey(KeyWeapon);
        PlayerPrefs.DeleteKey(KeyHelmet);
        PlayerPrefs.DeleteKey(KeyShield);
    }
}
