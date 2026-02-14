using UnityEngine;

public class BillboardFacingCamera : MonoBehaviour
{
    Camera mainCamera;


    void Start()
    {
        mainCamera = Camera.main; 
    }

    void Update()
    {
        Vector3 directionToCamera = mainCamera.transform.position - transform.position;
        directionToCamera.y = 0.0f;

        transform.rotation = Quaternion.LookRotation(directionToCamera);
    }
}
