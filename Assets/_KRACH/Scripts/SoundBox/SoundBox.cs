using UnityEngine;
using FMODUnity;
using System;
using DG.Tweening;
using System.Collections.Generic;

public class SoundBox : MonoBehaviour
{
    [SerializeField] private float health;
    [SerializeField] private StudioEventEmitter musicEmitter;

    [SerializeField] private GameObject hitAnimsContainer;


    // public enum SoundBoxType
    // {
    //     Base,
    //     Drum,
    //     Lead,
    //     Sfx,
    // }
    // public SoundBoxType soundBoxType;

    // private void Initialize()
    // {

    // }

    // private void Start()
    // {
    //     musicEmitter.GetComponent<StudioEventEmitter>();
    // }


    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        health -= _damage;

        foreach (DOTweenAnimation anim in hitAnimsContainer.GetComponentsInChildren<DOTweenAnimation>())
        {
            if (anim != null) anim.DORestart();
        }

        if (health <= 0f)
        {
            GetDestroyed(_hitPoint, _hitNormal);
        }
    }

    private void GetDestroyed(Vector3 _hitPoint, Vector3 _hitNormal)
    {
        RuntimeManager.PlayOneShot("event:/SFX/SpeakerDestroy", transform.position);    // sound
        musicEmitter.Stop();

        SoundBoxSpawner.Instance.DestroyingSoundBox(this);
    }
}
