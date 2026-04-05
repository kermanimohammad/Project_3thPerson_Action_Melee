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
		float attack = GetAttackWeight();
		float defend = GetDefendWeight();
		float flee = GetFleeWeight();
		float seek = GetSeekWeight();
		float total = attack + defend + flee + seek;

		StateID newState;

		if (total <= 0f)
		{
			newState = StateID.Seek;
		}
		else
		{
			float roll = Random.value * total;

			if (roll < attack) newState = StateID.Attack;
			else if ((roll -= attack) < defend) newState = StateID.Defend;
			else if ((roll -= defend) < flee) newState = StateID.Flee;
			else newState = StateID.Seek;
		}

		return newState;
	}

	protected override Transform GetUpdatedTarget()
	{
		return GlobalReferences.Instance.GetPlayer();
	}

	protected override float GetAttackWeight()
	{
		if (!attackManager.CanAttack() || !perception.InAttackRange(CurrentTarget))
			return 0f;

		float health01 = health.Normalized01;
		float healthConfidence = Mathf.Lerp(0.3f, 1f, health01);
		float randomFactor = Random.Range(0f, randomness);

		float weight =
			0.4f +
			aggression * 0.5f +
			bravery * 0.3f +
			healthConfidence * 0.4f +
			randomFactor;

		return Mathf.Max(0f, weight);
	}

	protected override float GetDefendWeight()
	{
		if (CurrentTarget != GlobalReferences.Instance.GetPlayer() ||
			!perception.InAttackRange(CurrentTarget, 0.9f))
			return 0f;

		float health01 = health.Normalized01;
		float lowHealthPressure = 1f - health01;
		float recoveryNeed = attackManager.CanAttack() ? 0f : 0.5f;
		float randomFactor = Random.Range(0f, randomness * 0.5f);

		float weight =
			caution * 0.5f +
			lowHealthPressure * 0.6f +
			recoveryNeed * 0.7f +
			randomFactor;

		return Mathf.Max(0f, weight);
	}

	protected override float GetFleeWeight()
	{
		float health01 = health.Normalized01;
		float lowHealthPressure = 1f - health01;

		float randomFactor = Random.Range(0f, randomness * 0.25f);

		float baseFlee = (1f - bravery) * 0.1f;

		if (health01 > fleeHealthThreshold)
			return baseFlee;

		float weight =
			0.7f +
			lowHealthPressure * 1.0f +
			(1f - bravery) * 0.6f +
			randomFactor;

		return Mathf.Max(0f, weight);
	}

	protected override float GetSeekWeight()
	{
		float health01 = health.Normalized01;
		float distance = Vector3.Distance(transform.position, CurrentTarget.position);

		bool farFromTarget = !perception.InAttackRange(CurrentTarget);
		bool healthy = health01 > fleeHealthThreshold;

		if (!farFromTarget)
			return 0;

		if (distance > flankRadius && farFromTarget && healthy)
			return 5f;

		float distanceFactor = Mathf.Clamp01(distance / 10f);

		float weight =
			0.2f +
			health01 +
			distanceFactor * 0.8f;

		return Mathf.Max(0f, weight);
	}

	protected override float GetFlankWeight()
	{
		return 0;
	}
}