using UnityEngine;

public class BillboardFacingCamera : MonoBehaviour
{
    private Camera mainCamera;

    [SerializeField] private bool flippedSprite;

    private void Start()
    {
        mainCamera = Camera.main; 
    }

    private void Update()
    {
        Vector3 directionToCamera = mainCamera.transform.position - transform.position;
        directionToCamera.y = 0.0f;

        if(flippedSprite)
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera) * Quaternion.Euler(0, 180, 0);
        }
    }
}
