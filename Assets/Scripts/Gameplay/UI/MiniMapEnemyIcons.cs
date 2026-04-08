using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically spawns/updates minimap icons for enemies.
/// Works with the "map moves under fixed player icon" setup:
/// - Place enemy icons under the same RectTransform that is moved (mapRect).
/// - Each icon anchoredPosition is mapped from enemy world XZ (same mapping as the map content).
/// </summary>
public class MiniMapEnemyIcons : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the map mover that shifts the minimap under the player.")]
    [SerializeField] private MiniMapMoveMapUnderPlayer mapMover;
    [Tooltip("Parent under which enemy icons are instantiated (should be the moved map rect).")]
    [SerializeField] private RectTransform iconsParent;
    [Tooltip("UI prefab for enemy icon (must have RectTransform + Image).")]
    [SerializeField] private RectTransform enemyIconPrefab;
    [SerializeField, Min(0.01f)] private float enemyIconScale = 0.1f;

    [Header("Enemy icon offsets")]
    [Tooltip("Extra offset applied to each enemy icon in minimap pixel space (after mapping).")]
    [SerializeField] private Vector2 enemyIconPixelOffset = Vector2.zero;
    [Tooltip("Extra offset applied to enemy world XZ before mapping (world units). Useful if your enemy pivot isn't centered.")]
    [SerializeField] private Vector2 enemyWorldXZOffset = Vector2.zero;

    [Header("Enemy discovery")]
    [Tooltip("If set, enemies will be found by Tag (active objects only).")]
    [SerializeField] private string enemyTag = "Enemy";
    [Tooltip("Optional: also discover enemies by LayerMask (active objects only). Off by default to avoid false positives when many objects share a layer.")]
    [SerializeField] private bool alsoDiscoverByLayerMask = false;
    [SerializeField] private LayerMask enemyLayers = 0;
    [SerializeField, Min(0.05f)] private float refreshIntervalSeconds = 0.5f;
    [SerializeField] private bool logDiscoveryDebug = false;

    [Header("Optional: clamp icons inside viewport")]
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private bool clampInsideViewport = true;

    private readonly Dictionary<Transform, RectTransform> icons = new();
    private float nextRefreshTime;

    private void Awake()
    {
        if (mapMover == null)
            mapMover = GetComponentInParent<MiniMapMoveMapUnderPlayer>();

        if (iconsParent == null && mapMover != null)
            iconsParent = mapMover.MapRect;
    }

    private void OnEnable()
    {
        nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        RefreshNow();
        UpdateAllPositions();
    }

    private void OnDisable()
    {
        ClearAllIcons();
    }

    private void LateUpdate()
    {
        if (mapMover == null || iconsParent == null || enemyIconPrefab == null)
            return;

        // Remove icons immediately when the enemy is destroyed/disabled (do not wait for refresh interval).
        PruneStaleEnemyIcons();

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshNow();
        }

        UpdateAllPositions();
    }

    /// <summary>
    /// Drops minimap icons for enemies that no longer exist or are inactive (e.g. died and were destroyed).
    /// Uses a rebuild instead of only <see cref="RemoveEnemy"/> so Unity "destroyed" keys are always cleared.
    /// </summary>
    private void PruneStaleEnemyIcons()
    {
        if (icons.Count == 0)
            return;

        bool anyStale = false;
        foreach (var kv in icons)
        {
            Transform t = kv.Key;
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                anyStale = true;
                break;
            }
        }

        if (!anyStale)
            return;

        var survivors = new Dictionary<Transform, RectTransform>(icons.Count);
        foreach (var kv in icons)
        {
            Transform t = kv.Key;
            if (t != null && t.gameObject.activeInHierarchy)
                survivors[kv.Key] = kv.Value;
            else if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }

        icons.Clear();
        foreach (var kv in survivors)
            icons.Add(kv.Key, kv.Value);
    }

    private void RefreshNow()
    {
        // Collect current enemies
        List<Transform> alive = ListPool<Transform>.Get();
        try
        {
            int foundByTag = 0;
            int foundByLayer = 0;

            if (!string.IsNullOrEmpty(enemyTag))
            {
                GameObject[] objs = GameObject.FindGameObjectsWithTag(enemyTag);
                for (int i = 0; i < objs.Length; i++)
                {
                    var go = objs[i];
                    if (go == null || !go.activeInHierarchy)
                        continue;
                    alive.Add(go.transform);
                    foundByTag++;
                }
            }

            if (alsoDiscoverByLayerMask && enemyLayers.value != 0)
            {
                // Active objects only. Note: this can be heavier; we run it on refreshIntervalSeconds.
                Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    if (t == null)
                        continue;
                    GameObject go = t.gameObject;
                    if (!go.activeInHierarchy)
                        continue;

                    int layerBit = 1 << go.layer;
                    if ((enemyLayers.value & layerBit) == 0)
                        continue;

                    // Filter out obvious non-enemy objects: require some enemy-like component on the root.
                    // This prevents "everything on layer X" from being treated as an enemy.
                    // (Most enemies have Health + AttackManager / EnemyMover.)
                    var h = go.GetComponentInParent<Health>();
                    if (h == null)
                        continue;
                    if (go.GetComponentInParent<AttackManager>() == null && go.GetComponentInParent<EnemyMover>() == null)
                        continue;

                    // Avoid duplicates (if an enemy is both tagged and layered)
                    if (!alive.Contains(t))
                    {
                        alive.Add(t);
                        foundByLayer++;
                    }
                }
            }

            if (logDiscoveryDebug)
                Debug.Log($"[MiniMapEnemyIcons] Found enemies: tag={foundByTag}, layer={foundByLayer}, totalUnique={alive.Count}", this);

            // Remove missing enemies
            List<Transform> toRemove = ListPool<Transform>.Get();
            try
            {
                foreach (var kv in icons)
                {
                    Transform t = kv.Key;
                    if (t == null || !t.gameObject.activeInHierarchy)
                    {
                        toRemove.Add(t);
                        continue;
                    }
                    if (!alive.Contains(t))
                        toRemove.Add(t);
                }
                for (int i = 0; i < toRemove.Count; i++)
                    RemoveEnemy(toRemove[i]);
            }
            finally
            {
                ListPool<Transform>.Release(toRemove);
            }

            // Add new enemies
            for (int i = 0; i < alive.Count; i++)
            {
                Transform t = alive[i];
                if (t == null)
                    continue;
                if (!icons.ContainsKey(t))
                    AddEnemy(t);
            }
        }
        finally
        {
            ListPool<Transform>.Release(alive);
        }
    }

    private void AddEnemy(Transform enemy)
    {
        if (enemy == null || iconsParent == null || enemyIconPrefab == null)
            return;

        RectTransform icon = Instantiate(enemyIconPrefab, iconsParent);
        // Normalize transform so it is visible & positioned predictably.
        icon.localScale = Vector3.one * enemyIconScale;
        icon.localRotation = Quaternion.identity;
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = Vector2.zero;
        icon.SetAsLastSibling();
        icon.gameObject.SetActive(true);
        icons[enemy] = icon;
    }

    private void RemoveEnemy(Transform enemy)
    {
        // Do not guard on `enemy == null`: Unity "destroyed" Transforms compare equal to null but remain valid dictionary keys.
        if (!icons.TryGetValue(enemy, out RectTransform icon))
            return;

        icons.Remove(enemy);
        if (icon != null)
            Destroy(icon.gameObject);
    }

    private void ClearAllIcons()
    {
        foreach (var kv in icons)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        icons.Clear();
    }

    private void UpdateAllPositions()
    {
        float scale = mapMover.PixelsPerWorldUnit;
        Vector2 center = mapMover.WorldCenterXZ;
        Vector2 mapPixelOffset = mapMover.PixelOffset;
        Transform playerT = mapMover.PlayerTransform;

        foreach (var kv in icons)
        {
            Transform enemy = kv.Key;
            RectTransform icon = kv.Value;
            if (enemy == null || icon == null)
                continue;

            Vector3 p = enemy.position;
            Vector2 worldXZ = new Vector2(p.x, p.z) + enemyWorldXZOffset;

            Vector2 pos;

            // If icons are parented under the moved mapRect, compute position in map-local space.
            // ScreenPos = mapRectPos + iconLocalPos => (Enemy - Player) * scale (+ offsets)
            if (iconsParent == mapMover.MapRect)
            {
                pos = (worldXZ - center) * scale + enemyIconPixelOffset;
            }
            else
            {
                // If icons are NOT under the moved mapRect (e.g., under viewport),
                // we must compute position relative to player so they move opposite to player motion.
                if (playerT == null)
                {
                    // Fallback: keep map-space mapping if player isn't resolved yet.
                    pos = (worldXZ - center) * scale + mapPixelOffset + enemyIconPixelOffset;
                }
                else
                {
                    Vector3 pp = playerT.position;
                    Vector2 playerXZ = new Vector2(pp.x, pp.z);
                    pos = (worldXZ - playerXZ) * scale + mapPixelOffset + enemyIconPixelOffset;
                }
            }

            if (clampInsideViewport && viewportRect != null)
                pos = ClampTo(pos, viewportRect, icon);

            icon.anchoredPosition = pos;
        }
    }

    private static Vector2 ClampTo(Vector2 anchored, RectTransform bounds, RectTransform icon)
    {
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

    // Simple pooled lists to avoid GC spikes.
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new();
        public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(32);
        public static void Release(List<T> list)
        {
            list.Clear();
            pool.Push(list);
        }
    }
}

