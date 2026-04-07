using UnityEngine;

public class MeleeEnemyAI : EnemyAIBase
{
	[Header("Personality")]
	[SerializeField, Range(0f, 1f)] private float aggression = 0.7f;
	[SerializeField, Range(0f, 1f)] private float bravery = 0.6f;
	[SerializeField, Range(0f, 1f)] private float caution = 0.4f;
	[SerializeField, Range(0f, 1f)] private float randomness = 0.15f;

	protected override StateID GetUpdatedDesiredState()
	{
		float flee = GetFleeScore();
		float attack = GetAttackScore() * (1f - flee * 0.7f);
		float defend = GetDefendScore() * (1f - flee * 0.25f);
		float seek = GetSeekScore() * (1f - flee * 0.4f);

		float best = -1f;
		StateID newState = StateID.Seek;

		if (attack > best) { best = attack; newState = StateID.Attack; }
		if (defend > best) { best = defend; newState = StateID.Defend; }
		if (flee > best) { best = flee; newState = StateID.Flee; }
		//flank
		if (seek > best) { newState = StateID.Seek; }

		return newState;
	}

	protected override Transform GetUpdatedTarget()
	{
		return GlobalReferences.Instance.GetPlayer();
	}

	protected override float GetAttackScore()
	{
		if (CurrentTarget == null)
			return 0f;

		if (!attackManager.CanAttack() || !perception.InAttackRange(CurrentTarget))
			return 0f;

		float health01 = health.Normalized01;
		float inRange = 1f;
		float healthyConfidence = Mathf.Lerp(0.25f, 1f, health01);
		float lowHealthPenalty = 1f - health01;

		float score =
			0.15f +
			aggression * 0.45f +
			bravery * 0.25f +
			healthyConfidence * 0.35f -
			caution * 0.15f -
			lowHealthPenalty * 0.25f;

		return Mathf.Clamp01(score) * inRange;
	}

	protected override float GetDefendScore()
	{
		if (CurrentTarget == null)
			return 0f;

		if (CurrentTarget != GlobalReferences.Instance.GetPlayer())
			return 0f;

		if (!perception.InAttackRange(CurrentTarget, 0.9f))
			return 0f;

		float health01 = health.Normalized01;
		float lowHealthPressure = 1f - health01;
		float recoveryNeed = attackManager.CanAttack() ? 0f : 1f;
		float cautionDrive = caution;
		float aggressionSuppression = 1f - aggression * 0.4f;

		float score =
			lowHealthPressure * 0.4f +
			recoveryNeed * 0.35f +
			cautionDrive * 0.3f +
			aggressionSuppression * 0.15f;

		return Mathf.Clamp01(score);
	}

	protected override float GetFleeScore()
	{
		float health01 = health.Normalized01;
		float missingHealth = 1f - health01;

		float belowThreshold01 = 0f;
		if (fleeHealthThreshold > 0f)
			belowThreshold01 = Mathf.Clamp01((fleeHealthThreshold - health01) / fleeHealthThreshold);

		float fear =
			(1f - bravery) * 0.35f +
			caution * 0.25f +
			missingHealth * 0.4f;

		float score =
			belowThreshold01 * 0.65f +
			fear * 0.5f;

		return Mathf.Clamp01(score);
	}

	protected override float GetSeekScore()
	{
		if (CurrentTarget == null)
			return 1f;

		float health01 = health.Normalized01;
		float distance = Vector3.Distance(transform.position, CurrentTarget.position);

		bool inRange = perception.InAttackRange(CurrentTarget);
		if (inRange)
			return 0f;

		float distancePressure = Mathf.Clamp01(distance / Mathf.Max(1f, flankRadius * 2f));
		float healthyEnough = Mathf.InverseLerp(fleeHealthThreshold, 1f, health01);

		float score =
			0.1f +
			distancePressure * 0.45f +
			healthyEnough * 0.3f +
			aggression * 0.15f +
			bravery * 0.1f -
			caution * 0.1f;

		return Mathf.Clamp01(score);
	}
	protected override float GetFlankScore()
	{
		return 0;
	}
}