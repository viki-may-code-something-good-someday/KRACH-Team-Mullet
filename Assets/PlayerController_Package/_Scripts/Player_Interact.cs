using UnityEngine;

public class Player_Interact : MonoBehaviour
{
    [SerializeField] private float hitRange;
    [SerializeField] private float hitDamage;

    private Camera cameraMain;


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
        //Interactable 
        if (Physics.Raycast(cameraMain.transform.position, cameraMain.transform.forward, out RaycastHit hitinfo, hitRange, LayerMask.GetMask("Interactable"), QueryTriggerInteraction.Ignore))
        {
            Debug.Log("1" + hitinfo.transform.name);
            hitinfo.collider.TryGetComponent<Interactable>(out Interactable interactableObj);
            if (interactableObj != null)
            {
                interactableObj.Interact();
            }
        }

        var test = 1;
        test += 1;

        //Destructable (hitinfo needed)
        if(Physics.Raycast(cameraMain.transform.position, cameraMain.transform.forward, out RaycastHit hitinfoDestructable, hitRange, LayerMask.GetMask("Destructable"), QueryTriggerInteraction.Ignore))
        {
            Debug.Log("2" + hitinfoDestructable.transform.name);
            hitinfoDestructable.collider.TryGetComponent<Destructable>(out Destructable destructableObject);
            if (destructableObject != null)
            {
                destructableObject.Destruct(hitDamage,hitinfoDestructable.point, hitinfoDestructable.normal);
            }
        }
    }

}
