using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enables/disables weapon "Trail" renderers during attack animations.
/// Put this on the Player root (same object that has <see cref="PlayerCombat"/>).
/// </summary>
public sealed class WeaponTrailParticleToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;

    [Header("Auto-discovery")]
    [Tooltip("Weapons root (Right-hand). The active weapon is assumed to be one of its children.")]
    [SerializeField] private Transform searchRoot;
    [Tooltip("Trail is assumed to be the FIRST child of the active weapon under searchRoot.")]
    [SerializeField] private int trailChildIndex = 0;
    [Tooltip("If present under the weapon, prefer this child name as the trail root (case-insensitive).")]
    [SerializeField] private string trailRootName = "trail";
    [Tooltip("Re-scan this often to handle weapon swaps/late activation (0 disables).")]
    [SerializeField, Min(0f)] private float rescanIntervalSeconds = 0.25f;

    [Header("Control mode")]
    [Tooltip("If true, trails are controlled ONLY by Animation Events calling TrailOn/TailOff methods.")]
    [SerializeField] private bool controlViaAnimationEvents = true;
    [Tooltip("If controlViaAnimationEvents is false, uses animator-based detection to auto-toggle trails.")]
    [SerializeField] private bool useSuppressLocomotionAsAttackSignal = true;

    [Header("Audio (optional)")]
    [Tooltip("If true, plays an AudioSource on the 'trail' container when TrailOn is triggered.")]
    [SerializeField] private bool playWhooshOnTrailOn = true;
    [Tooltip("If true, restarts the whoosh even if it's already playing.")]
    [SerializeField] private bool restartWhooshIfPlaying = false;

    [Header("Runtime (read-only)")]
    [SerializeField] private List<TrailRenderer> trailRenderers = new List<TrailRenderer>(8);

    private bool _lastAttackState;
    private float _nextRescanTime;
    private Transform _currentWeapon;
    private Transform _currentTrailRoot;
    private bool _eventDrivenEnabled;
    private AudioSource _trailAudioSource;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponent<PlayerCombat>();
        if (searchRoot == null)
            searchRoot = transform;

        RefreshTrailList();
        ApplyTrails(false);
        _lastAttackState = false;
        _eventDrivenEnabled = false;
    }

    private void OnEnable()
    {
        RefreshTrailList();
        ApplyTrails(false);
        _lastAttackState = false;
        _nextRescanTime = Time.unscaledTime + Mathf.Max(0.01f, rescanIntervalSeconds);
        StartCoroutine(RefreshNextFrame());
        _eventDrivenEnabled = false;
    }

    private void OnDisable()
    {
        ApplyTrails(false);
        _lastAttackState = false;
        _eventDrivenEnabled = false;
    }

    private void Update()
    {
        if (rescanIntervalSeconds > 0f && Time.unscaledTime >= _nextRescanTime)
        {
            // Keep list synced with weapon activation/swaps.
            RefreshTrailList();
            _nextRescanTime = Time.unscaledTime + rescanIntervalSeconds;
        }

        if (controlViaAnimationEvents)
        {
            // Event-driven: ensure current enabled state is applied to the latest resolved trail list (weapon swaps).
            ApplyTrails(_eventDrivenEnabled);
            return;
        }

        bool isAttacking = combat != null && (combat.IsAttackingAnimation || (useSuppressLocomotionAsAttackSignal && combat.SuppressLocomotionFromInput));
        if (isAttacking == _lastAttackState)
            return;

        ApplyTrails(isAttacking);
        _lastAttackState = isAttacking;
    }

    [ContextMenu("Refresh Trail List")]
    public void RefreshTrailList()
    {
        ResolveActiveWeaponAndTrailRoot(out var weapon, out var trailRoot);

        // No weapon/trail root (yet) → clear.
        if (weapon == null || trailRoot == null)
        {
            _currentWeapon = null;
            _currentTrailRoot = null;
            trailRenderers.Clear();
            _trailAudioSource = null;
            return;
        }

        // If unchanged, keep current lists.
        if (weapon == _currentWeapon && trailRoot == _currentTrailRoot && trailRenderers.Count > 0)
            return;

        _currentWeapon = weapon;
        _currentTrailRoot = trailRoot;

        trailRenderers.Clear();

        trailRoot.GetComponentsInChildren(includeInactive: true, result: trailRenderers);
        _trailAudioSource = trailRoot.GetComponent<AudioSource>();
    }

    private void ResolveActiveWeaponAndTrailRoot(out Transform weapon, out Transform trailRoot)
    {
        weapon = null;
        trailRoot = null;

        var root = searchRoot != null ? searchRoot : transform;
        if (root == null)
            return;

        // If user assigned the character root, try to find the common right-hand weapon root under it.
        // Supports both "X_Weapons" (some rigs) and "Weapons" (your screenshot).
        if (root.childCount > 0 && root.name.IndexOf("weapon", System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            var xWeapons = root.Find("X_Weapons");
            if (xWeapons != null)
                root = xWeapons;
            else
            {
                var weapons = root.Find("Weapons");
                if (weapons != null)
                    root = weapons;
            }
        }

        // Active weapon: first child that's active in hierarchy.
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var c = root.GetChild(i);
            if (c == null)
                continue;
            if (!c.gameObject.activeInHierarchy)
                continue;
            weapon = c;
            break;
        }

        if (weapon == null)
            return;

        if (weapon.childCount <= 0)
            return;

        // Prefer named child "trail" (container), otherwise use index.
        if (!string.IsNullOrWhiteSpace(trailRootName))
        {
            for (int i = 0; i < weapon.childCount; i++)
            {
                var c = weapon.GetChild(i);
                if (c == null)
                    continue;
                if (string.Equals(c.name, trailRootName, System.StringComparison.OrdinalIgnoreCase))
                {
                    trailRoot = c;
                    break;
                }
            }
        }

        if (trailRoot == null)
        {
            int idx = Mathf.Clamp(trailChildIndex, 0, weapon.childCount - 1);
            trailRoot = weapon.GetChild(idx);
        }
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        RefreshTrailList();
    }

    private void ApplyTrails(bool enable)
    {
        for (int i = 0; i < trailRenderers.Count; i++)
        {
            var tr = trailRenderers[i];
            if (tr == null)
                continue;

            // For TrailRenderer, toggling only enabled can be unreliable depending on materials/shaders.
            // Use emitting (if available) + Clear() on disable.
            tr.enabled = true;
            tr.emitting = enable;
            if (!enable)
                tr.Clear();
        }
    }

    /// <summary>Animation Event: turn trails ON.</summary>
    public void TrailOn()
    {
        bool wasEnabled = _eventDrivenEnabled;
        _eventDrivenEnabled = true;
        RefreshTrailList();
        ApplyTrails(true);

        if (!wasEnabled)
            TryPlayTrailWhoosh();
    }

    /// <summary>Animation Event: turn trails OFF.</summary>
    public void TrailOff()
    {
        _eventDrivenEnabled = false;
        RefreshTrailList();
        ApplyTrails(false);
    }

    private void TryPlayTrailWhoosh()
    {
        if (!playWhooshOnTrailOn)
            return;
        if (_trailAudioSource == null)
            return;

        if (restartWhooshIfPlaying && _trailAudioSource.isPlaying)
            _trailAudioSource.Stop();

        // If a clip is assigned, PlayOneShot respects the configured output group/volume on the source.
        if (_trailAudioSource.clip != null)
            _trailAudioSource.PlayOneShot(_trailAudioSource.clip);
        else
            _trailAudioSource.Play();
    }

    [ContextMenu("DEBUG: Force Enable Trails")]
    private void DebugForceEnableTrails()
    {
        RefreshTrailList();
        ApplyTrails(true);
        _lastAttackState = true;
        _eventDrivenEnabled = true;
        TryPlayTrailWhoosh();
    }

    [ContextMenu("DEBUG: Force Disable Trails")]
    private void DebugForceDisableTrails()
    {
        RefreshTrailList();
        ApplyTrails(false);
        _lastAttackState = false;
        _eventDrivenEnabled = false;
    }
}

