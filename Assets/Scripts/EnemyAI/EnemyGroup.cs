using System.Collections.Generic;
using UnityEngine;

public class EnemyGroup : MonoBehaviour
{
	private List<EnemyAIBase> members;

	public Transform GetAssignedTarget(EnemyAIBase member)
	{
		if (member is MeleeEnemyAI)
		{

		}
		else
		{
			//TODO infiltrators
		}

		return GlobalReferences.Instance.GetPlayer();
	}

	public void RegisterMember(EnemyAIBase member)
	{
		if (member == null || members.Contains(member))
			return;
		members.Add(member);
	}

	public void UnregisterMember(EnemyAIBase member)
	{
		members.Remove(member);
	}

	public Vector3 GetSeparationHint(Vector3 selfPosition, float radius, float weight)
	{
		Vector3 push = Vector3.zero;
		int count = 0;
		for (int i = 0; i < members.Count; i++)
		{
			EnemyAIBase m = members[i];
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


}
