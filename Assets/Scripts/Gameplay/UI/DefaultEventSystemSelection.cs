using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Ensures EventSystem selection is set to a specific UI object when this component becomes active.
/// Useful when you have panel navigation (SetActive) and want a default highlighted button.
/// </summary>
public class DefaultEventSystemSelection : MonoBehaviour
{
    [SerializeField] private GameObject selectedGameObject;

    private void OnEnable()
    {
        TrySelect();
    }

    private void Start()
    {
        TrySelect();
    }

    private void TrySelect()
    {
        if (selectedGameObject == null) return;
        if (!selectedGameObject.activeInHierarchy) return;

        var es = EventSystem.current;
        if (es == null) return;

        UIButtonHoverSfx.PrepareProgrammaticSelect(selectedGameObject);
        es.SetSelectedGameObject(selectedGameObject);
    }
}

