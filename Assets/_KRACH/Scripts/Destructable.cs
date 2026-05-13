using Unity.Entities.UniversalDelegates;
using UnityEngine;

public class Destructable : MonoBehaviour
{
    [SerializeField] private WallData wallData;
    [SerializeField] private SoundBox soundBox;
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private ParticleSystem soundboxDamageParticles;


    public void Destruct(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        if (wallData != null)
        {
            // Spawn Wall Damage Particles
            if (dustParticles != null) Instantiate(dustParticles, _hitPoint, Quaternion.LookRotation(_hitNormal));

            wallData.TakeDamage(_damage, _hitPoint, _hitNormal);

        }
        else if (soundBox != null)
        {
            // Spawn Soundbox Damage Particles
            if (soundboxDamageParticles != null) Instantiate(soundboxDamageParticles, _hitPoint, Quaternion.LookRotation(_hitNormal));

            soundBox.TakeDamage(_damage, _hitPoint, _hitNormal);
        }
    }
}
