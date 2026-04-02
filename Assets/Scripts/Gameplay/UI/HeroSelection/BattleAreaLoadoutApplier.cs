using UnityEngine;

/// <summary>
/// BattleArea-side loader for the MainMenu loadout (character + equipment indices).
/// Assumes all weapons are already under right hand, shields under left hand, helmets under head;
/// we only toggle GameObjects by index.
/// </summary>
public class BattleAreaLoadoutApplier : MonoBehaviour
{
    [Header("Characters in scene")]
    [Tooltip("0 = Paladin, 1 = Erika (match MainMenu indices). Only the selected one will be active.")]
    [SerializeField] private GameObject[] characterRoots = new GameObject[2];

    [Header("Equipment (per character)")]
    [SerializeField] private EquipmentSet[] equipmentByCharacter = new EquipmentSet[2];

    [Header("HUD (optional)")]
    [Tooltip("If assigned, the HUD will bind to the selected character's Health. If null, the first PlayerHealthUI in scene is used.")]
    [SerializeField] private PlayerHealthUI playerHealthUi;

    [System.Serializable]
    public class EquipmentSet
    {
        public GameObject[] weaponsRightHand;
        public GameObject[] shieldsLeftHand;
        public GameObject[] helmetsHead;
    }

    [Header("Behaviour")]
    [SerializeField] private bool applyOnStart = true;

    private void Start()
    {
        if (!applyOnStart)
            return;
        ApplyFromSavedLoadout();
    }

    [ContextMenu("Apply From Saved Loadout")]
    public void ApplyFromSavedLoadout()
    {
        if (!BattleLoadoutPersistence.TryLoad(out var loadout))
        {
            // No saved data; default to character 0 and disable all equipment extras.
            Apply(loadout);
            return;
        }

        Apply(loadout);
    }

    public void Apply(in BattleMenuLoadout loadout)
    {
        int characterIndex = Mathf.Clamp(loadout.CharacterIndex, 0, characterRoots.Length - 1);

        for (int i = 0; i < characterRoots.Length; i++)
        {
            if (characterRoots[i] != null)
                characterRoots[i].SetActive(i == characterIndex);
        }

        // Auto-bind HUD to the selected character's Health.
        if (characterIndex >= 0 && characterIndex < characterRoots.Length && characterRoots[characterIndex] != null)
        {
            Health h = characterRoots[characterIndex].GetComponentInChildren<Health>(includeInactive: false);
            PlayerHealthUI ui = playerHealthUi != null ? playerHealthUi : Object.FindFirstObjectByType<PlayerHealthUI>();
            if (ui != null)
                ui.SetTargetHealth(h);
        }

        if (equipmentByCharacter == null || equipmentByCharacter.Length == 0)
            return;

        if (characterIndex < 0 || characterIndex >= equipmentByCharacter.Length)
            return;

        EquipmentSet set = equipmentByCharacter[characterIndex];
        SetActiveByIndex(set.weaponsRightHand, loadout.WeaponIndex);
        SetActiveByIndex(set.helmetsHead, loadout.HelmetIndex);
        SetActiveByIndex(set.shieldsLeftHand, loadout.ShieldIndex);
    }

    private static void SetActiveByIndex(GameObject[] items, int index)
    {
        if (items == null || items.Length == 0)
            return;

        int clamped = Mathf.Clamp(index, -1, items.Length - 1);

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                items[i].SetActive(i == clamped);
        }
    }
}

