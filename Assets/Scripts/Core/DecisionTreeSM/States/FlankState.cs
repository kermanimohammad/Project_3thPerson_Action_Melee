using UnityEngine;

public class FlankState : AbstractState<EnemyAIBase>
{
    public FlankState(EnemyAIBase owner, StateMachine<EnemyAIBase> stateMachine) : base(StateID.Flank, owner, stateMachine)
    {
    }

    public override void Enter()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} ENTER -> Flank");
    }

    public override void Exit()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} EXIT -> Flank");
    }

    public override void Tick()
    {
        //if (owner.VerboseLogs)
        //    Debug.Log($"{owner.name} TICK -> Flank");

        //if (owner.Player == null)
        //{
        //    if (owner.VerboseLogs)
        //        Debug.Log($"{owner.name} Flank aborted: Player is null");
        //    return;
        //}

        //if (owner.Player == null)
        //    return;

        //owner.FaceTarget(owner.Player.position);

        //Vector3 flankPoint = owner.GetFlankDestination();

        //if (owner.VerboseLogs)
        //    Debug.Log($"{owner.name} Flank -> moving to flank point {flankPoint}");


        //owner.MoveTo(flankPoint, 1.05f);

        //if (owner.IsNear(flankPoint, 0.9f) && owner.InAttackRange(1f))
        //{
        //    if (owner.VerboseLogs)
        //        Debug.Log($"{owner.name} Flank -> Attack");

        //    stateMachine.SetState(StateID.Attack);
        //}
    }
}