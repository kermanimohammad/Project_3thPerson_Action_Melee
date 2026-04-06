using UnityEngine;

/// <summary>
/// Keeps the player icon fixed and moves the minimap image underneath it (inverse movement).
/// Put this on any UI object (commonly the minimap Image GameObject).
/// </summary>
public class MiniMapMoveMapUnderPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform mapRect;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float autoBindRetrySeconds = 1.0f;

    [Header("Mapping")]
    [Tooltip("Pixels per 1 world unit. Example: 10 means 1m in world = 10px on minimap.")]
    [SerializeField] private float pixelsPerWorldUnit = 10f;

    [Tooltip("World position (X,Z) that should map to minimap center when player is at that point.")]
    [SerializeField] private Vector2 worldCenterXZ = Vector2.zero;

    [Tooltip("Additional pixel offset applied after mapping (useful to fine-tune centering).")]
    [SerializeField] private Vector2 pixelOffset = Vector2.zero;

    [Header("Clamping (optional)")]
    [Tooltip("If assigned, clamps the map so the frame never shows outside-map empty space.")]
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private bool clampInsideViewport = true;

    public RectTransform MapRect => mapRect;
    public float PixelsPerWorldUnit => pixelsPerWorldUnit;
    public Vector2 WorldCenterXZ => worldCenterXZ;
    public Vector2 PixelOffset => pixelOffset;
    public Transform PlayerTransform => player;

    private Coroutine bindRoutine;

    private void Reset()
    {
        mapRect = transform as RectTransform;
    }

    private void Awake()
    {
        if (mapRect == null)
            mapRect = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (player == null)
            BindToActivePlayer();
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (mapRect == null)
            return;

        if (player == null)
            return;

        Vector3 p = player.position;
        Vector2 worldXZ = new Vector2(p.x, p.z);

        // Inverse movement: player moves +X => map moves -X, etc.
        Vector2 delta = worldXZ - worldCenterXZ;
        Vector2 mapPos = -(delta * pixelsPerWorldUnit) + pixelOffset;

        if (clampInsideViewport && viewportRect != null)
            mapPos = ClampMapToViewport(mapPos, mapRect, viewportRect);

        mapRect.anchoredPosition = mapPos;
    }

    public void BindToActivePlayer()
    {
        if (TryResolveActivePlayer(out var t))
        {
            player = t;
            return;
        }

        if (autoBindRetrySeconds > 0f && bindRoutine == null)
            bindRoutine = StartCoroutine(BindRetryRoutine());
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    private System.Collections.IEnumerator BindRetryRoutine()
    {
        float t = 0f;
        while (t < autoBindRetrySeconds)
        {
            if (TryResolveActivePlayer(out var p))
            {
                player = p;
                bindRoutine = null;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        bindRoutine = null;
    }

    private bool TryResolveActivePlayer(out Transform playerTransform)
    {
        playerTransform = null;

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null && go.activeInHierarchy)
            {
                playerTransform = go.transform;
                return true;
            }
        }

        PlayerMotor[] motors = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
        for (int i = 0; i < motors.Length; i++)
        {
            var m = motors[i];
            if (m == null || !m.isActiveAndEnabled)
                continue;
            playerTransform = m.transform;
            return true;
        }

        return false;
    }

    private static Vector2 ClampMapToViewport(Vector2 desiredMapAnchoredPos, RectTransform map, RectTransform viewport)
    {
        // Assumes map and viewport share the same parent space.
        // We clamp the map so the viewport rect always stays covered by the map rect.
        Rect mapR = map.rect;
        Rect viewR = viewport.rect;

        // Map position is its anchoredPosition. With pivot centered (0.5,0.5), the map covers:
        // [pos + mapR.xMin .. pos + mapR.xMax] in parent space; same for y.
        float minX = viewR.xMax - mapR.xMax; // map leftmost so its right edge covers viewport right
        float maxX = viewR.xMin - mapR.xMin; // map rightmost so its left edge covers viewport left
        float minY = viewR.yMax - mapR.yMax;
        float maxY = viewR.yMin - mapR.yMin;

        desiredMapAnchoredPos.x = Mathf.Clamp(desiredMapAnchoredPos.x, minX, maxX);
        desiredMapAnchoredPos.y = Mathf.Clamp(desiredMapAnchoredPos.y, minY, maxY);
        return desiredMapAnchoredPos;
    }
}

