using Mirror; // Mirror hinzugefügt
using UnityEngine;

public class Destructable : NetworkBehaviour
{
    [SerializeField] private WallData wallData;
    [SerializeField] private SoundBox soundBox;
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private ParticleSystem soundboxDamageParticles;

    // Wird vom Player_Interact auf dem Server ausgeführt
    [Server]
    public void Destruct(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        // 1. Logik (Schaden) wird NUR auf dem Server berechnet
        if (wallData != null)
        {
            wallData.TakeDamage(_damage, _hitPoint, _hitNormal);
            RpcShowEffects(_hitPoint, _hitNormal, true); // true = Wall
        }
        else if (soundBox != null)
        {
            soundBox.TakeDamage(_damage, _hitPoint, _hitNormal);
            RpcShowEffects(_hitPoint, _hitNormal, false); // false = Soundbox
        }
    }

    // 2. Visuelles Feedback wird an ALLE Clients gesendet
    [ClientRpc]
    private void RpcShowEffects(Vector3 _hitPoint, Vector3 _hitNormal, bool isWall)
    {
        if (isWall && dustParticles != null)
        {
            Instantiate(dustParticles, _hitPoint, Quaternion.LookRotation(_hitNormal));
        }
        else if (!isWall && soundboxDamageParticles != null)
        {
            Instantiate(soundboxDamageParticles, _hitPoint, Quaternion.LookRotation(_hitNormal));
        }
    }
}