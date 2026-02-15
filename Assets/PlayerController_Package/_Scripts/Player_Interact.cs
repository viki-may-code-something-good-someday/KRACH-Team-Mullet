using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using FMODUnity;

public class Player_Interact : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private List<GameObject> armsVisuals = new List<GameObject>();
    [Header("Interaction Settings")]
    [SerializeField] private float hitRange;
    [SerializeField] private float hitDamage;

    private Camera cameraMain;
    private bool rightArmPunching;


    void Start()
    {
        cameraMain = Camera.main;

    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PunchAnimation();
            TryInteract();
        }
    }

    private void TryInteract()
    {
        bool punchedAir = false;

        //Interactable 
        if (Physics.Raycast(cameraMain.transform.position, cameraMain.transform.forward, out RaycastHit hitinfo, hitRange, LayerMask.GetMask("Interactable"), QueryTriggerInteraction.Ignore))
        {
            Debug.Log("1" + hitinfo.transform.name);
            hitinfo.collider.TryGetComponent<Interactable>(out Interactable interactableObj);
            if (interactableObj != null)
            {
                interactableObj.Interact();
                RuntimeManager.PlayOneShot("event:/SFX/Punch");
            }
        }
        else punchedAir = true; 

        //Destructable (hitinfo needed)
        if(Physics.Raycast(cameraMain.transform.position, cameraMain.transform.forward, out RaycastHit hitinfoDestructable, hitRange, LayerMask.GetMask("Destructable"), QueryTriggerInteraction.Ignore))
        {
            Debug.Log("2" + hitinfoDestructable.transform.name);
            hitinfoDestructable.collider.TryGetComponent<Destructable>(out Destructable destructableObject);
            if (destructableObject != null)
            {
                destructableObject.Destruct(hitDamage,hitinfoDestructable.point, hitinfoDestructable.normal);
                RuntimeManager.PlayOneShot("event:/SFX/Punch");
            }
        }
        else punchedAir = true;
        
        if (punchedAir) RuntimeManager.PlayOneShot("event:/SFX/PunchAir");    // sound
    }

    private void PunchAnimation()
    {
        if(armsVisuals.Count != 2)
        {
            Debug.LogWarning("PunchAnimation: Expected 2 arms, got " + armsVisuals.Count);
            return;
        }

        int selectedArm;

        if(rightArmPunching)
        {
            selectedArm = 0;
        }
        else
        {
            selectedArm = 1;
        }

        rightArmPunching = !rightArmPunching;


        List<DOTweenAnimation> dotweenAnims = new List<DOTweenAnimation>(armsVisuals[selectedArm].GetComponents<DOTweenAnimation>());
        if (dotweenAnims.Count > 0)
        {
            foreach (DOTweenAnimation anim in dotweenAnims)
            {
                anim.DORestart();
            }
        }
        else
        {
            Debug.LogError("PunchAnimation: DOTweenAnimation component missing on " + armsVisuals[selectedArm].name);
        }
    }

}
