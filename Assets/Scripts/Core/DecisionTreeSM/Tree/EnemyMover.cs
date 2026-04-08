using System.Collections.Generic;
using UnityEngine;

// EnemyMover
public class EnemyMover : MonoBehaviour
{
	[SerializeField] private float speed = 3.2f;
	[SerializeField] private float arriveDistance = 1.35f;
	[SerializeField] private Animator animator;
	[SerializeField] private CharacterController controller;
	[SerializeField] private NodeGraph graph;
	[SerializeField] private float jumpHeight = 1.15f;
	[SerializeField] private float jumpDuration = 0.5f;
	[Tooltip("Max yaw deg/s when rotating toward move velocity. Inspector on EnemyGroupMemberAI also has align-to-velocity when using that path.")]
	[SerializeField, Min(1f)] private float alignToVelocityMaxDegreesPerSecond = 220f;

	private Vector3 _velocity;
	private Vector3 _groupSeparationVector;
	private float _externalSpeedMultiplier = 1f;
	List<Node> currentPath;
	int currentIndex = 0;
	private bool isJumping = false;
	private Vector3 jumpStart;
	private Vector3 jumpEnd;
	private float jumpTimer = 0f;

	public float CurrentSpeed => speed;

	/// <summary>
	/// Allows higher-level AI to scale movement speed (e.g., walk when low stamina).
	/// </summary>
	public void SetExternalSpeedMultiplier(float multiplier)
	{
		_externalSpeedMultiplier = Mathf.Clamp(multiplier, 0f, 10f);
	}

	private void Awake()
	{
		graph = FindFirstObjectByType<NodeGraph>();
	}

	public void MoveTo(Vector3 destination, float speedMultiplier = 1) => MoveTo(destination, Vector3.zero, speedMultiplier);
	private void MoveTo(Vector3 destination, Vector3 groupSeparationVector, float speedMultiplier = 1)
	{
		Vector3 flat = destination - transform.position;
		flat.y = 0f;
		if (flat.sqrMagnitude < arriveDistance * arriveDistance)
		{
			currentIndex++;
			return;
		}

		groupSeparationVector.y = 0f;
		Vector3 goalDir = flat.normalized;
		Vector3 combined = goalDir + groupSeparationVector;
		Vector3 dir;
		if (combined.sqrMagnitude < 1e-6f)
			dir = goalDir;
		else
			dir = combined.normalized;

		float effectiveSpeed = speed * Mathf.Clamp(speedMultiplier, 0f, 10f) * _externalSpeedMultiplier;
		_velocity = dir * effectiveSpeed;

		ApplyMove(dir * effectiveSpeed, effectiveSpeed);
		animator.SetFloat(AnimParams.Speed, _velocity.magnitude);
		animator.SetBool(AnimParams.IsGrounded, controller.isGrounded);
	}

	public void Move()
	{
		if (currentPath == null || currentIndex >= currentPath.Count)
		{
			Debug.LogWarning($"Null Path Detected for {gameObject.name}");
			return;
		}

		var currentNode = currentPath[currentIndex];

		if (currentIndex > 0)
		{
			var prevNode = currentPath[currentIndex - 1];

			if (currentNode.NodeType == NodeTypeEnum.Jumping &&
				prevNode.NodeType == NodeTypeEnum.Jumping &&
				currentNode.loc.y > prevNode.loc.y)
			{
				JumpTowards(currentNode.loc);
				return;
			}
		}

		// Pass 1 here; external scaling is applied inside MoveTo.
		MoveTo(currentNode.loc, _groupSeparationVector, 1f);
	}

	/// <summary>
	/// Provides a small planar steering bias to reduce stacking when multiple enemies share a destination/path.
	/// Expected in world space, XZ only (Y ignored).
	/// </summary>
	public void SetGroupSeparation(Vector3 worldSeparation)
	{
		worldSeparation.y = 0f;
		_groupSeparationVector = worldSeparation;
	}

	private void JumpTowards(Vector3 destination)
	{
		if (!isJumping)
		{
			animator.SetTrigger(AnimParams.Jump);
			isJumping = true;
			jumpStart = transform.position;
			jumpEnd = destination;
			jumpTimer = 0f;
		}

		jumpTimer += Time.deltaTime;
		float t = Mathf.Clamp01(jumpTimer / jumpDuration);

		Vector3 horizontal = Vector3.Lerp(jumpStart, jumpEnd, t);
		float verticalOffset = 4f * jumpHeight * t * (1f - t);
		Vector3 desiredPosition = horizontal + Vector3.up * verticalOffset;

		Vector3 frameDelta = desiredPosition - transform.position;

		if (controller != null)
		{
			controller.Move(frameDelta);
		}

		Vector3 look = jumpEnd - transform.position;
		look.y = 0f;
		if (look.sqrMagnitude > 0.001f)
		{
			Quaternion targetRot = Quaternion.LookRotation(look.normalized);
			transform.rotation = Quaternion.RotateTowards(
				transform.rotation,
				targetRot,
				alignToVelocityMaxDegreesPerSecond * Time.deltaTime);
		}

		animator.SetFloat(AnimParams.Speed, speed);
		animator.SetBool(AnimParams.IsGrounded, controller != null ? controller.isGrounded : false);

		if (t >= 1f)
		{
			isJumping = false;

			Vector3 flatCurrent = transform.position;
			Vector3 flatEnd = jumpEnd;
			flatCurrent.y = 0f;
			flatEnd.y = 0f;

			float remainingDistance = Vector3.Distance(flatCurrent, flatEnd);

			if (remainingDistance <= arriveDistance)
			{
				currentIndex++;
			}
			else
			{
				RecalculatePathFinding(jumpEnd);
			}
		}
	}

	public void RecalculatePathFinding(Vector3 destination)
	{
		Node start = Pathfinding.FindNearestNode(transform.position, graph.Nodes);
		Node dest = Pathfinding.FindNearestNode(destination, graph.Nodes);
		currentPath = Pathfinding.FindPath(start, dest, destination);
		isJumping = false;
		currentIndex = 0;
	}

	private void ApplyMove(Vector3 planarVelocity, float speed)
	{
		if (controller != null)
		{
			Vector3 motion = planarVelocity * Time.deltaTime;
			if (!isJumping) motion.y = Physics.gravity.y * Time.deltaTime;
			controller.Move(motion);
			if (planarVelocity.sqrMagnitude > 0.08f)
			{
				Vector3 look = planarVelocity;
				look.y = 0f;
				Quaternion targetRot = Quaternion.LookRotation(look.normalized);
				transform.rotation = Quaternion.RotateTowards(
					transform.rotation,
					targetRot,
					alignToVelocityMaxDegreesPerSecond * Time.deltaTime);
			}
		}
		else
		{
			transform.position += planarVelocity * Time.deltaTime;
		}
	}

	private void Update()
	{
		UpdateAnimatorGrounded();
	}

	private bool ComputeIsGroundedForAnimator()
	{
		if (controller != null && controller.enabled)
			return controller.isGrounded;

		const float probe = 0.45f;
		Vector3 origin = transform.position + Vector3.up * 0.15f;
		return Physics.Raycast(origin, Vector3.down, probe, ~0, QueryTriggerInteraction.Ignore);
	}

	private void UpdateAnimatorGrounded()
	{
		if (animator == null)
			return;

		bool grounded = ComputeIsGroundedForAnimator();

		animator.SetBool(AnimParams.IsGrounded, grounded);
	}

}
