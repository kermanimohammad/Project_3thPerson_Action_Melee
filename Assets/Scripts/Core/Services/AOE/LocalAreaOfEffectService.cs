using System;
using UnityEngine;

public class LocalAreaOfEffectService : MonoBehaviour, IAreaOfEffectService
{
    private void Awake()
    {
        AreaOfEffectService.Register(this);
    }

    public void CreateSphereAOE(
        GameObject owner,
        Vector3 center,
        float radius,
        float timeToDestroy,
        Action<GameObject> onEnterAction)
    {
        GameObject aoeObject = new GameObject("SphereAOE");

        aoeObject.transform.position = center;

        SphereCollider collider = aoeObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = radius;

        Rigidbody rb = aoeObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        AOEBehaviour behaviour = aoeObject.AddComponent<AOEBehaviour>();
        behaviour.Initialize(owner, onEnterAction, timeToDestroy, AOEType.Special);
    }

    public void CreateBoxAOE(
        GameObject owner,
        Vector3 center,
        Vector3 size,
        Quaternion rotation,
        float timeToDestroy,
        Action<GameObject> onEnterAction)
    {
        GameObject aoeObject = new GameObject("BoxAOE");

        aoeObject.transform.position = center;
        aoeObject.transform.rotation = rotation;

        BoxCollider collider = aoeObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;

        Rigidbody rb = aoeObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        AOEBehaviour behaviour = aoeObject.AddComponent<AOEBehaviour>();
        behaviour.Initialize(owner, onEnterAction, timeToDestroy, AOEType.Normal);
    }
}