using UnityEngine;

/// <summary>
/// Rotates an object purely locally (no networking) around the Y axis towards the active camera.
/// For sprites created at runtime that have no NetworkIdentity (e.g. falling wall
/// decorations). For networked, physics-driven billboards use BillboardObject instead.
/// </summary>
public class CameraFacingSprite : MonoBehaviour
{
    [SerializeField] private bool flipped;

    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        Vector3 toCamera = targetCamera.transform.position - transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(toCamera);
        transform.rotation = flipped ? look : look * Quaternion.Euler(0f, 180f, 0f);
    }
}
