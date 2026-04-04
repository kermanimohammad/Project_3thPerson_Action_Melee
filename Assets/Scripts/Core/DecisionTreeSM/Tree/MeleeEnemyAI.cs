using UnityEngine;

public class MeleeEnemyAI : EnemyAIBase
{
	protected override StateID UpdateDesiredState()
	{
		if (ShouldFlee())
		{
			return StateID.Flee;
		}

		if (ShouldAttack())
		{
			return StateID.Attack;
		}

		if (ShouldDefend())
		{
			return StateID.Defend;
		}

		if (ShouldFlank())
		{
			return StateID.Flank;
		}

		return StateID.Seek;
	}

	protected override bool ShouldAttack()
	{
		return perception.InAttackRange(CurrentTarget) && attackManager.CanAttack();
	}

	protected override bool ShouldDefend()
	{
		bool lowHealthInMelee = health != null && health.Normalized01 <= defendHealthThreshold && perception.InAttackRange(CurrentTarget, 0.75f);

		bool recoveringInMelee = !attackManager.CanAttack() && perception.InAttackRange(CurrentTarget, 0.75f);

		return lowHealthInMelee || recoveringInMelee;

	}

	protected override bool ShouldFlank()
	{
		return false; // groupAI != null && groupAI.ShouldUnitFlank(this, attackRange * 1.8f);
	}

	protected override bool ShouldFlee()
	{
		return health != null && health.Normalized01 <= fleeHealthThreshold;
	}

	protected override bool ShouldSeek()
	{
		return true;
	}

	protected override Transform GetUpdatedTarget()
	{
		return GlobalReferences.Instance.GetPlayer();
	}
}
