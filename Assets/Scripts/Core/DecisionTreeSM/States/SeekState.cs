using UnityEngine;

public class SeekState : AbstractState<EnemyAI, MainStateID>
{
    public SeekState(EnemyAI owner, StateMachine<EnemyAI, MainStateID> stateMachine) : base(MainStateID.Seek, owner, stateMachine)
    {
    }

    public override void Enter()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} ENTER -> Seek");
    }

    public override void Exit()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} EXIT -> Seek");
    }

    public override void Tick()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} TICK -> Seek");

        if (owner.ShouldFlee())
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Seek -> Flee");
            stateMachine.SetState(MainStateID.Flee);
            return;
        }

        if (owner.ShouldEnterCombat())
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Seek -> Combat");
            stateMachine.SetState(MainStateID.Combat);
            return;
        }

        EnemyGroupAI group = owner.GroupAI;

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} Seek -> continuing seek behavior");

        if (group != null && !owner.IsWallBroken())
        {
            if (group.BreachPoint != null && !owner.IsNear(group.BreachPoint.position, 1.5f))
            {
                if (owner.VerboseLogs)
                    Debug.Log($"{owner.name} Seek -> moving to breach point {group.BreachPoint.position}");

                owner.MoveTo(group.BreachPoint.position);
                return;
            }

            if (group.WallTarget != null)
            {
                float distToWall = Vector3.Distance(owner.transform.position, group.WallTarget.position);

                if (owner.VerboseLogs)
                    Debug.Log($"{owner.name} Seek -> wall detected, distance = {distToWall:F2}");

                if (distToWall > owner.WallBreakRange)
                {
                    if (owner.VerboseLogs)
                        Debug.Log($"{owner.name} Seek -> moving to wall target {group.WallTarget.position}");

                    owner.MoveTo(group.WallTarget.position);
                }
                else
                {
                    if (owner.VerboseLogs)
                        Debug.Log($"{owner.name} Seek -> trying to attack wall");

                    owner.TryAttackWall();
                }

                return;
            }
        }

        Vector3 searchTarget = owner.GetSearchDestination();

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} Seek -> moving to search target {searchTarget}");

        owner.MoveTo(searchTarget);

        if (group != null && group.GroupAlerted && owner.IsNear(searchTarget, 1.25f) && !owner.CanSeePlayer())
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Seek -> clearing group alert");

            group.ClearAlert();
        }
    }
}