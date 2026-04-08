using UnityEngine;

public class GlobalReferences : Singleton<GlobalReferences>
{
	private Transform player;
	[SerializeField] private string playerTag = "Player";
	[SerializeField] private GameObject magicStoneObjective;
	[SerializeField] private DoorBreakable[] palaceDoors;
	[SerializeField] private NodeGraph graph;

	public Transform GetPlayer()
	{
		if (player == null)
		{
			GameObject go = GameObject.FindGameObjectWithTag(playerTag);
			player = go != null ? go.transform : null;
		}

		return player;
	}

	public GameObject GetMagicStone() => magicStoneObjective;
	public Transform GetMagicStoneTransform() => magicStoneObjective != null ? magicStoneObjective.transform : null;

	public DoorBreakable[] GetDoors() => palaceDoors;
	public NodeGraph GetGraph() => graph;

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

	private float HorizontalDistance(Vector3 a, Vector3 b)
	{
		a.y = 0f;
		b.y = 0f;
		return Vector3.Distance(a, b);
	}

}
