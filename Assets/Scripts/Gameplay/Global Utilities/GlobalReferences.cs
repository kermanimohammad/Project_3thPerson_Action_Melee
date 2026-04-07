using UnityEngine;

public class GlobalReferences : Singleton<GlobalReferences>
{
	[SerializeField] private Transform player;
	[SerializeField] private Transform goal;
	[SerializeField] private Transform[] doors;

	public Transform GetPlayer()
	{
		if (player == null)
		{
			player = GameObject.FindWithTag("Player")?.transform;
		}

		return player;
	}

	public Transform GetGoal() => goal;
	public Transform[] GetDoors() => doors;
}
