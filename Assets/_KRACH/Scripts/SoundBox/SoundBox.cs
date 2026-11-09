using System.Collections;
using UnityEngine;
using FMODUnity;
using DG.Tweening;
using Mirror;

public class SoundBox : NetworkBehaviour, IDestructable
{
    [Header("References")]
    [SerializeField] private GameObject hitAnimsContainer;
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private LuaSoundEmitter hitSoundEmitter;
    [SerializeField] private LuaSoundEmitter destroySoundEmitter;

    [Header("Settings")]
    [SerializeField] private float health;
    [Tooltip("Delay between the killing blow and the box actually being destroyed – lets the hit " +
             "animation play out. The box can't take further damage during this time.")]
    [SerializeField] private float destroyDelay = 1.5f;

    public ParticleSystem HitParticles => hitParticles;

    /// <summary>Fires exactly once per box on the server as soon as it was destroyed.</summary>
    public static event System.Action<SoundBox> OnDestroyedServer;

    private bool isDead;

    [Server]
    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        // A dead box (waiting for destroyDelay) must not take further hits – previously every
        // hit during the delay scheduled another destruction, so OnDestroyedServer could fire
        // multiple times and the win check / counter got confused.
        if (isDead) return;

        health -= _damage;
        RpcShowEffects(_hitPoint, _hitNormal);

        if (health <= 0f)
        {
            isDead = true;
            SetCollidersEnabled(false);
            RpcOnKilled();
            StartCoroutine(DestroyAfterDelay());
        }
    }

    [ClientRpc]
    private void RpcShowEffects(Vector3 _hitPoint, Vector3 _hitNormal)
    {
        if (hitAnimsContainer != null)
        {
            foreach (DOTweenAnimation anim in hitAnimsContainer.GetComponents<DOTweenAnimation>())
            {
                if (anim != null) anim.DORestart();
            }
        }

        if (hitParticles == null)
            Debug.LogWarning($"[SoundBox] hitParticles on '{name}' is unassigned in the Inspector.");
        else
            SpawnOneShotParticles(_hitPoint, _hitNormal);

        if (hitSoundEmitter != null)
            hitSoundEmitter.PlayOneShot();
    }

    /// <summary>Killing blow: punches pass through from now on, the music stops.</summary>
    [ClientRpc]
    private void RpcOnKilled()
    {
        SetCollidersEnabled(false);

        // This box's music goes silent with it.
        foreach (StudioEventEmitter emitter in GetComponentsInChildren<StudioEventEmitter>())
            emitter.Stop();
    }

    [Server]
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, destroyDelay));

        if (SoundBoxSpawner.Instance != null)
            SoundBoxSpawner.Instance.NotifySoundBoxDestroyed(this);

        OnDestroyedServer?.Invoke(this);

        // Rpcs are sent before the destroy message, so all clients still get the feedback.
        RpcOnDestroyed();
        NetworkServer.Destroy(gameObject);
    }

    /// <summary>
    /// Destruction feedback on all clients. Previously the destroy sound only ran on the server
    /// (= only the host heard it).
    /// </summary>
    [ClientRpc]
    private void RpcOnDestroyed()
    {
        if (destroySoundEmitter != null)
            destroySoundEmitter.PlayOneShot(); // one-shots keep playing after the box is gone
        else
            Debug.LogWarning($"[SoundBox] destroySoundEmitter on '{name}' is not assigned.");

        if (hitParticles != null)
            SpawnOneShotParticles(transform.position + Vector3.up * 0.5f, Vector3.up);

        CameraEffects.ShakeAt(transform.position, GameFeelSettings.Instance.soundBoxDestroyedTrauma);
    }

    private void SetCollidersEnabled(bool value)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = value;
    }

    private void SpawnOneShotParticles(Vector3 position, Vector3 normal)
    {
        Quaternion rotation = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        ParticleSystem ps = Instantiate(hitParticles, position, rotation);

        // The particle prefab has no "Destroy" stop action – without this every instance would
        // pile up in the scene over the course of the round.
        ParticleSystem.MainModule main = ps.main;
        Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
    }
}
