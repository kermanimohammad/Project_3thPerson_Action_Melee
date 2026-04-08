using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared blackboard for a 3-member squad: doors, stone, player, who is fighting.
/// Place one instance per squad in the scene and wire references in the Inspector.
/// </summary>
public class EnemySquadCoordinator : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Max horizontal distance to count as 'engaging' the player in melee context.")]
    [SerializeField] private float engagePlayerRadius = 4f;

    /// <summary>Horizontal distance at which a member counts as engaging the player.</summary>
    public float EngagePlayerRadius => engagePlayerRadius;

    [Header("Objectives")]
    [Tooltip("Palace doors (DoorBreakable). At least one must be broken to consider the palace 'open'.")]
    [SerializeField] private DoorBreakable[] palaceDoors;
    [Tooltip("Magic stone / objective inside palace (Health or DoorBreakable on same object).")]
    [SerializeField] private GameObject magicStoneObjective;

    public Transform PlayerTransform { get; private set; }
    public Vector3 LastKnownPlayerPosition { get; private set; }
    public bool HasLastKnownPlayer { get; private set; }

    private readonly List<EnemyGroupMemberAI> _members = new List<EnemyGroupMemberAI>(4);

    private void Awake()
    {
        LastKnownPlayerPosition = transform.position;
        HasLastKnownPlayer = false;
    }

    private void Update()
    {
        ResolvePlayer();
        ResolveObjectivesIfMissing();
        if (PlayerTransform != null)
        {
            LastKnownPlayerPosition = PlayerTransform.position;
            HasLastKnownPlayer = true;
        }
    }

    /// <summary>
    /// Runtime-friendly configuration for wave-spawned squads.
    /// Any null values will be auto-resolved by <see cref="ResolveObjectivesIfMissing"/>.
    /// </summary>
    public void ConfigureObjectives(DoorBreakable[] doors, GameObject magicStone)
    {
        if (doors != null && doors.Length > 0)
            palaceDoors = doors;
        if (magicStone != null)
            magicStoneObjective = magicStone;
    }

    private void ResolvePlayer()
    {
        if (PlayerTransform != null && PlayerTransform.gameObject.activeInHierarchy)
            return;

        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        PlayerTransform = go != null ? go.transform : null;
    }

    private void ResolveObjectivesIfMissing()
    {
        // Doors: if not wired in inspector, fall back to anything in scene.
        if (palaceDoors == null || palaceDoors.Length == 0)
            palaceDoors = Object.FindObjectsByType<DoorBreakable>(FindObjectsSortMode.None);

        // Magic stone: try GlobalReferences first, then name-based fallback for existing BattleArea object.
        if (magicStoneObjective == null)
        {
            if (GlobalReferences.Instance != null)
                magicStoneObjective = GlobalReferences.Instance.GetMagicStone();
        }

        if (magicStoneObjective == null)
        {
            // BattleArea currently has an object named "MagicalSton" (typo). Use contains match to be robust.
            var gos = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < gos.Length; i++)
            {
                var go = gos[i];
                if (go == null) continue;
                var n = go.name;
                if (string.IsNullOrEmpty(n)) continue;
                if (n.Contains("MagicalSton") || n.Contains("MagicStone") || n.Contains("Magic Stone"))
                {
                    magicStoneObjective = go;
                    break;
                }
            }
        }
    }

    public void RegisterMember(EnemyGroupMemberAI member)
    {
        if (member == null || _members.Contains(member))
            return;
        _members.Add(member);
    }

    public void UnregisterMember(EnemyGroupMemberAI member)
    {
        _members.Remove(member);
    }

    /// <summary>
    /// Returns a point around the player for this member to approach so attackers don't stack on one angle.
    /// Slots are assigned deterministically by member instance id order: 0°, +min°, -min°, +2min°, -2min°, ...
    /// </summary>
    public Vector3 GetEngageApproachPoint(EnemyGroupMemberAI member, float radius, float minAngleDegrees = 45f)
    {
        var player = PlayerTransform;
        if (player == null)
            return member != null ? member.transform.position : transform.position;

        if (member == null)
            return player.position;

        // Stable ordering across frames.
        var list = new List<EnemyGroupMemberAI>(_members.Count);
        for (int i = 0; i < _members.Count; i++)
        {
            var m = _members[i];
            if (m == null || !m.isActiveAndEnabled)
                continue;
            list.Add(m);
        }
        list.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

        int idx = list.IndexOf(member);
        if (idx < 0) idx = Mathf.Abs(member.GetInstanceID()) % 5;

        float ang;
        if (idx == 0) ang = 0f;
        else
        {
            int k = (idx + 1) / 2; // 1,1,2,2...
            float sign = (idx % 2 == 1) ? 1f : -1f;
            ang = sign * k * Mathf.Max(0f, minAngleDegrees);
        }

        // Base direction: from player toward squad center (keeps squad coming from same general side).
        Vector3 baseDir = transform.position - player.position;
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.0001f)
        {
            baseDir = member.transform.position - player.position;
            baseDir.y = 0f;
        }
        if (baseDir.sqrMagnitude < 0.0001f)
            baseDir = player.forward;

        baseDir.Normalize();
        Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * baseDir;
        if (dir.sqrMagnitude < 0.0001f)
            dir = baseDir;

        return player.position + dir.normalized * Mathf.Max(0.1f, radius);
    }

    /// <summary>True if any registered palace door is already broken.</summary>
    public bool IsPalaceBreached()
    {
        if (palaceDoors == null)
            return false;
        for (int i = 0; i < palaceDoors.Length; i++)
        {
            DoorBreakable d = palaceDoors[i];
            if (d != null && d.IsBroken)
                return true;
        }
        return false;
    }

    /// <summary>Pick a door that still has HP; otherwise null.</summary>
    public DoorBreakable GetBestDoorToAttack(Vector3 fromPosition)
    {
        DoorBreakable best = null;
        float bestDist = float.MaxValue;
        if (palaceDoors == null)
            return null;

        for (int i = 0; i < palaceDoors.Length; i++)
        {
            DoorBreakable d = palaceDoors[i];
            if (d == null || d.IsBroken)
                continue;
            float dist = HorizontalDistance(fromPosition, d.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }
        return best;
    }

    public Transform GetMagicStoneTransform() => magicStoneObjective != null ? magicStoneObjective.transform : null;

    /// <summary>How many squad members are within horizontal range of the player (for task delegation).</summary>
    public int CountMembersNearPlayer()
    {
        if (PlayerTransform == null)
            return 0;
        int n = 0;
        for (int i = 0; i < _members.Count; i++)
        {
            EnemyGroupMemberAI m = _members[i];
            if (m == null || !m.isActiveAndEnabled)
                continue;
            float d = HorizontalDistance(m.transform.position, PlayerTransform.position);
            if (d <= engagePlayerRadius)
                n++;
        }
        return n;
    }

    /// <summary>Like <see cref="CountMembersNearPlayer"/> but excludes one member (e.g. self).</summary>
    public int CountMembersNearPlayerExcluding(EnemyGroupMemberAI exclude)
    {
        if (PlayerTransform == null)
            return 0;
        int n = 0;
        for (int i = 0; i < _members.Count; i++)
        {
            EnemyGroupMemberAI m = _members[i];
            if (m == null || !m.isActiveAndEnabled || m == exclude)
                continue;
            float d = HorizontalDistance(m.transform.position, PlayerTransform.position);
            if (d <= engagePlayerRadius)
                n++;
        }
        return n;
    }

    /// <summary>Separation vector from squadmates (XZ), normalized or zero.</summary>
    public Vector3 GetSeparationHint(Vector3 selfPosition, float radius, float weight)
    {
        Vector3 push = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _members.Count; i++)
        {
            EnemyGroupMemberAI m = _members[i];
            if (m == null)
                continue;
            Vector3 o = m.transform.position;
            float d = Vector3.Distance(new Vector3(selfPosition.x, 0f, selfPosition.z), new Vector3(o.x, 0f, o.z));
            if (d < 0.001f || d > radius)
                continue;
            Vector3 away = selfPosition - o;
            away.y = 0f;
            push += away.normalized * (1f - d / radius);
            count++;
        }
        if (count == 0)
            return Vector3.zero;
        push /= count;
        return push * weight;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        if (palaceDoors != null)
        {
            for (int i = 0; i < palaceDoors.Length; i++)
            {
                if (palaceDoors[i] != null)
                    Gizmos.DrawLine(transform.position, palaceDoors[i].transform.position);
            }
        }
        if (magicStoneObjective != null)
            Gizmos.DrawLine(transform.position, magicStoneObjective.transform.position);
    }
#endif
}
