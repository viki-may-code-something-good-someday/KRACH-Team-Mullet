using TMPro;
using UnityEngine;

public class Player_Interact : MonoBehaviour
{
    Camera cameraMain;
    float hitRange = 2f;
    Interactable targetInteractable;
    Destructable targetDestructable;



    void Start()
    {
        cameraMain = Camera.main;

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Interact Pressed");
            TryInteract();
        }
    }

    private void TryInteract()
    {
        RaycastHit hitinfo;
        //Interactable 
        if (Physics.Raycast(cameraMain.transform.position, cameraMain.transform.forward, out hitinfo, hitRange, LayerMask.GetMask("Interactable"), QueryTriggerInteraction.Ignore))
        {
            hitinfo.collider.TryGetComponent<Interactable>(out Interactable interactableObj);
            if (interactableObj != null)
            {
                targetInteractable = interactableObj;
                targetInteractable.Interact();
            }
        }
        Debug.Log($"hit: {hitinfo.transform.name}");

        RaycastHit hitinfoDestructable;
        //Destructable (hitinfo needed)
        if(Physics.Raycast(cameraMain.transform.position, cameraMain.transform.forward, out hitinfoDestructable, hitRange, LayerMask.GetMask("Destructable"), QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"hit: {hitinfoDestructable.transform.name}");
            hitinfoDestructable.collider.TryGetComponent<Destructable>(out Destructable destructableObject);
            if (destructableObject != null)
            {
                targetDestructable = destructableObject;
                targetDestructable.Destruct(hitinfoDestructable.point, hitinfoDestructable.normal);
            }
        }
    }

}
