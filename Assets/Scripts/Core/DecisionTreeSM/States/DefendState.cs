using UnityEngine;

public class DefendState : AbstractState<EnemyAIBase>
{
    public DefendState(EnemyAIBase owner, StateMachine<EnemyAIBase> stateMachine) : base(StateID.Defend, owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.GetDefense().StartDefend();
    }

    public override void Exit()
    {
        owner.GetDefense().StopDefend();
    }

    public override void Tick()
    {
        TryRotateTowardsAimTarget(owner.CurrentTarget);
    }

    private void TryRotateTowardsAimTarget(Transform aimTarget)
    {
        if (aimTarget == null)
        {
            return;
        }

        Vector3 direction = aimTarget.position - owner.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            owner.transform.rotation = targetRotation;
        }
    }

}