using UnityEngine;

/// <summary>
/// Right panel: some buttons choose helmets, some choose shields.
/// Helmets = direct children under a root parented to Head (per character).
/// Shields = direct children under a root parented to RightHand (per character).
/// Wire helmet buttons to <see cref="SelectHelmet"/> and shield buttons to <see cref="SelectShield"/>
/// with Int arguments 0, 1, 2… matching child order under each root.
/// </summary>
public class MenuHelmetShieldPicker : MonoBehaviour
{
    [Header("Character selection")]
    [SerializeField] private HeroShowcaseCameraController characterShowcase;

    [Header("Helmet roots (direct children = one helmet mesh/prefab each, same order as helmet UI buttons)")]
    [SerializeField] private Transform helmetsRootCharacter0;
    [SerializeField] private Transform helmetsRootCharacter1;

    [Header("Shield roots (direct children = one shield each, same order as shield UI buttons)")]
    [SerializeField] private Transform shieldsRootCharacter0;
    [SerializeField] private Transform shieldsRootCharacter1;

    [Header("Defaults (applied to both characters on Start if enabled)")]
    [SerializeField] private int defaultHelmetIndex;
    [SerializeField] private int defaultShieldIndex;
    [SerializeField] private bool applyDefaultsOnStart = true;

    [Header("Helmet UI Visuals (same order as helmet slot indices)")]
    [SerializeField] private MenuEquipmentButtonVisual[] helmetButtons;
    [SerializeField] private Color helmetNormalFrameColor = Color.white;
    [SerializeField] private Color helmetHoverFrameColor = new Color(1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color helmetSelectedFrameColor = new Color(0.2f, 1f, 0.3f, 1f);

    [Header("Shield UI Visuals (same order as shield slot indices)")]
    [SerializeField] private MenuEquipmentButtonVisual[] shieldButtons;
    [SerializeField] private Color shieldNormalFrameColor = Color.white;
    [SerializeField] private Color shieldHoverFrameColor = new Color(1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color shieldSelectedFrameColor = new Color(0.2f, 1f, 0.3f, 1f);

    private readonly int[] _helmetIndexByCharacter = new int[2];
    private readonly int[] _shieldIndexByCharacter = new int[2];
    private int _hoveredHelmetIndex = -1;
    private int _hoveredShieldIndex = -1;
    private int _lastVisualCharacterIndex = -1;

    private void Awake()
    {
        _helmetIndexByCharacter[0] = defaultHelmetIndex;
        _helmetIndexByCharacter[1] = defaultHelmetIndex;
        _shieldIndexByCharacter[0] = defaultShieldIndex;
        _shieldIndexByCharacter[1] = defaultShieldIndex;
    }

    private void Start()
    {
        if (applyDefaultsOnStart)
        {
            for (int c = 0; c < 2; c++)
            {
                ApplyHelmet(c, _helmetIndexByCharacter[c]);
                ApplyShield(c, _shieldIndexByCharacter[c]);
            }
        }

        InitializeHelmetShieldButtons();
        RefreshHelmetButtonVisuals();
        RefreshShieldButtonVisuals();
    }

    private void Update()
    {
        if (characterShowcase == null) return;

        int currentCharacter = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        if (currentCharacter != _lastVisualCharacterIndex)
        {
            RefreshHelmetButtonVisuals();
            RefreshShieldButtonVisuals();
        }
    }

    /// <summary>Inspector: Button OnClick → MenuHelmetShieldPicker.SelectHelmet, int = helmet slot index.</summary>
    public void SelectHelmet(int helmetIndex)
    {
        if (!TryGetCharacter(out int c)) return;

        _helmetIndexByCharacter[c] = helmetIndex;
        ApplyHelmet(c, helmetIndex);
        RefreshHelmetButtonVisuals();
    }

    /// <summary>Inspector: Button OnClick → MenuHelmetShieldPicker.SelectShield, int = shield slot index.</summary>
    public void SelectShield(int shieldIndex)
    {
        if (!TryGetCharacter(out int c)) return;

        _shieldIndexByCharacter[c] = shieldIndex;
        ApplyShield(c, shieldIndex);
        RefreshShieldButtonVisuals();
    }

    public void OnHelmetButtonHoverChanged(int helmetIndex, bool isHovering)
    {
        _hoveredHelmetIndex = isHovering ? helmetIndex : -1;
        RefreshHelmetButtonVisuals();
    }

    public void OnShieldButtonHoverChanged(int shieldIndex, bool isHovering)
    {
        _hoveredShieldIndex = isHovering ? shieldIndex : -1;
        RefreshShieldButtonVisuals();
    }

    /// <summary>0 = first hero, 1 = second. Used when saving loadout for battle.</summary>
    public int GetHelmetIndexForCharacter(int characterIndex)
    {
        characterIndex = Mathf.Clamp(characterIndex, 0, 1);
        return _helmetIndexByCharacter[characterIndex];
    }

    /// <summary>0 = first hero, 1 = second. Used when saving loadout for battle.</summary>
    public int GetShieldIndexForCharacter(int characterIndex)
    {
        characterIndex = Mathf.Clamp(characterIndex, 0, 1);
        return _shieldIndexByCharacter[characterIndex];
    }

    private bool TryGetCharacter(out int characterIndex)
    {
        characterIndex = 0;
        if (characterShowcase == null)
        {
            Debug.LogWarning($"{nameof(MenuHelmetShieldPicker)}: assign {nameof(characterShowcase)}.", this);
            return false;
        }

        characterIndex = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        return true;
    }

    private void ApplyHelmet(int characterIndex, int helmetIndex)
    {
        Transform root = characterIndex == 0 ? helmetsRootCharacter0 : helmetsRootCharacter1;
        MenuEquipmentSingleChildUtility.ApplySingleActiveChild(
            root,
            helmetIndex,
            this,
            $"helmets root (character {characterIndex})");
    }

    private void ApplyShield(int characterIndex, int shieldIndex)
    {
        Transform root = characterIndex == 0 ? shieldsRootCharacter0 : shieldsRootCharacter1;
        MenuEquipmentSingleChildUtility.ApplySingleActiveChild(
            root,
            shieldIndex,
            this,
            $"shields root (character {characterIndex})");
    }

    private void InitializeHelmetShieldButtons()
    {
        if (helmetButtons != null)
        {
            for (int i = 0; i < helmetButtons.Length; i++)
            {
                if (helmetButtons[i] == null) continue;
                helmetButtons[i].InitializeHelmet(this, i);
            }
        }

        if (shieldButtons != null)
        {
            for (int i = 0; i < shieldButtons.Length; i++)
            {
                if (shieldButtons[i] == null) continue;
                shieldButtons[i].InitializeShield(this, i);
            }
        }
    }

    private void RefreshHelmetButtonVisuals()
    {
        if (helmetButtons == null || helmetButtons.Length == 0) return;

        int characterIndex = 0;
        if (characterShowcase != null)
        {
            characterIndex = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        }

        _lastVisualCharacterIndex = characterIndex;
        int selectedHelmetIndex = _helmetIndexByCharacter[characterIndex];
        bool lastInputMouse = MenuEquipmentButtonVisual.LastInputWasMouse;
        bool hasHover = _hoveredHelmetIndex >= 0;
        bool showSelected = !lastInputMouse || !hasHover;

        for (int i = 0; i < helmetButtons.Length; i++)
        {
            MenuEquipmentButtonVisual button = helmetButtons[i];
            if (button == null) continue;

            EquipmentButtonVisualState state = EquipmentButtonVisualState.Normal;
            if (showSelected && i == selectedHelmetIndex)
            {
                state = EquipmentButtonVisualState.Selected;
            }
            else if (i == _hoveredHelmetIndex && hasHover)
            {
                state = EquipmentButtonVisualState.Hover;
            }

            button.ApplyState(state, helmetNormalFrameColor, helmetHoverFrameColor, helmetSelectedFrameColor);
        }
    }

    private void RefreshShieldButtonVisuals()
    {
        if (shieldButtons == null || shieldButtons.Length == 0) return;

        int characterIndex = 0;
        if (characterShowcase != null)
        {
            characterIndex = Mathf.Clamp(characterShowcase.SelectedCharacterIndex, 0, 1);
        }

        _lastVisualCharacterIndex = characterIndex;
        int selectedShieldIndex = _shieldIndexByCharacter[characterIndex];
        bool lastInputMouse = MenuEquipmentButtonVisual.LastInputWasMouse;
        bool hasHover = _hoveredShieldIndex >= 0;
        bool showSelected = !lastInputMouse || !hasHover;

        for (int i = 0; i < shieldButtons.Length; i++)
        {
            MenuEquipmentButtonVisual button = shieldButtons[i];
            if (button == null) continue;

            EquipmentButtonVisualState state = EquipmentButtonVisualState.Normal;
            if (showSelected && i == selectedShieldIndex)
            {
                state = EquipmentButtonVisualState.Selected;
            }
            else if (i == _hoveredShieldIndex && hasHover)
            {
                state = EquipmentButtonVisualState.Hover;
            }

            button.ApplyState(state, shieldNormalFrameColor, shieldHoverFrameColor, shieldSelectedFrameColor);
        }
    }
}
