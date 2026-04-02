using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Prevents "two buttons highlighted" in MainMenu:
/// when mouse/pen pointer enters a button, we also set it as EventSystem selected,
/// so keyboard/gamepad selection can't stay on a different button.
/// </summary>
public class MainMenuButtonPointerSelectionSync : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        var es = EventSystem.current;
        if (es == null) return;
        es.SetSelectedGameObject(gameObject);
    }
}

