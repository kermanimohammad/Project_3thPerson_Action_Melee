using System.Collections;
using UnityEngine;

/// <summary>
/// Ensures the cursor is visible and unlocked when MainMenu scene is active.
/// Optionally recenters the cursor by briefly locking it for one frame.
/// </summary>
public sealed class MainMenuCursorController : MonoBehaviour
{
    [Header("Behaviour")]
    [SerializeField] private bool showCursor = true;
    [SerializeField] private bool unlockCursor = true;
    [Tooltip("If true, briefly locks the cursor for one frame then unlocks it to re-center on screen.")]
    [SerializeField] private bool recenterCursorOnEnable = true;

    private Coroutine _routine;

    private void OnEnable()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ApplyCursorStateNextFrame());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator ApplyCursorStateNextFrame()
    {
        // Let UI/EventSystem initialize first.
        yield return null;

        if (recenterCursorOnEnable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            yield return null;
        }

        if (unlockCursor)
            Cursor.lockState = CursorLockMode.None;

        if (showCursor)
            Cursor.visible = true;

        _routine = null;
    }
}

