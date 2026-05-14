using Mirror; // Mirror hinzugefügt
using UnityEngine;

public class BillboardObject : NetworkBehaviour
{
    [SerializeField] private bool flippedSprite;
    [SerializeField] private bool knockbackEnabled = true;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float punchKnockbackMultiplier = 3f;
    [SerializeField] private float knockbackDuration = 0.3f;
    [SerializeField] private ParticleSystem punchParticles;

    private Camera mainCamera;
    private Rigidbody rb;
    private bool isKnockedBack;

    private void Start()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (isKnockedBack || mainCamera == null) return;

        // Lokale Drehung für jeden Spieler bleibt erhalten
        Vector3 directionToCamera = mainCamera.transform.position - transform.position;
        directionToCamera.y = 0f;

        if (flippedSprite)
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera) * Quaternion.Euler(0, 180, 0);
        }
    }

    [ServerCallback] // Sorgt dafür, dass die Kollision nur 1x vom Server registriert wird
    private void OnCollisionEnter(Collision collision)
    {
        if (knockbackEnabled && collision.gameObject.CompareTag("Player"))
        {
            RpcApplyKnockback(collision.transform.position, knockbackForce);
        }
    }

    // Wird von Player_Interact (Server) ausgelöst
    [Server]
    public void TakePunch(Vector3 puncherPosition)
    {
        if (knockbackEnabled)
        {
            RpcApplyKnockback(puncherPosition, knockbackForce * punchKnockbackMultiplier);
        }
    }

    // Client Physik und VFX
    [ClientRpc]
    private void RpcApplyKnockback(Vector3 sourcePosition, float force)
    {
        if (punchParticles != null)
        {
            ParticleSystem particles = Instantiate(punchParticles, transform.position + new Vector3(0f, 0.6f, 0f), Quaternion.identity);
            particles.Play();
        }

        Vector3 knockbackDirection = (transform.position - sourcePosition).normalized;
        float randomMultiplier = Mathf.Max(Random.value, 0.5f) * 3f;

        Vector3 velocity = new Vector3(
            knockbackDirection.x,
            0.8f,
            knockbackDirection.z
        ) * force * randomMultiplier;

        rb.linearVelocity = velocity;

        isKnockedBack = true;
        CancelInvoke(nameof(ResetKnockback));
        Invoke(nameof(ResetKnockback), knockbackDuration);
    }

    private void ResetKnockback()
    {
        isKnockedBack = false;
    }
}