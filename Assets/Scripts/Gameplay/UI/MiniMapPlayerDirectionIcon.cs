using UnityEngine;

/// <summary>
/// Rotates the player minimap icon to match the player's facing direction.
/// World Y (yaw) is mapped to UI Z rotation.
/// </summary>
public class MiniMapPlayerDirectionIcon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float autoBindRetrySeconds = 1.0f;

    [Header("Rotation")]
    [Tooltip("If true, rotation direction is inverted (useful if your minimap is mirrored).")]
    [SerializeField] private bool invertRotation = true;
    [Tooltip("Extra degrees added after mapping. Use this to align your icon art (e.g., if the arrow points up).")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    private Coroutine bindRoutine;

    private void Reset()
    {
        playerIcon = transform as RectTransform;
    }

    private void Awake()
    {
        if (playerIcon == null)
            playerIcon = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (player == null)
            BindToActivePlayer();
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (player == null || playerIcon == null)
            return;

        float yaw = player.eulerAngles.y;
        float z = (invertRotation ? -yaw : yaw) + rotationOffsetDegrees;
        playerIcon.localEulerAngles = new Vector3(0f, 0f, z);
    }

    public void BindToActivePlayer()
    {
        if (TryResolveActivePlayer(out var t))
        {
            player = t;
            return;
        }

        if (autoBindRetrySeconds > 0f && bindRoutine == null)
            bindRoutine = StartCoroutine(BindRetryRoutine());
    }

    public void SetPlayer(Transform playerTransform) => player = playerTransform;

    private System.Collections.IEnumerator BindRetryRoutine()
    {
        float t = 0f;
        while (t < autoBindRetrySeconds)
        {
            if (TryResolveActivePlayer(out var p))
            {
                player = p;
                bindRoutine = null;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        bindRoutine = null;
    }

    private bool TryResolveActivePlayer(out Transform playerTransform)
    {
        playerTransform = null;

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null && go.activeInHierarchy)
            {
                playerTransform = go.transform;
                return true;
            }
        }

        PlayerMotor[] motors = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
        for (int i = 0; i < motors.Length; i++)
        {
            var m = motors[i];
            if (m == null || !m.isActiveAndEnabled)
                continue;
            playerTransform = m.transform;
            return true;
        }

        return false;
    }
}

