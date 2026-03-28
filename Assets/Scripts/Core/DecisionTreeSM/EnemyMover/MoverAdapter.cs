using UnityEngine;

public class MoverAdapter : EnemyMoverBase
{
    public override void MoveTo(Vector3 destination, float speedMultiplier = 1f)
    {
    }

    public override void StopMoving()
    {
    }

    public override void FaceTowards(Vector3 target)
    {
    }

    public override float CurrentSpeed => 0f;

    public override bool HasReachedDestination(float threshold)
    {
        return false;
    }
}