using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStaminaUI : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [Header("Low stamina blink")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField, Range(0f, 1f)] private float lowStaminaThreshold = 0.2f;
    [Tooltip("Blink speed in cycles per second.")]
    [SerializeField, Min(0.1f)] private float blinkCyclesPerSecond = 2.0f;
    [SerializeField] private Color normalFillColor = Color.white;
    [SerializeField] private Color lowFillColor = Color.red;

    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float autoBindRetrySeconds = 1.0f;

    private Coroutine bindRoutine;
    private Coroutine blinkRoutine;
    private bool isLow;

    private void OnEnable()
    {
        if (playerStamina != null && playerStamina.isActiveAndEnabled)
        {
            playerStamina.OnStaminaChanged += UpdateBar;
            RefreshNow();
        }
        else
        {
            BindToActivePlayerStamina();
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

    private void UpdateBar(float current, float max)
    {
        if (staminaSlider == null)
            return;
        float normalized = max <= 0f ? 0f : current / max;
        staminaSlider.value = normalized;
        UpdateLowState(normalized);
    }

    public void BindToActivePlayerStamina()
    {
        Unbind();

        if (TryResolveActivePlayerStamina(out var s))
        {
            playerStamina = s;
            playerStamina.OnStaminaChanged += UpdateBar;
            RefreshNow();
            return;
        }

        if (autoBindRetrySeconds > 0f && bindRoutine == null)
            bindRoutine = StartCoroutine(BindRetryRoutine());
    }

    public void SetTargetStamina(PlayerStamina stamina)
    {
        Unbind();
        playerStamina = stamina;
        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged += UpdateBar;
            RefreshNow();
        }
    }

    private void RefreshNow()
    {
        if (playerStamina == null)
            return;
        UpdateBar(playerStamina.CurrentStamina, playerStamina.MaxStamina);
    }

    private void Unbind()
    {
        if (playerStamina != null)
            playerStamina.OnStaminaChanged -= UpdateBar;
    }

    private System.Collections.IEnumerator BindRetryRoutine()
    {
        float t = 0f;
        while (t < autoBindRetrySeconds)
        {
            if (TryResolveActivePlayerStamina(out var s))
            {
                playerStamina = s;
                playerStamina.OnStaminaChanged += UpdateBar;
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
        bool lowNow = normalized < lowStaminaThreshold;
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
        if (fillImage == null && staminaText == null)
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
            if (fillImage == null && staminaText == null)
            {
                blinkRoutine = null;
                yield break;
            }

            float t = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f) * blinkCyclesPerSecond) + 1f) * 0.5f;
            Color c = Color.Lerp(normalFillColor, lowFillColor, t);
            if (fillImage != null) fillImage.color = c;
            if (staminaText != null) staminaText.color = c;
            yield return null;
        }
    }

    private void ApplyNormalFillColor()
    {
        if (fillImage != null)
            fillImage.color = normalFillColor;
        if (staminaText != null)
            staminaText.color = normalFillColor;
    }

    private bool TryResolveActivePlayerStamina(out PlayerStamina stamina)
    {
        stamina = null;

        PlayerStamina[] all = Object.FindObjectsByType<PlayerStamina>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || !s.isActiveAndEnabled)
                continue;

            if (IsTaggedInParentChain(s.transform, playerTag))
            {
                stamina = s;
                return true;
            }
        }

        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || !s.isActiveAndEnabled)
                continue;
            stamina = s;
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

