using UnityEngine;
using FMODUnity;

public class SoundBox : MonoBehaviour
{
    [SerializeField] private float health;
    [SerializeField] private StudioEventEmitter musicEmitter;

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

    private void Start()
    {
        musicEmitter.GetComponent<StudioEventEmitter>();
    }

    private void Update()
    {
        Idle();
    }

    private void Idle()
    {
        // Dotween here
    }

    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        health -= _damage;

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
