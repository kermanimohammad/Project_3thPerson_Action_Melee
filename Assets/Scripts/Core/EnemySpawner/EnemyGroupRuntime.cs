using System;
using System.Collections.Generic;

public class EnemyGroupRuntime
{
    public event Action<EnemyGroupRuntime> OnGroupCleared;

    public string GroupName { get; }
    public EnemyGroupAI GroupAIInstance { get; }

    private readonly HashSet<EnemySpawnedMember> aliveMembers = new();

    public EnemyGroupRuntime(string groupName, EnemyGroupAI groupAIInstance)
    {
        GroupName = groupName;
        GroupAIInstance = groupAIInstance;
    }

    public void RegisterMember(EnemySpawnedMember member)
    {
        if (member == null)
            return;

        if (aliveMembers.Add(member))
            member.OnMemberDied += HandleMemberDied;
    }

    private void HandleMemberDied(EnemySpawnedMember member)
    {
        if (member == null)
            return;

        member.OnMemberDied -= HandleMemberDied;
        aliveMembers.Remove(member);

        if (aliveMembers.Count == 0)
            OnGroupCleared?.Invoke(this);
    }

    public int AliveCount => aliveMembers.Count;
}