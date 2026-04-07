using UnityEngine;

public class DebugEnemyMover : EnemyMoverBase
{
    [SerializeField] private bool verboseLogs = true;
    [SerializeField] private bool debugReachedDestination;
    [SerializeField] private float currentSpeed;

    public override float CurrentSpeed => currentSpeed;

    public override void MoveTo(Vector3 destination, float speedMultiplier = 1f)
    {
        currentSpeed = speedMultiplier;

        if (verboseLogs)
            Debug.Log($"{name} [DebugEnemyMover] MoveTo -> {destination}, speed x{speedMultiplier}");
    }

    public override void StopMoving()
    {
        currentSpeed = 0f;

        if (verboseLogs)
            Debug.Log($"{name} [DebugEnemyMover] StopMoving");
    }

    public override void FaceTowards(Vector3 target)
    {
        if (verboseLogs)
            Debug.Log($"{name} [DebugEnemyMover] FaceTowards -> {target}");
    }

    public override bool HasReachedDestination(float threshold)
    {
        return debugReachedDestination;
    }

	public override void MoveTo(Vector3 destination, Vector3 groupSeparationVector, float speedMultiplier = 1)
	{
		throw new System.NotImplementedException();
	}
}