using System.Linq;
using UnityEngine;

public static class PlayerAutoAimer
{
	private const float _nearbyRadius = 2f;
	private const float _forwardConeAngle = 60f;

	public static Transform GetClosestNearbyEnemy(this Transform player, LayerMask enemyLayer)
	{
		Transform[] targetEnemies = GetNearbyEnemies(player, enemyLayer);

		Transform[] nearbyEnemiesInForwardCone = targetEnemies.Where(enemy => IsInsideForwardCone(player, enemy)).ToArray();
		if (nearbyEnemiesInForwardCone.Length > 0)
		{
			targetEnemies = nearbyEnemiesInForwardCone;
		}

		return targetEnemies.OrderBy(enemy => Vector3.SqrMagnitude(enemy.position - player.position)).FirstOrDefault();
	}

	private static bool IsInsideForwardCone(Transform player, Transform enemy)
	{
		Vector3 directionToEnemy = (enemy.position - player.position).normalized;

		float angle = Vector3.Angle(player.forward, directionToEnemy);

		return angle <= _forwardConeAngle * 0.5f;
	}

	private static Transform[] GetNearbyEnemies(Transform player, LayerMask enemyLayer)
	{
		Collider[] hits = Physics.OverlapSphere(player.position, _nearbyRadius, enemyLayer);

		Transform[] enemies = new Transform[hits.Length];

		for (int i = 0; i < hits.Length; i++)
		{
			enemies[i] = hits[i].transform;
		}

		return enemies;
	}

}
