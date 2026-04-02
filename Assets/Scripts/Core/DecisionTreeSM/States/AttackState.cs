using UnityEngine;
public class AttackState : AbstractState<EnemyAI, CombatStateID>
{
    public AttackState(EnemyAI owner, StateMachine<EnemyAI, CombatStateID> stateMachine) : base(CombatStateID.Attack, owner, stateMachine)
    {
    }

    public override void Enter()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} ENTER -> Attack");
    }

    public override void Exit()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} EXIT -> Attack");
    }

    public override void Tick()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} TICK -> Attack");

        if (owner.Player == null)
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Attack aborted: Player is null");
            return;
        }


        if (owner.Player == null)
            return;

        owner.FaceTarget(owner.Player.position);

        if (!owner.InAttackRange())
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Attack -> moving closer to player");
            owner.MoveTo(owner.Player.position);
            return;
        }

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} Attack -> trying to attack player");

        owner.TryAttackPlayer();
    }
}