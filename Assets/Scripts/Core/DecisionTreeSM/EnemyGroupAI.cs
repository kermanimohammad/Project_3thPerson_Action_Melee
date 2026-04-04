using System.Collections.Generic;
using UnityEngine;

public class EnemyGroupAI : MonoBehaviour
{
    [Header("Shared References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform breachPoint;
    [SerializeField] private Transform wallTarget;
    [SerializeField] private Transform regroupPoint;
    [SerializeField] private Transform searchCenter;

    [Header("Search")]
    [SerializeField] private float searchRadius = 6f;

    [Header("Group State")]
    [SerializeField] private bool waveActive = true;
    [SerializeField] private bool wallBroken = false;
    [SerializeField] private bool groupAlerted = false;

    private readonly List<EnemyAIBase> members = new List<EnemyAIBase>();

    public Transform Player => player;
    public Transform BreachPoint => breachPoint;
    public Transform WallTarget => wallTarget;
    public Transform RegroupPoint => regroupPoint;
    public Transform SearchCenter => searchCenter;

    public bool WaveActive => waveActive;
    public bool WallBroken => wallBroken;
    public bool GroupAlerted => groupAlerted;

    public Vector3 LastKnownPlayerPosition { get; private set; }

    public void Register(EnemyAIBase enemy)
    {
        if (enemy != null && !members.Contains(enemy))
            members.Add(enemy);
    }

    public void Unregister(EnemyAIBase enemy)
    {
        if (enemy != null)
            members.Remove(enemy);
    }

    public void AlertGroup(Vector3 playerPosition)
    {
        groupAlerted = true;
        LastKnownPlayerPosition = playerPosition;
    }

    public void ClearAlert()
    {
        groupAlerted = false;
    }

    public void SetWallBroken()
    {
        wallBroken = true;
    }

    public Vector3 GetSearchPoint(EnemyAIBase enemy)
    {
        Vector3 center = searchCenter != null ? searchCenter.position : transform.position;

        int seed = Mathf.Abs(enemy.GetInstanceID());
        float angle = (seed % 360) * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * searchRadius;
        return center + offset;
    }

    public Vector3 GetFlankPoint(EnemyAIBase enemy, float radius)
    {
        if (player == null)
            return enemy.transform.position;

        Vector3 toEnemy = (enemy.transform.position - player.position).normalized;
        if (toEnemy.sqrMagnitude < 0.001f)
            toEnemy = enemy.transform.right;

        Vector3 side = (Mathf.Abs(enemy.GetInstanceID()) % 2 == 0) ? Vector3.Cross(Vector3.up, toEnemy)
                                                                   : Vector3.Cross(toEnemy, Vector3.up);

        return player.position + side.normalized * radius;
    }

    public int CountMembersNearPlayer(float radius, EnemyAIBase exclude = null)
    {
        if (player == null)
            return 0;

        int count = 0;
        float sqr = radius * radius;

        foreach (EnemyAIBase member in members)
        {
            if (member == null || member == exclude)
                continue;

            if ((member.transform.position - player.position).sqrMagnitude <= sqr)
                count++;
        }

        return count;
    }

    public bool ShouldUnitFlank(EnemyAIBase enemy, float crowdedRadius)
    {
        int nearbyAllies = CountMembersNearPlayer(crowdedRadius, enemy);
        bool assignedFlanker = Mathf.Abs(enemy.GetInstanceID()) % 2 == 0;

        return nearbyAllies >= 1 && assignedFlanker;
    }
}