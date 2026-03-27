using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AOEBehaviour : MonoBehaviour
{
    private Action<GameObject> onEnterAction;
    private float lifetime;
    private float spawnTime;

    private AOEType aoeType;
    private Collider aoeCollider;

    private HashSet<GameObject> affectedObjects = new HashSet<GameObject>();

    public void Initialize(Action<GameObject> action, float timeToDestroy, AOEType type)
    {
        onEnterAction = action;
        lifetime = timeToDestroy;
        aoeType = type;
        spawnTime = Time.time;

        aoeCollider = GetComponent<Collider>();

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (affectedObjects.Contains(other.gameObject))
            return;

        affectedObjects.Add(other.gameObject);

        onEnterAction?.Invoke(other.gameObject);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (aoeCollider == null) return;

        float elapsed = Time.time - spawnTime;
        float alpha = Mathf.Clamp01(1f - (elapsed / lifetime));

        Color baseColor = aoeType == AOEType.Special
            ? new Color(1f, 0f, 0f, alpha)      // Red
            : new Color(1f, 1f, 0f, alpha);     // Yellow

        Gizmos.color = baseColor;

        if (aoeCollider is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.position, sphere.radius);
        }
        else if (aoeCollider is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}