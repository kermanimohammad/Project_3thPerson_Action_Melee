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
		float lowHealth = 1f - health01;
		bool playerPressuring = PlayerIsNearAndAttacking();

		float healthDrive = Mathf.Lerp(0.35f, 1f, health01);
		float aggressionDrive = aggression;
		float braveryDrive = bravery;
		float cautionPenalty = caution;

		float pressurePenalty = playerPressuring ? 0.35f : 0f;
		float lowHealthPenalty = lowHealth * 0.2f;

		float score =
			0.2f +
			aggressionDrive * 0.35f +
			braveryDrive * 0.2f +
			healthDrive * 0.3f -
			cautionPenalty * 0.15f -
			pressurePenalty -
			lowHealthPenalty;

		// If the player is not actively threatening, lean more toward attacking.
		if (!playerPressuring)
			score += 0.2f;

		return Mathf.Clamp01(score);
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
		float lowHealth = 1f - health01;
		bool playerPressuring = PlayerIsNearAndAttacking();

		float pressureDrive = playerPressuring ? 1f : 0f;
		float recoveryNeed = attackManager.CanAttack() ? 0f : 1f;

		float score =
			lowHealth * 0.2f +          // low HP matters, but not too much
			recoveryNeed * 0.3f +       // defend more when recovering between attacks
			caution * 0.2f +
			pressureDrive * 0.45f -     // main reason to defend
			aggression * 0.1f -
			bravery * 0.05f;

		// If player is not attacking, defending should usually not dominate.
		if (!playerPressuring)
			score -= 0.2f;

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

	public bool PlayerIsNearAndAttacking()
	{
		Transform player = GlobalReferences.Instance.GetPlayer();

		if (player == null || !perception.InAttackRange(player, 0.9f))
		{
			return false;
		}

		return player.GetComponent<PlayerCombat>().IsAttackingAnimation;
	}
}