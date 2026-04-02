using UnityEngine;

public class DefendState : AbstractState<EnemyAI, CombatStateID>
{
    private float defendUntil;

    public DefendState(EnemyAI owner, StateMachine<EnemyAI, CombatStateID> stateMachine) : base(CombatStateID.Defend, owner, stateMachine)
    {
    }

    public override void Enter()
    {
        defendUntil = Time.time + owner.DefendDuration;
        owner.SetDefending(true);

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} ENTER -> Defend (until {defendUntil:F2})");

    }

    public override void Exit()
    {
        owner.SetDefending(false);

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} EXIT -> Defend");
    }

    public override void Tick()
    {
        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} TICK -> Defend");

        if (owner.Player == null)
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Defend aborted: Player is null");
            return;
        }

        if (owner.Player == null)
            return;

        owner.FaceTarget(owner.Player.position);

        Vector3 away = (owner.transform.position - owner.Player.position).normalized;
        Vector3 fallback = owner.transform.position + away * 1.5f;

        if (owner.VerboseLogs)
            Debug.Log($"{owner.name} Defend -> moving to fallback position {fallback}");

        owner.MoveTo(fallback, 0.85f);

        if (Time.time >= defendUntil)
        {
            if (owner.VerboseLogs)
                Debug.Log($"{owner.name} Defend -> Attack");

            stateMachine.SetState(CombatStateID.Attack);
        }
    }
}