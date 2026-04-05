using UnityEngine;

/// <summary>
/// Converts Player world XZ position to a UI anchored position on a minimap.
/// Attach this to any UI GameObject (e.g., the player icon).
/// </summary>
public class MiniMapPlayerIcon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float autoBindRetrySeconds = 1.0f;

    [Header("Mapping")]
    [Tooltip("Pixels per 1 world unit. Example: 10 means 1m in world = 10px on minimap.")]
    [SerializeField] private float pixelsPerWorldUnit = 10f;

    [Tooltip("World position (X,Z) that should map to minimap center (0,0).")]
    [SerializeField] private Vector2 worldCenterXZ = Vector2.zero;

    [Tooltip("Additional pixel offset applied after mapping (useful to fine-tune centering).")]
    [SerializeField] private Vector2 pixelOffset = Vector2.zero;

    [Header("Clamping (optional)")]
    [Tooltip("If assigned, keeps the icon inside this rect (usually the minimap frame).")]
    [SerializeField] private RectTransform clampToRect;
    [SerializeField] private bool clampInside = true;

    private Coroutine bindRoutine;

    private void Reset()
    {
        playerIcon = transform as RectTransform;
    }

    private void Awake()
    {
        if (playerIcon == null)
            playerIcon = transform as RectTransform;
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
        if (player == null && bindRoutine == null && autoBindRetrySeconds > 0f)
            BindToActivePlayer();

        if (player == null || playerIcon == null)
            return;

        Vector3 p = player.position;
        Vector2 worldXZ = new Vector2(p.x, p.z);

        // Move center: (worldCenterXZ) -> (0,0) in UI space, then scale to pixels.
        Vector2 delta = worldXZ - worldCenterXZ;
        Vector2 uiPos = delta * pixelsPerWorldUnit + pixelOffset;

        if (clampInside && clampToRect != null)
            uiPos = ClampTo(uiPos, clampToRect, playerIcon);

        playerIcon.anchoredPosition = uiPos;
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

        // Fallback: prefer any active PlayerMotor in scene (more robust if tag isn't set).
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

    private static Vector2 ClampTo(Vector2 anchored, RectTransform bounds, RectTransform icon)
    {
        // Assumes icon is a child (direct or indirect) of bounds' coordinate space.
        Rect r = bounds.rect;
        Vector2 half = icon.rect.size * 0.5f;

        float minX = r.xMin + half.x;
        float maxX = r.xMax - half.x;
        float minY = r.yMin + half.y;
        float maxY = r.yMax - half.y;

        anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
        anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
        return anchored;
    }
}

