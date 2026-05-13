using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMODUnity;

public class WallData : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject wallNormal;
    [SerializeField] private GameObject wallBroken;
    [SerializeField] private List<GameObject> wallDecorations = new List<GameObject>();
    [SerializeField] private List<Rigidbody> wallChunks;


    [Header("Settings")]
    [SerializeField] private bool indestructable;
    [SerializeField] private float health;
    [SerializeField] private float explosionForce;
    [SerializeField] private float explosionRadius;

    [Header("Wall Decorations")]
    [SerializeField] private float wallDecorationsFadeOutSpeedMultiplier = 0.2f;
    [SerializeField] private float speedUpWallDecorationsFadeOutSpeedMultiplier = 0.5f;


    public float Health { get { return health; } }

    private bool fadeOutSpeedIncreased;
    private bool isDestroyed = false;
    private bool fadeOutPieces;




    private void Update()
    {
        CheckWallDestruction();
    }

    private void CheckWallDestruction()
    {
        if (health <= 0f && !isDestroyed)
        {
            TakeDamage(0f, transform.position, transform.forward);

            health = 100f;
        }

        if (fadeOutPieces)
        {
            FadeOutWallDecorations();
        }
    }

    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        if (indestructable || isDestroyed) return;

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

        //handle Wall Decorations
        WallDecorationsSetup();

        //wall fractures
        wallNormal.SetActive(false);  //needs rework
        wallBroken.SetActive(true);   //needs rework

        List<Rigidbody> rigidbodies = wallBroken.transform.GetComponentsInChildren<Rigidbody>().ToList(); //needs rework

        foreach (Rigidbody rb in rigidbodies) //needs rework
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddExplosionForce(explosionForce, _hitPoint, explosionRadius, 1f, ForceMode.Impulse);

        }

    }

    private void WallDecorationsSetup() //needs rework
    {
        fadeOutPieces = true;

        foreach (GameObject piece in wallDecorations)
        {
            piece.transform.parent = null;

            piece.AddComponent<Rigidbody>();

            SphereCollider sc = piece.AddComponent<SphereCollider>();
            sc.radius = 0.1f;

            piece.AddComponent<BillboardObject>();
        }
    }

    private void FadeOutWallDecorations()
    {
        for (int i = wallDecorations.Count - 1; i >= 0; i--)
        {
            SpriteRenderer sr = wallDecorations[i].GetComponent<SpriteRenderer>();

            if (sr == null)
            {
                Destroy(wallDecorations[i]);
                wallDecorations.RemoveAt(i);
                continue;
            }

            Color currentColor = sr.material.color;
            float newAlpha = currentColor.a - Time.deltaTime * wallDecorationsFadeOutSpeedMultiplier;
            sr.material.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);


            if (newAlpha <= 0f)
            {
                Destroy(wallDecorations[i]);
                wallDecorations.RemoveAt(i);
            }
            else if (newAlpha <= 80f && !fadeOutSpeedIncreased)
            {
                fadeOutSpeedIncreased = true;
                wallDecorationsFadeOutSpeedMultiplier = speedUpWallDecorationsFadeOutSpeedMultiplier;
            }
        }
    }
}
