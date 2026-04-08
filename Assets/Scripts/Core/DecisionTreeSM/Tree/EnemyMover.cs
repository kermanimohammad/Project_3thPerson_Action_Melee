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

	private Vector3 _velocity;
	List<Node> currentPath;
	int currentIndex = 0;
	private bool isJumping = false;
	private Vector3 jumpStart;
	private Vector3 jumpEnd;
	private float jumpTimer = 0f;

	public float CurrentSpeed => speed;

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

		Vector3 dir = flat.normalized + groupSeparationVector;
		if (dir.sqrMagnitude < 0.001f)
			dir = flat.normalized;
		else
			dir.Normalize();

		_velocity = dir * speed;

		ApplyMove(dir * speed, speed);
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

		MoveTo(currentNode.loc);
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
			transform.rotation = Quaternion.Slerp(
				transform.rotation,
				Quaternion.LookRotation(look.normalized),
				10f * Time.deltaTime
			);
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
			if (planarVelocity.sqrMagnitude > 0.01f)
			{
				Vector3 look = planarVelocity;
				look.y = 0f;
				transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), 10f * Time.deltaTime);
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
