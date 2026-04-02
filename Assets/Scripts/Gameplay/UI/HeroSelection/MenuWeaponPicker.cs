using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Weapon icons on the left panel map to child indices under each character's weapon root.
/// Clicking a button activates that weapon on the currently selected hero only; siblings are hidden.
/// </summary>
public class MenuWeaponPicker : MonoBehaviour
{
    [Header("Character selection (same object as HeroShowcaseCameraController is fine)")]
    [SerializeField] private HeroShowcaseCameraController characterShowcase;

    [Header("3D weapon roots (direct children = one weapon each, same order as UI buttons)")]
    [Tooltip("Usually Paladin/Character1 → mixamorig:RightHand/Weapons")]
    [SerializeField] private Transform weaponsRootCharacter0;
    [Tooltip("Usually Erika/Character2 → …/Weapons (1)")]
    [SerializeField] private Transform weaponsRootCharacter1;

    [Header("Defaults")]
    [SerializeField] private int defaultWeaponIndex;
    [Tooltip("If true, on Start both characters are forced to defaultWeaponIndex visibility.")]
    [SerializeField] private bool applyDefaultOnStart = true;

    [Header("Weapon UI Visuals (same order as weapon indices)")]
    [SerializeField] private MenuEquipmentButtonVisual[] weaponButtons;
    [SerializeField] private Color normalFrameColor = Color.white;
    [SerializeField] private Color hoverFrameColor = new Color(1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color selectedFrameColor = new Color(0.2f, 1f, 0.3f, 1f);

    /// <summary>Last chosen weapon slot per character (0 or 1).</summary>
    private readonly int[] _weaponIndexByCharacter = new int[2];
    private int _hoveredWeaponIndex = -1;
    private int _lastVisualCharacterIndex = -1;

    private void Awake()
    {
        // Auto-assign the SelectHero panel by name, so we don't accidentally steal
        // EventSystem selection while the SelectHero UI is hidden.
        if (selectHeroPanelForSelection == null)
        {
            var go = GameObject.Find("SelectHero");
            if (go != null) selectHeroPanelForSelection = go;
        }

        _weaponIndexByCharacter[0] = defaultWeaponIndex;
        _weaponIndexByCharacter[1] = defaultWeaponIndex;
    }

    private void Start()
    {
        if (applyDefaultOnStart)
        {
            ApplyWeaponSelection(0, _weaponIndexByCharacter[0]);
            ApplyWeaponSelection(1, _weaponIndexByCharacter[1]);
        }

        InitializeWeaponButtons();
        RefreshWeaponButtonVisuals();
    }

    [Header("UI Navigation (EventSystem)")]
    [SerializeField] private bool setSelectedOnEnable = true;
    [SerializeField] private GameObject selectHeroPanelForSelection;

    private bool _wasSelectHeroActive;
    private Coroutine _restoreSelectionAfterHeroChangeRoutine;

    private void OnEnable()
    {
        if (!setSelectedOnEnable) return;

        bool selectHeroActive = selectHeroPanelForSelection == null ? true : selectHeroPanelForSelection.activeInHierarchy;
        _wasSelectHeroActive = selectHeroActive;

        // If it's already active, sync immediately.
        if (selectHeroActive)
        {
            SyncEventSystemSelectionToCurrentWeapon();
        }
    }

    private void Update()
    {
        // If SelectHero was toggled with SetActive, OnEnable may not re-fire.
        if (selectHeroPanelForSelection != null)
        {
            bool activeNow = selectHeroPanelForSelection.activeInHierarchy;
            if (activeNow && !_wasSelectHeroActive)
            {
                _hoveredWeaponIndex = -1; // avoid stale hover affecting visuals
                RefreshWeaponButtonVisuals();
                if (setSelectedOnEnable) SyncEventSystemSelectionToCurrentWeapon();
            }

            _wasSelectHeroActive = activeNow;
        }

        if (characterShowcase == null) return;

        int currentCharacter = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        if (currentCharacter != _lastVisualCharacterIndex)
        {
            RefreshWeaponButtonVisuals();

            // Defer one frame so Button.interactable updates on Next/Previous match EventSystem state.
            if (setSelectedOnEnable)
            {
                if (_restoreSelectionAfterHeroChangeRoutine != null)
                    StopCoroutine(_restoreSelectionAfterHeroChangeRoutine);
                _restoreSelectionAfterHeroChangeRoutine = StartCoroutine(CoRestoreSelectionAfterHeroChange());
            }
        }
    }

    private IEnumerator CoRestoreSelectionAfterHeroChange()
    {
        yield return null;
        _restoreSelectionAfterHeroChangeRoutine = null;

        if (selectHeroPanelForSelection != null && !selectHeroPanelForSelection.activeInHierarchy)
            yield break;

        // Switching heroes disables Next/Previous arrows (interactable=false). If the user had
        // keyboard/gamepad focus on one of those buttons, selection stays on a non-interactable
        // Selectable and UI navigation stops. Re-pin selection to the current weapon when needed.
        if (ShouldRestoreSelectionAfterCharacterChange())
            SyncEventSystemSelectionToCurrentWeapon();
    }

    /// <summary>
    /// True if we should move EventSystem selection to a valid control after hero index changes.
    /// </summary>
    private bool ShouldRestoreSelectionAfterCharacterChange()
    {
        if (selectHeroPanelForSelection != null && !selectHeroPanelForSelection.activeInHierarchy)
            return false;

        EventSystem es = EventSystem.current;
        if (es == null) return true;

        GameObject cur = es.currentSelectedGameObject;
        if (cur == null) return true;

        var sel = cur.GetComponent<Selectable>();
        if (sel == null) return false;
        return !sel.interactable;
    }

    private void SyncEventSystemSelectionToCurrentWeapon()
    {
        if (weaponButtons == null || weaponButtons.Length == 0) return;
        if (selectHeroPanelForSelection != null && !selectHeroPanelForSelection.activeInHierarchy) return;

        int characterIndex = 0;
        if (characterShowcase != null)
        {
            characterIndex = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        }

        int desiredIndex = Mathf.Clamp(_weaponIndexByCharacter[characterIndex], 0, weaponButtons.Length - 1);
        var visual = weaponButtons[desiredIndex];
        if (visual == null) return;

        // Prefer selecting the Button component (so navigation works correctly).
        var btn = visual.GetComponent<Button>();
        GameObject target = (btn != null) ? btn.gameObject : visual.gameObject;
        if (target == null) return;

        EventSystem es = EventSystem.current;
        if (es != null)
        {
            UIButtonHoverSfx.PrepareProgrammaticSelect(target);
            es.SetSelectedGameObject(target);
        }

        RefreshWeaponButtonVisuals();
    }

    /// <summary>Wire each weapon UI Button to this method; set argument 0, 1, 2, … in the Inspector (Int mode).</summary>
    public void SelectWeapon(int weaponIndex)
    {
        if (characterShowcase == null)
        {
            Debug.LogWarning($"{nameof(MenuWeaponPicker)}: assign {nameof(characterShowcase)}.", this);
            return;
        }

        int c = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        _weaponIndexByCharacter[c] = weaponIndex;
        ApplyWeaponSelection(c, weaponIndex);
        RefreshWeaponButtonVisuals();
    }

    public void OnWeaponButtonHoverChanged(int weaponIndex, bool isHovering)
    {
        _hoveredWeaponIndex = isHovering ? weaponIndex : -1;
        RefreshWeaponButtonVisuals();
    }

    /// <summary>0 = first hero, 1 = second. Used when saving loadout for battle.</summary>
    public int GetWeaponIndexForCharacter(int characterIndex)
    {
        characterIndex = Mathf.Clamp(characterIndex, 0, 1);
        return _weaponIndexByCharacter[characterIndex];
    }

    private void ApplyWeaponSelection(int characterIndex, int weaponIndex)
    {
        Transform root = characterIndex == 0 ? weaponsRootCharacter0 : weaponsRootCharacter1;
        MenuEquipmentSingleChildUtility.ApplySingleActiveChild(
            root,
            weaponIndex,
            this,
            $"weapons root (character {characterIndex})");
    }

    private void InitializeWeaponButtons()
    {
        if (weaponButtons == null) return;

        for (int i = 0; i < weaponButtons.Length; i++)
        {
            if (weaponButtons[i] == null) continue;
            weaponButtons[i].Initialize(this, i);
        }
    }

    private void RefreshWeaponButtonVisuals()
    {
        if (weaponButtons == null || weaponButtons.Length == 0) return;

        int characterIndex = 0;
        if (characterShowcase != null)
        {
            characterIndex = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        }

        _lastVisualCharacterIndex = characterIndex;
        int selectedWeaponIndex = _weaponIndexByCharacter[characterIndex];
        bool lastInputMouse = MenuEquipmentButtonVisual.LastInputWasMouse;
        bool hasHover = _hoveredWeaponIndex >= 0;
        bool showSelected = !lastInputMouse || !hasHover;

        for (int i = 0; i < weaponButtons.Length; i++)
        {
            MenuEquipmentButtonVisual button = weaponButtons[i];
            if (button == null) continue;

            EquipmentButtonVisualState state = EquipmentButtonVisualState.Normal;
            if (showSelected && i == selectedWeaponIndex)
            {
                state = EquipmentButtonVisualState.Selected;
            }
            else if (i == _hoveredWeaponIndex && hasHover)
            {
                state = EquipmentButtonVisualState.Hover;
            }

            button.ApplyState(state, normalFrameColor, hoverFrameColor, selectedFrameColor);
        }
    }
}
