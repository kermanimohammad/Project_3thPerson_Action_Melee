using UnityEngine;

public abstract class EnemyState : MonoBehaviour
{
	public abstract void Enter();
	public abstract void Exit();
	public abstract void Tick();

#if UNITY_EDITOR
	public virtual void DrawGizmos() {}
#endif
}