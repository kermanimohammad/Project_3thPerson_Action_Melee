using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sets the BattleArea HUD avatar picture based on the selected character saved in MainMenu.
/// </summary>
public class BattleAreaAvatarPicture : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite paladinSprite;
    [SerializeField] private Sprite erikaSprite;
    [SerializeField] private bool applyOnStart = true;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
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
        if (targetImage == null)
            return;

        Sprite s = characterIndex == 1 ? erikaSprite : paladinSprite;
        if (s != null)
            targetImage.sprite = s;
    }
}

