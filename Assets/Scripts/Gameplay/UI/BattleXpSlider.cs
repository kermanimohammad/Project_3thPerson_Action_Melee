using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Battle XP bar: slider 0 … max (default 1800). Adds each enemy <see cref="EnemyKillReward"/> kill reward; clamps at max.
/// Place on a Canvas object; assign the XP <see cref="Slider"/>.
/// </summary>
public sealed class BattleXpSlider : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider xpSlider;
    [Tooltip("Optional: shows player level, e.g. \"Level 1\".")]
    [SerializeField] private TextMeshProUGUI levelLabel;
    [Tooltip("Optional: shows current/max XP, e.g. \"450/1800\".")]
    [SerializeField] private TextMeshProUGUI xpLabel;

    [Header("XP")]
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(1f)] private float maxXp = 1800f;
    [Tooltip("When leveling up at the tree, next max XP = previous max XP × this multiplier.")]
    [SerializeField, Min(1f)] private float levelMaxXpMultiplier = 1.5f;

    [Header("Tree level-up audio (XP drain loop)")]
    [Tooltip("Looping clip while XP empties at the level-up tree. Stops when drain finishes, before level applies.")]
    [SerializeField] private AudioClip treeDrainLoopSound;
    [SerializeField, Range(0f, 1f)] private float treeDrainLoopVolume = 1f;
    [SerializeField] private AudioMixerGroup treeDrainLoopOutputGroup;
    [SerializeField] private AudioSource treeDrainLoopAudioSource;
    [Tooltip("If false, no drain loop is played even if a clip is assigned.")]
    [SerializeField] private bool playTreeDrainLoopSound = true;

    private bool _treeDrainLoopPlaying;

    public float CurrentXp { get; private set; }
    public float MaxXp => maxXp;
    public int Level => level;
    public bool IsDrainingAtTree { get; private set; }

    private void Awake()
    {
        if (xpSlider == null)
            xpSlider = GetComponent<Slider>();

        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = maxXp;
            xpSlider.value = 0f;
        }

        CurrentXp = 0f;
        BattleProgression.SetLevel(level);
        RefreshLabel();
        EnsureTreeDrainLoopAudioSource();
    }

    private void EnsureTreeDrainLoopAudioSource()
    {
        if (!playTreeDrainLoopSound || treeDrainLoopSound == null || treeDrainLoopAudioSource != null)
            return;

        treeDrainLoopAudioSource = GetComponent<AudioSource>();
        if (treeDrainLoopAudioSource == null)
            treeDrainLoopAudioSource = gameObject.AddComponent<AudioSource>();
        treeDrainLoopAudioSource.playOnAwake = false;
        treeDrainLoopAudioSource.loop = true;
        if (treeDrainLoopOutputGroup != null)
            treeDrainLoopAudioSource.outputAudioMixerGroup = treeDrainLoopOutputGroup;
    }

    private void OnEnable()
    {
        EnemyKillReward.OnEnemyKilledWithReward += OnEnemyKilled;
    }

    private void OnDisable()
    {
        EnemyKillReward.OnEnemyKilledWithReward -= OnEnemyKilled;
        StopTreeDrainLoop();
    }

    private void OnEnemyKilled(float reward, GameObject _)
    {
        if (IsDrainingAtTree)
            return;

        if (reward <= 0f || maxXp <= 0f)
            return;

        if (CurrentXp >= maxXp)
            return;

        float add = Mathf.Min(reward, maxXp - CurrentXp);
        CurrentXp += add;

        if (xpSlider != null)
            xpSlider.value = CurrentXp;

        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (levelLabel != null)
            levelLabel.text = $"Level {level}";

        int cur = Mathf.RoundToInt(CurrentXp);
        int mx = Mathf.RoundToInt(maxXp);
        if (xpLabel != null)
            xpLabel.text = $"{cur}/{mx}";

        BattleProgression.SetLevel(level);
    }

    public bool CanLevelUpAtTree()
    {
        return !IsDrainingAtTree && maxXp > 0f && CurrentXp >= maxXp;
    }

    /// <summary>
    /// Drains XP to zero over <paramref name="drainSeconds"/>, then increments level and increases max XP by multiplier.
    /// Safe to call repeatedly; will ignore if not eligible.
    /// <paramref name="onTreeDrainStarted"/> when XP drain begins (e.g. tree particle on).
    /// <paramref name="onTreeDrainEnded"/> when XP has reached zero after the drain (e.g. tree particle off), before level/max XP change.
    /// <paramref name="onLevelUpCompleted"/> after level and max XP are updated (e.g. completion sound).
    /// </summary>
    public void StartTreeLevelUp(float drainSeconds = 3f, Action onLevelUpCompleted = null, Action onTreeDrainStarted = null, Action onTreeDrainEnded = null)
    {
        if (!CanLevelUpAtTree())
            return;

        if (drainSeconds <= 0f)
            drainSeconds = 0.01f;

        StartCoroutine(DrainXpThenLevelUp(drainSeconds, onLevelUpCompleted, onTreeDrainStarted, onTreeDrainEnded));
    }

    private System.Collections.IEnumerator DrainXpThenLevelUp(float drainSeconds, Action onLevelUpCompleted, Action onTreeDrainStarted, Action onTreeDrainEnded)
    {
        IsDrainingAtTree = true;
        onTreeDrainStarted?.Invoke();
        PlayTreeDrainLoop();

        float startXp = Mathf.Clamp(CurrentXp, 0f, maxXp);
        float t = 0f;
        while (t < drainSeconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / drainSeconds);
            SetCurrentXpInternal(Mathf.Lerp(startXp, 0f, a));
            yield return null;
        }

        StopTreeDrainLoop();
        SetCurrentXpInternal(0f);
        onTreeDrainEnded?.Invoke();

        level = Mathf.Max(1, level + 1);
        maxXp = Mathf.Max(1f, maxXp * Mathf.Max(1f, levelMaxXpMultiplier));

        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = maxXp;
            xpSlider.value = 0f;
        }

        RefreshLabel();
        IsDrainingAtTree = false;

        onLevelUpCompleted?.Invoke();
    }

    private void PlayTreeDrainLoop()
    {
        if (!playTreeDrainLoopSound || treeDrainLoopSound == null)
            return;

        EnsureTreeDrainLoopAudioSource();
        if (treeDrainLoopAudioSource == null)
            return;

        treeDrainLoopAudioSource.loop = true;
        treeDrainLoopAudioSource.clip = treeDrainLoopSound;
        treeDrainLoopAudioSource.volume = treeDrainLoopVolume;
        treeDrainLoopAudioSource.Play();
        _treeDrainLoopPlaying = true;
    }

    private void StopTreeDrainLoop()
    {
        if (treeDrainLoopAudioSource == null || !_treeDrainLoopPlaying)
            return;

        treeDrainLoopAudioSource.Stop();
        _treeDrainLoopPlaying = false;
    }

    private void SetCurrentXpInternal(float value)
    {
        CurrentXp = Mathf.Clamp(value, 0f, maxXp);
        if (xpSlider != null)
            xpSlider.value = CurrentXp;
        RefreshLabel();
    }

    /// <summary>Reset to start of battle (e.g. scene load).</summary>
    public void ResetXpToZero()
    {
        CurrentXp = 0f;
        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = maxXp;
            xpSlider.value = 0f;
        }

        RefreshLabel();
    }
}
