using System;
using UnityEngine;

/// <summary>
/// Per-enemy kill reward amount (set in Inspector). When <see cref="Health"/> reaches zero, fires once.
/// Other systems can subscribe to <see cref="OnEnemyKilledWithReward"/> or read <see cref="RewardAmount"/> from the dying enemy.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyKillReward : MonoBehaviour
{
    [Header("Reward")]
    [Tooltip("Award value granted when this enemy dies (e.g. score / currency).")]
    [SerializeField] private float rewardAmount = 10f;

    /// <summary>Raised once when this enemy dies: (reward, enemy root).</summary>
    public static event Action<float, GameObject> OnEnemyKilledWithReward;

    public float RewardAmount => rewardAmount;

    private Health _health;
    private bool _rewardEmitted;

    private void Awake()
    {
        _health = GetComponent<Health>();
        if (_health == null)
            _health = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnHealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (_rewardEmitted || current > 0f)
            return;

        _rewardEmitted = true;
        OnEnemyKilledWithReward?.Invoke(rewardAmount, gameObject);
    }
}
