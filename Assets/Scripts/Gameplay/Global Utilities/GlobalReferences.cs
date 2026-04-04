using UnityEngine;

public class GlobalReferences : Singleton<GlobalReferences>
{
	[SerializeField] private Transform player;
	[SerializeField] private Transform goal;
	[SerializeField] private Transform[] doors;

	public Transform GetPlayer() => player;
	public Transform GetGoal() => goal;
	public Transform[] GetDoors() => doors;
}
