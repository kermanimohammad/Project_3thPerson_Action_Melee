using System;
using UnityEngine;

public interface IAreaOfEffectService
{
    void CreateSphereAOE(
        Vector3 center,
        float radius,
        float timeToDestroy,
        Action<GameObject> onEnterAction);

    void CreateBoxAOE(
        Vector3 center,
        Vector3 size,
        Quaternion rotation,
        float timeToDestroy,
        Action<GameObject> onEnterAction);
}