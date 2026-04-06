using System.Collections;
using UnityEngine;

/// <summary>
/// Hides a UI element at start, then activates it after a delay (e.g. splash skip button after 6s).
/// </summary>
public sealed class SplashIntroDelayedShow : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField, Min(0f)] private float delaySeconds = 6f;
    [Tooltip("If true, delay uses real time (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = true;
    [Tooltip("If true, target is SetActive(false) in Awake so it is hidden before the delay.")]
    [SerializeField] private bool hideAtStart = true;

    private void Awake()
    {
        if (target == null)
            return;

        if (hideAtStart)
            target.SetActive(false);
    }

    private void Start()
    {
        if (target == null)
            return;

        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(delaySeconds);
        else
            yield return new WaitForSeconds(delaySeconds);

        if (target != null)
            target.SetActive(true);
    }
}
