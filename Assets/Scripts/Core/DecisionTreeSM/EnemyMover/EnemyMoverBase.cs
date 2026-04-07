using UnityEngine;

public abstract class EnemyMoverBase : MonoBehaviour
{
    public abstract void MoveTo(Vector3 destination, float speedMultiplier = 1f);
    public abstract void MoveTo(Vector3 destination, Vector3 groupSeparationVector, float speedMultiplier = 1f);
    public abstract void StopMoving();
    public abstract void FaceTowards(Vector3 target);
    public abstract float CurrentSpeed { get; }
    public abstract bool HasReachedDestination(float threshold);
}