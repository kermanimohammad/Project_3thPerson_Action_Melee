using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EquipmentButtonVisualState
{
    Normal = 0,
    Hover = 1,
    Selected = 2
}

/// <summary>
/// Per-slot button visuals (normal / hover / selected) for weapon, helmet, or shield grid buttons.
/// </summary>
public class MenuEquipmentButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    // We want exactly one "focus" source for UI highlights:
    // - If last input was mouse, hover should own the highlight.
    // - If last input was keyboard/gamepad navigation, selection should own the highlight.
    // PointerEnter triggers selection switch to prevent "both highlighted" (selected + hover) artifacts.
    private static bool s_lastInputWasMouse;

    public static bool LastInputWasMouse => s_lastInputWasMouse;

    [Header("Optional Visual Targets")]
    [SerializeField] private Image frameImage;
    [SerializeField] private GameObject hoverMarker;
    [SerializeField] private GameObject selectedMarker;

    private enum OwnerKind
    {
        None = 0,
        Weapon = 1,
        Helmet = 2,
        Shield = 3
    }

    private OwnerKind _ownerKind;
    private MenuWeaponPicker _weaponPicker;
    private MenuHelmetShieldPicker _helmetShieldPicker;
    private int _slotIndex = -1;

    public void Initialize(MenuWeaponPicker owner, int slotIndex)
    {
        _ownerKind = OwnerKind.Weapon;
        _weaponPicker = owner;
        _helmetShieldPicker = null;
        _slotIndex = slotIndex;
    }

    public void InitializeHelmet(MenuHelmetShieldPicker owner, int slotIndex)
    {
        _ownerKind = OwnerKind.Helmet;
        _weaponPicker = null;
        _helmetShieldPicker = owner;
        _slotIndex = slotIndex;
    }

    public void InitializeShield(MenuHelmetShieldPicker owner, int slotIndex)
    {
        _ownerKind = OwnerKind.Shield;
        _weaponPicker = null;
        _helmetShieldPicker = owner;
        _slotIndex = slotIndex;
    }

    public void ApplyState(EquipmentButtonVisualState state, Color normalColor, Color hoverColor, Color selectedColor)
    {
        if (frameImage != null)
        {
            switch (state)
            {
                case EquipmentButtonVisualState.Selected:
                    frameImage.color = selectedColor;
                    break;
                case EquipmentButtonVisualState.Hover:
                    frameImage.color = hoverColor;
                    break;
                default:
                    frameImage.color = normalColor;
                    break;
            }
        }

        if (hoverMarker != null)
        {
            hoverMarker.SetActive(state == EquipmentButtonVisualState.Hover);
        }

        if (selectedMarker != null)
        {
            selectedMarker.SetActive(state == EquipmentButtonVisualState.Selected);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_slotIndex < 0) return;

        s_lastInputWasMouse = true;
        // If selection was set by keyboard/gamepad to a different button,
        // moving the mouse here should steal selection to avoid both highlights.
        var es = EventSystem.current;
        if (es != null)
            es.SetSelectedGameObject(gameObject);

        switch (_ownerKind)
        {
            case OwnerKind.Weapon:
                if (_weaponPicker != null) _weaponPicker.OnWeaponButtonHoverChanged(_slotIndex, true);
                break;
            case OwnerKind.Helmet:
                if (_helmetShieldPicker != null) _helmetShieldPicker.OnHelmetButtonHoverChanged(_slotIndex, true);
                break;
            case OwnerKind.Shield:
                if (_helmetShieldPicker != null) _helmetShieldPicker.OnShieldButtonHoverChanged(_slotIndex, true);
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_slotIndex < 0) return;

        switch (_ownerKind)
        {
            case OwnerKind.Weapon:
                if (_weaponPicker != null) _weaponPicker.OnWeaponButtonHoverChanged(_slotIndex, false);
                break;
            case OwnerKind.Helmet:
                if (_helmetShieldPicker != null) _helmetShieldPicker.OnHelmetButtonHoverChanged(_slotIndex, false);
                break;
            case OwnerKind.Shield:
                if (_helmetShieldPicker != null) _helmetShieldPicker.OnShieldButtonHoverChanged(_slotIndex, false);
                break;
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        // OnSelect can be triggered both by mouse (pointer) and by keyboard/gamepad (navigation).
        // We keep hover highlight only for pointer-based selection; for navigation-based selection we clear hover.
        bool fromPointer = eventData is PointerEventData;
        if (fromPointer)
        {
            // Keep mouse as last input (so the picker can prioritize Hover visuals).
            return;
        }

        s_lastInputWasMouse = false;

        switch (_ownerKind)
        {
            case OwnerKind.Weapon:
                if (_weaponPicker != null) _weaponPicker.OnWeaponButtonHoverChanged(_slotIndex, false);
                break;
            case OwnerKind.Helmet:
                if (_helmetShieldPicker != null) _helmetShieldPicker.OnHelmetButtonHoverChanged(_slotIndex, false);
                break;
            case OwnerKind.Shield:
                if (_helmetShieldPicker != null) _helmetShieldPicker.OnShieldButtonHoverChanged(_slotIndex, false);
                break;
        }
    }
}
