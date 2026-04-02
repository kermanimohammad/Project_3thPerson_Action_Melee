using TMPro;
using UnityEngine;

/// <summary>
/// Sets the BattleArea HUD name label based on the selected character saved in MainMenu.
/// </summary>
public class BattleAreaAvatarNameLabel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI targetLabel;
    [SerializeField] private string paladinName = "Paladin";
    [SerializeField] private string erikaName = "Erika";
    [SerializeField] private bool applyOnStart = true;

    private void Awake()
    {
        if (targetLabel == null)
            targetLabel = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (applyOnStart)
            ApplyFromSavedLoadout();
    }

    [ContextMenu("Apply From Saved Loadout")]
    public void ApplyFromSavedLoadout()
    {
        BattleMenuLoadout loadout;
        if (!BattleLoadoutPersistence.TryLoad(out loadout))
            loadout.CharacterIndex = 0;

        Apply(loadout.CharacterIndex);
    }

    public void Apply(int characterIndex)
    {
        if (targetLabel == null)
            return;

        targetLabel.text = characterIndex == 1 ? erikaName : paladinName;
    }
}

