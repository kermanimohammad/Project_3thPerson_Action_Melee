using UnityEngine;

public class EnemyAIPerception : MonoBehaviour
{
	[SerializeField] protected float eyeHeight = 1.6f;
	[SerializeField] protected float detectionRange = 18f;
	[SerializeField] protected float attackRange = 2.2f;
	[SerializeField] protected float wallBreakRange = 2.4f;
	[SerializeField] protected float losePlayerAfter = 4f;
	[SerializeField] protected LayerMask lineOfSightMask = ~0;

	private float lastSeenPlayerTime = -999f;

	public bool InAttackRange(Transform target, float extraRange = 0f)
	{
		if (target == null)
			return false;

		float range = attackRange + extraRange;
		return (transform.position - target.position).sqrMagnitude <= range * range;
	}

	public bool CanSeePlayer(Transform player)
	{
		if (player == null)
			return false;

		Vector3 eye = transform.position + Vector3.up * eyeHeight;
		Vector3 targetEye = player.position + Vector3.up * eyeHeight;
		float dist = Vector3.Distance(eye, targetEye);

		if (dist > detectionRange)
			return false;

		bool visible = true;

		if (Physics.Linecast(eye, targetEye, out RaycastHit hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
		{
			visible = hit.transform == player || hit.transform.IsChildOf(player);
		}

		if (visible)
		{
			lastSeenPlayerTime = Time.time;
		}

		return visible;
	}

	private void OnDrawGizmosSelected()
	{
		Vector3 eye = transform.position + Vector3.up * eyeHeight;

		// ---------- Ranges ----------
		// Detection range (yellow)
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, detectionRange);

		// Attack range (red)
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, attackRange);

		// Wall break range (magenta)
		Gizmos.color = Color.magenta;
		Gizmos.DrawWireSphere(transform.position, wallBreakRange);

		// ---------- Eye position ----------
		Gizmos.color = Color.cyan;
		Gizmos.DrawSphere(eye, 0.1f);

		// ---------- Forward direction ----------
		Gizmos.color = Color.blue;
		Gizmos.DrawLine(eye, eye + transform.forward * 2f);
	}
}
