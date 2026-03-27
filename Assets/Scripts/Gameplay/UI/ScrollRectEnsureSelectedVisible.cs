using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// When UI navigation (keyboard/gamepad) changes <see cref="EventSystem.currentSelectedGameObject"/>,
/// scrolls a <see cref="ScrollRect"/> vertically so the selected item stays inside the viewport (mask).
/// Mouse-only hover does not change selection, so it won't scroll until something becomes selected.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollRectEnsureSelectedVisible : MonoBehaviour
{
    [SerializeField] private RectTransform contentOverride;
    [Tooltip("Extra margin inside the viewport (pixels in world space along Y).")]
    [SerializeField] private float verticalPadding = 8f;

    [Tooltip("Optional: only react when this panel is active (e.g. SelectHero).")]
    [SerializeField] private GameObject onlyWhenActiveRoot;

    private ScrollRect _scrollRect;
    private RectTransform _content;
    private GameObject _lastSelected;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        _content = contentOverride != null ? contentOverride : _scrollRect.content;
    }

    private RectTransform ViewportRect =>
        _scrollRect.viewport != null
            ? _scrollRect.viewport
            : (RectTransform)_scrollRect.transform;

    private void LateUpdate()
    {
        if (_scrollRect == null || _content == null) return;
        if (onlyWhenActiveRoot != null && !onlyWhenActiveRoot.activeInHierarchy)
        {
            _lastSelected = null;
            return;
        }

        var es = EventSystem.current;
        if (es == null) return;

        GameObject selected = es.currentSelectedGameObject;
        if (selected == null || selected == _lastSelected) return;
        _lastSelected = selected;

        var targetRt = selected.GetComponent<RectTransform>();
        if (targetRt == null) return;
        if (!IsDescendantOf(targetRt, _content)) return;

        EnsureVisibleVertical(targetRt);
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        Transform t = child;
        while (t != null)
        {
            if (t == ancestor) return true;
            t = t.parent;
        }

        return false;
    }

    private void EnsureVisibleVertical(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();
        _scrollRect.StopMovement();

        RectTransform viewport = ViewportRect;

        Vector3[] targetCorners = new Vector3[4];
        Vector3[] viewCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);
        viewport.GetWorldCorners(viewCorners);

        float viewMinY = viewCorners[0].y + verticalPadding;
        float viewMaxY = viewCorners[1].y - verticalPadding;
        float targetMinY = targetCorners[0].y;
        float targetMaxY = targetCorners[1].y;

        float dyWorld = 0f;
        if (targetMinY < viewMinY) dyWorld = viewMinY - targetMinY;
        else if (targetMaxY > viewMaxY) dyWorld = viewMaxY - targetMaxY;

        if (Mathf.Abs(dyWorld) < 0.5f) return;

        // Move content with the viewport so the target shifts by dyWorld along world Y.
        _content.position += new Vector3(0f, dyWorld, 0f);

        // Clamp so we don't scroll past content bounds (best-effort).
        ClampContentVertical();
    }

    private void ClampContentVertical()
    {
        RectTransform viewport = ViewportRect;
        Vector3[] cCorners = new Vector3[4];
        Vector3[] vCorners = new Vector3[4];
        _content.GetWorldCorners(cCorners);
        viewport.GetWorldCorners(vCorners);

        float contentMinY = cCorners[0].y;
        float contentMaxY = cCorners[1].y;
        float viewMinY = vCorners[0].y;
        float viewMaxY = vCorners[1].y;

        float fix = 0f;
        if (contentMinY > viewMinY) fix = viewMinY - contentMinY;
        else if (contentMaxY < viewMaxY) fix = viewMaxY - contentMaxY;

        if (Mathf.Abs(fix) > 0.5f)
            _content.position += new Vector3(0f, fix, 0f);
    }
}
