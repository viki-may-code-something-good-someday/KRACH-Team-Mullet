using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMODUnity;
using DG.Tweening;

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

    private bool isDestroyed;
    [Header("Wall Pieces")]
    [SerializeField] private float piecesWallFadeOutSpeedMultiplier;
    private bool fadeOutSpeedIncreased; 

    private bool fadeOutPieces;


    public float Health { get { return health; } }

    private void Start()
    {
        isDestroyed = false;
    }

    private void Update()
    {
        if (health <= 0f && !isDestroyed )
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
        
        RuntimeManager.PlayOneShot("event:/SFX/WallHit", _hitPoint);    // sound


        if (health <= 0f)
        {
            GetDestroyed(_hitPoint, _hitNormal);
        }
    }

    private void GetDestroyed(Vector3 _hitPoint, Vector3 _hitNormal)
    {
        isDestroyed = true;

        RuntimeManager.PlayOneShot("event:/SFX/WallBreakdown", _hitPoint);    // sound

        //handle Wall Pieces
        WallPiecesSetup();

        //wall fractures
        wallNormal.SetActive(false);
        wallBroken.SetActive(true);

        GameManager.Instance.WallWasDestroyed(this);

        List<Rigidbody> rigidbodies = wallBroken.transform.GetComponentsInChildren<Rigidbody>().ToList();

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddExplosionForce(explosionForce, _hitPoint, explosionRadius, 1f, ForceMode.Impulse);

        }

        if(RMF_Script.Instance != null)
        {
            if(RMF_Script.Instance.IsRMFHigh())
            {
                GameManager.Instance.GameOverBecauseWallDestroyedWithLowRMF(); // player lost because they destroyed a wall when RMF was low
            }
        }
    }

    private void WallPiecesSetup()
    {
        fadeOutPieces = true;

        foreach (GameObject piece in wallPieces)
        {
            piece.transform.parent = null;

            piece.AddComponent<Rigidbody>();

            SphereCollider sc = piece.AddComponent<SphereCollider>();
            sc.radius = 0.1f;
            sc.sharedMaterial = wallPiecesPhysicsMaterial;

            piece.AddComponent<BillboardFacingCamera>();
        }
    }

    private void FadeOutWallPieces()
    {
        for (int i = wallPieces.Count - 1; i >= 0; i--)
        {
            SpriteRenderer sr = wallPieces[i].GetComponent<SpriteRenderer>();

            if (sr == null)
            {
                Destroy(wallPieces[i]);
                wallPieces.RemoveAt(i);
                continue;
            }

            Color currentColor = sr.material.color;
            float newAlpha = currentColor.a - Time.deltaTime * piecesWallFadeOutSpeedMultiplier;
            sr.material.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);

            
            if (newAlpha <= 0f)
            {
                Destroy(wallPieces[i]);
                wallPieces.RemoveAt(i);
            }
            else if(newAlpha <= 80f && !fadeOutSpeedIncreased)
            {
                fadeOutSpeedIncreased = true;
                piecesWallFadeOutSpeedMultiplier *= 2f;
            }
        }
    }
}
