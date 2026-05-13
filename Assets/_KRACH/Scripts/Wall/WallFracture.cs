using UnityEngine;

public class WallFracture : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Settings")]
    [SerializeField] private float upwardsExplosionStrength = 0.5f;
    [SerializeField] private float explosionForce = 8f;
    [SerializeField] private float explosionRadius = 0.1f;

    private void OnEnable()
    {
        // add pool
        Destroy(gameObject, 5f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // this is so that the player can easily walk through the wall pieces after the wall destruction
        if (collision.gameObject.CompareTag("Player"))
        {
            // add pool
            Vector3 explosionDirection = (transform.position - collision.transform.position).normalized;
            Vector3 adjustedDirection = new Vector3(explosionDirection.x, upwardsExplosionStrength, explosionDirection.z);

            rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
        }
    }
}
