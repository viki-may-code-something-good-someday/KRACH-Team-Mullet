using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Wall_Data : MonoBehaviour
{
    [SerializeField] private GameObject wallNormal;
    [SerializeField] private GameObject wallBroken;
    [SerializeField] private float health;
    [SerializeField] private float explosionForce;
    [SerializeField] private float explosionRadius;

    public float Health { get { return health; } }

    private void Update()
    {
        if (health <= 0f)
        {
            TakeDamage(0f, transform.position, transform.forward);

            health = 100f;
        }
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
        wallNormal.SetActive(false);
        wallBroken.SetActive(true);

        List<Rigidbody> rigidbodies = wallBroken.transform.GetComponentsInChildren<Rigidbody>().ToList();

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddExplosionForce(explosionForce, _hitPoint, explosionRadius, 1f, ForceMode.Impulse);

        }
    }
}
