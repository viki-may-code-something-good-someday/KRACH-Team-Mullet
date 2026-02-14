using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Wall_Data : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject wallNormal;
    [SerializeField] private GameObject wallBroken;
    [SerializeField] private List<GameObject> wallPieces = new List<GameObject>();
    [SerializeField] private PhysicsMaterial wallPiecesPhysicsMaterial;

    [Header("Wall Data")]
    [SerializeField] private float health;
    [SerializeField] private float explosionForce;
    [SerializeField] private float explosionRadius;

    [Header("Wall Pieces")]
    [SerializeField] private float piecesWallFadeOutSpeedMultiplier;

    private bool fadeOutPieces;


    public float Health { get { return health; } }

    private void Update()
    {
        if (health <= 0f)
        {
            TakeDamage(0f, transform.position, transform.forward);

            health = 100f;
        }

        if (fadeOutPieces)
        {
            FadeOutWallPieces();
        }
    }

    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        health -= _damage;

        //wallPieces could shake?

        if (health <= 0f)
        {
            GetDestroyed(_hitPoint, _hitNormal);
        }
    }

    private void GetDestroyed(Vector3 _hitPoint, Vector3 _hitNormal)
    {
        //handle Wall Pieces
        WallPiecesSetup();

        //wall fractures
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

    private void WallPiecesSetup()
    {
        fadeOutPieces = true;

        foreach (GameObject piece in wallPieces)
        {
            piece.transform.parent = null;

            Rigidbody rb = piece.AddComponent<Rigidbody>();
            //rb.freezeRotation = true;
            SphereCollider sc = piece.AddComponent<SphereCollider>();
            sc.radius = 0.1f;
            sc.sharedMaterial = wallPiecesPhysicsMaterial;
            piece.AddComponent<BillboardFacingCamera>();
        }
    }

    private void FadeOutWallPieces()
    {
        foreach (GameObject piece in wallPieces)
        {
            Color currentColor = piece.GetComponent<SpriteRenderer>().material.color;
            float newAlpha = currentColor.a - Time.deltaTime * piecesWallFadeOutSpeedMultiplier;
            piece.GetComponent<SpriteRenderer>().material.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);

            if(newAlpha <= 80f)
            {
                piecesWallFadeOutSpeedMultiplier *= 2f;
            }
            else if (newAlpha <= 0f)
            {
                Destroy(piece);
            }
        }
    }
}
