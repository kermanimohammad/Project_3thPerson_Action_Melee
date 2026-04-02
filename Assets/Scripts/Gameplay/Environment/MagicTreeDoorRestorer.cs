using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Magic tree that restores all doors after the player stays nearby for a duration.
/// Shows a UI Slider fill while charging.
/// </summary>
public class MagicTreeDoorRestorer : MonoBehaviour
{
    [System.Serializable]
    public class DoorSpawn
    {
        public Transform spawn;
        public GameObject doorPrefab;
    }

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("If set, only a collider on these layers can trigger restoration.")]
    [SerializeField] private LayerMask playerLayers = ~0;

    [Header("Charge UI")]
    [SerializeField] private Slider chargeSlider;
    [SerializeField] private GameObject chargeUiRoot;
    [SerializeField] private float chargeSeconds = 3f;

    [Header("Door restore")]
    [Tooltip("When restoration completes, all DoorBreakable objects are destroyed and these prefabs are spawned.")]
    [SerializeField] private List<DoorSpawn> doorSpawns = new List<DoorSpawn>();
    [Tooltip("If true, destroys existing DoorBreakable objects before spawning new doors.")]
    [SerializeField] private bool destroyExistingBreakableDoors = true;

    private Coroutine chargeRoutine;
    private bool restoring;

    private void Awake()
    {
        SetUiVisible(false);
        if (chargeSlider != null)
            chargeSlider.value = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;
        StartCharging();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;
        StopCharging();
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (((1 << other.gameObject.layer) & playerLayers.value) == 0)
            return false;

        if (other.CompareTag(playerTag))
            return true;

        // In case the trigger hits a child collider of the player.
        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag))
                return true;
            t = t.parent;
        }

        return false;
    }

    private void StartCharging()
    {
        if (restoring)
            return;

        SetUiVisible(true);

        if (chargeRoutine != null)
            StopCoroutine(chargeRoutine);

        chargeRoutine = StartCoroutine(ChargeRoutine());
    }

    private void StopCharging()
    {
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        if (chargeSlider != null)
            chargeSlider.value = 0f;

        SetUiVisible(false);
    }

    private IEnumerator ChargeRoutine()
    {
        float duration = Mathf.Max(0.05f, chargeSeconds);
        float t = 0f;

        if (chargeSlider != null)
            chargeSlider.value = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            if (chargeSlider != null)
                chargeSlider.value = Mathf.Clamp01(t / duration);
            yield return null;
        }

        chargeRoutine = null;
        restoring = true;

        try
        {
            RestoreDoors();
        }
        finally
        {
            restoring = false;
            if (chargeSlider != null)
                chargeSlider.value = 0f;
            SetUiVisible(false);
        }
    }

    private void RestoreDoors()
    {
        if (destroyExistingBreakableDoors)
        {
            DoorBreakable[] existing = Object.FindObjectsByType<DoorBreakable>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null)
                    continue;
                Destroy(existing[i].gameObject);
            }
        }

        for (int i = 0; i < doorSpawns.Count; i++)
        {
            DoorSpawn ds = doorSpawns[i];
            if (ds == null || ds.spawn == null || ds.doorPrefab == null)
                continue;

            Instantiate(ds.doorPrefab, ds.spawn.position, ds.spawn.rotation);
        }
    }

    private void SetUiVisible(bool visible)
    {
        if (chargeUiRoot != null)
            chargeUiRoot.SetActive(visible);
        else if (chargeSlider != null)
            chargeSlider.gameObject.SetActive(visible);
    }
}

