using UnityEngine;

public class ConcreteEnemyMover : EnemyMoverBase
{
	[SerializeField] private float speed = 3.2f;
	[SerializeField] private float arriveDistance = 1.35f;
	[SerializeField] private Animator animator;
	[SerializeField] private CharacterController controller;

	private Vector3 targetPosition;
	private Vector3 _velocity;

	public override float CurrentSpeed => speed;

	public override void FaceTowards(Vector3 target)
	{
		Vector3 direction = target - transform.position;
		direction.y = 0f;

		if (direction.sqrMagnitude <= 0.0001f)
			return;

		Quaternion targetRotation = Quaternion.LookRotation(direction);
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
	}

	public override bool HasReachedDestination(float threshold)
	{
		Vector3 flatCurrent = transform.position;
		Vector3 flatTarget = targetPosition;

		flatCurrent.y = 0f;
		flatTarget.y = 0f;

		return Vector3.Distance(flatCurrent, flatTarget) <= threshold;
	}

	public override void MoveTo(Vector3 destination, float speedMultiplier = 1) => MoveTo(destination, Vector3.zero, speedMultiplier);

	public override void MoveTo(Vector3 destination, Vector3 groupSeparationVector, float speedMultiplier = 1)
	{
		targetPosition = destination;
		Vector3 flat = destination - transform.position;
		flat.y = 0f;
		if (flat.sqrMagnitude < arriveDistance * arriveDistance)
		{
			_velocity = Vector3.zero;
			// pathfinding possibly here
			return;
		}

		Vector3 dir = flat.normalized + groupSeparationVector;
		if (dir.sqrMagnitude < 0.001f)
			dir = flat.normalized;
		else
			dir.Normalize();

		_velocity = dir * speed;

		// pathfinding possibly here

		ApplyMove(dir * speed, speed);

		animator.SetFloat(AnimParams.Speed, _velocity.magnitude);
		animator.SetBool(AnimParams.IsGrounded, controller.isGrounded);
	}

	private void ApplyMove(Vector3 planarVelocity, float speed)
	{
		if (controller != null)
		{
			Vector3 motion = planarVelocity * Time.deltaTime;
			motion.y = Physics.gravity.y * Time.deltaTime;
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

	public override void StopMoving()
	{
		//animator.SetFloat(AnimParams.Speed, 0f);
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