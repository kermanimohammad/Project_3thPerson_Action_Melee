using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Waits then starts <see cref="VideoPlayer.Play"/> (e.g. splash intro after showing a static image).
/// Add to the same GameObject as <see cref="VideoPlayer"/> or assign the reference.
/// </summary>
public sealed class SplashIntroDelayedVideo : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField, Min(0f)] private float delaySeconds = 3f;
    [Tooltip("If true, delay uses real time (ignores Time.timeScale). Recommended for splash screens.")]
    [SerializeField] private bool useUnscaledTime = true;

    private void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            return;

        videoPlayer.playOnAwake = false;
        videoPlayer.Stop();
        StartCoroutine(PlayAfterDelay());
    }

    private IEnumerator PlayAfterDelay()
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(delaySeconds);
        else
            yield return new WaitForSeconds(delaySeconds);

        if (videoPlayer != null && videoPlayer.isActiveAndEnabled)
            videoPlayer.Play();
    }
}
