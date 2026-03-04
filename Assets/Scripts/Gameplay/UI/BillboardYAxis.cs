using UnityEngine;

public class BillboardYAxis : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        Vector3 direction = mainCamera.transform.position - transform.position;

        // Remove vertical influence (so it doesn't rotate on X)
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-direction);
        }
    }
}