using System;
using UnityEngine;

public class EnemySpawnedMember : MonoBehaviour
{
    [SerializeField] private Health health;

    public event Action<EnemySpawnedMember> OnMemberDied;

    private bool notified;

    private void Reset()
    {
        health = GetComponent<Health>();
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (notified)
            return;

        notified = true;
        OnMemberDied?.Invoke(this);
    }
}