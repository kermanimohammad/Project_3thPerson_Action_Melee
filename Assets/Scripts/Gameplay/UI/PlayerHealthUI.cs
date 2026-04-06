using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [Header("Low health blink")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text healthText;
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.2f;
    [Tooltip("Blink speed in cycles per second.")]
    [SerializeField, Min(0.1f)] private float blinkCyclesPerSecond = 2.0f;
    [SerializeField] private Color normalFillColor = Color.white;
    [SerializeField] private Color lowFillColor = Color.red;

    [SerializeField] private Health playerHealth;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float autoBindRetrySeconds = 1.0f;

    private Coroutine bindRoutine;
    private Coroutine blinkRoutine;
    private bool isLow;

    private void OnEnable()
    {
        // If another system already assigned the active player (e.g., loadout applier), keep it.
        if (playerHealth != null && playerHealth.isActiveAndEnabled)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;
            RefreshNow();
        }
        else
        {
            BindToActivePlayerHealth();
        }

        ApplyNormalFillColor();
    }

    private void OnDisable()
    {
        Unbind();

        StopBlink();
        ApplyNormalFillColor();

        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }
    }

    private void UpdateHealthBar(float current, float max)
    {
        if (healthSlider == null)
            return;
        float normalized = max <= 0f ? 0f : (float)current / max;
        healthSlider.value = normalized;
        UpdateLowState(normalized);
    }

    /// <summary>Call this after enabling the chosen character, if needed.</summary>
    public void BindToActivePlayerHealth()
    {
        Unbind();

        if (TryResolveActivePlayerHealth(out var h))
        {
            playerHealth = h;
            playerHealth.OnHealthChanged += UpdateHealthBar;
            RefreshNow();
            return;
        }

        if (autoBindRetrySeconds > 0f && bindRoutine == null)
            bindRoutine = StartCoroutine(BindRetryRoutine());
    }

    /// <summary>Explicitly bind to a known Health component (recommended for multi-hero scenes).</summary>
    public void SetTargetHealth(Health health)
    {
        Unbind();
        playerHealth = health;
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;
            RefreshNow();
        }
    }

    private void RefreshNow()
    {
        if (playerHealth == null)
            return;
        UpdateHealthBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void Unbind()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;
    }

    private System.Collections.IEnumerator BindRetryRoutine()
    {
        float t = 0f;
        while (t < autoBindRetrySeconds)
        {
            if (TryResolveActivePlayerHealth(out var h))
            {
                playerHealth = h;
                playerHealth.OnHealthChanged += UpdateHealthBar;
                RefreshNow();
                bindRoutine = null;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        bindRoutine = null;
    }

    private void UpdateLowState(float normalized)
    {
        bool lowNow = normalized < lowHealthThreshold;
        if (lowNow == isLow)
            return;

        isLow = lowNow;
        if (isLow)
            StartBlink();
        else
        {
            StopBlink();
            ApplyNormalFillColor();
        }
    }

    private void StartBlink()
    {
        if (fillImage == null && healthText == null)
            return;
        if (blinkRoutine != null)
            return;
        blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    private void StopBlink()
    {
        if (blinkRoutine == null)
            return;
        StopCoroutine(blinkRoutine);
        blinkRoutine = null;
    }

    private System.Collections.IEnumerator BlinkRoutine()
    {
        while (true)
        {
            if (fillImage == null && healthText == null)
            {
                blinkRoutine = null;
                yield break;
            }

            float t = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f) * blinkCyclesPerSecond) + 1f) * 0.5f;
            Color c = Color.Lerp(normalFillColor, lowFillColor, t);
            if (fillImage != null) fillImage.color = c;
            if (healthText != null) healthText.color = c;
            yield return null;
        }
    }

    private void ApplyNormalFillColor()
    {
        if (fillImage != null)
            fillImage.color = normalFillColor;
        if (healthText != null)
            healthText.color = normalFillColor;
    }

    private bool TryResolveActivePlayerHealth(out Health health)
    {
        health = null;

        // Prefer objects tagged Player (including parent chain), and only those active in hierarchy.
        Health[] all = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            if (h == null || !h.isActiveAndEnabled)
                continue;

            if (IsTaggedInParentChain(h.transform, playerTag))
            {
                health = h;
                return true;
            }
        }

        // Fallback: first active Health in scene (useful if tag isn't set correctly yet).
        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            if (h == null || !h.isActiveAndEnabled)
                continue;
            health = h;
            return true;
        }

        return false;
    }

    private static bool IsTaggedInParentChain(Transform t, string tag)
    {
        while (t != null)
        {
            if (t.CompareTag(tag))
                return true;
            t = t.parent;
        }
        return false;
    }

}