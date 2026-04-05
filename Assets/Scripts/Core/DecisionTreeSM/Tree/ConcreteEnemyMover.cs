using UnityEngine;

public class ConcreteEnemyMover : EnemyMoverBase
{
	[SerializeField] private float speed = 1f;
	[SerializeField] private Animator animator;
	[SerializeField] private CharacterController controller;

	private Vector3 targetPosition;
	private float verticalVelocity;
	private const float _gravity = -9.81f;

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

	public override void MoveTo(Vector3 destination, float speedMultiplier = 1)
	{
		targetPosition = destination;

		float finalSpeed = speed * speedMultiplier;

		Vector3 horizontal = targetPosition - transform.position;
		horizontal.y = 0f;

		bool reached = horizontal.sqrMagnitude <= 0.0025f || HasReachedDestination(0.05f);

		if (reached)
		{
			StopMoving();
			ApplyGravity();
			return;
		}

		horizontal.Normalize();

		// Apply horizontal movement
		Vector3 velocity = horizontal * finalSpeed;

		// Apply gravity
		if (controller.isGrounded && verticalVelocity < 0f)
			verticalVelocity = -2f; // stick to ground
		else
			verticalVelocity += _gravity * Time.deltaTime;

		velocity.y = verticalVelocity;

		controller.Move(velocity * Time.deltaTime);

		FaceTowards(targetPosition);

		animator.SetFloat(AnimParams.Speed, finalSpeed);
		animator.SetBool(AnimParams.IsGrounded, controller.isGrounded);
	}

	public override void StopMoving()
	{
		ApplyGravity();
		animator.SetFloat(AnimParams.Speed, 0f);
		animator.SetBool(AnimParams.IsGrounded, controller.isGrounded);
	}

	private void ApplyGravity()
	{
		if (controller.isGrounded && verticalVelocity < 0f)
			verticalVelocity = -2f;
		else
			verticalVelocity += _gravity * Time.deltaTime;

		controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
	}
}