using DG.Tweening;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using Sirenix.OdinInspector;
using UnityEditor.SceneManagement;
#endif

public class Wall : NetworkBehaviour, IDestructable
{
    [Header("References")]
    [SerializeField] private GameObject wallNormal;
    [SerializeField] private GameObject wallBroken;
    [SerializeField] private List<GameObject> wallDecorations = new List<GameObject>();
    [SerializeField] private ParticleSystem hitParticle;
    [SerializeField] private LuaSoundEmitter wallHitSoundEmitter;
    [SerializeField] private LuaSoundEmitter wallBreakdownSoundEmitter;
    [Tooltip("Sound for indestructible ('metal') walls — played instead of taking damage when 'indestructable' is on.")]
    [SerializeField] private LuaSoundEmitter wallMetalSoundEmitter;

    [Header("Settings")]
    [SerializeField] private bool indestructable;
    // Only the server needs to know the health, it doesn't have to be synchronized
    [SerializeField] private float health;
    [SerializeField] private float explosionForce;
    [SerializeField] private float explosionRadius;

    [Header("Wall Decorations")]
    [SerializeField] private float wallDecorationsFadeOutSpeedMultiplier = 0.2f;
    [SerializeField] private float speedUpWallDecorationsFadeOutSpeedMultiplier = 0.5f;
    [Tooltip("Below this alpha value (0–1) the faster fade-out speed is used.")]
    [Range(0f, 1f)]
    [SerializeField] private float speedUpBelowAlpha = 0.5f;

    [Header("Doors")]
    [SerializeField] private Door doorPrefab;
    [SerializeField] private List<Door> spawnedDoors = new List<Door>();

    public float Health => health;

    public ParticleSystem HitParticles => hitParticle;

#if UNITY_EDITOR
    private enum Axis { X, Y, Z }
    [SerializeField] private Axis thicknessAxis = Axis.Z;
    [SerializeField] private float doorSurfaceGap = 0.05f;
#endif

    private bool fadeOutSpeedIncreased;
    private bool fadeOutPieces;
    private bool brokenVisualsApplied;

    private float maxHealth;
    private Renderer[] normalRenderers;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // SyncVar: when the server sets this to true, ALL players (including those who join later)
    // know this wall is broken. This automatically triggers "OnWallDestroyed" on every machine.
    [SyncVar(hook = nameof(OnWallDestroyed))]
    private bool isDestroyed = false;

    private void Awake()
    {
        maxHealth = Mathf.Max(health, 0.0001f);
    }

    private void Update()
    {
        // The visual fade-out logic simply runs locally on every machine
        if (fadeOutPieces)
        {
            FadeOutWallDecorations();
        }
    }

    [Server]
    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        if (indestructable)
        {
            // Indestructible ("metal") wall: feedback sound only, no damage.
            RpcPlayMetalSound();
            return;
        }

        if (isDestroyed) return; // can happen with fast punches in the same frame – not an error

        health -= _damage;

        // Play sound/effects on all clients
        RpcShowEffects(_hitPoint, _hitNormal, Mathf.Clamp01(health / maxHealth));
        RpcPlayHitSound(_hitPoint);

        if (health <= 0f)
        {
            // 1. Mark as destroyed (late joiners get the state via the SyncVar)
            isDestroyed = true;

            // 2. Send the RPC for the physical explosion to all active players
            RpcTriggerExplosion(_hitPoint);
        }
    }

    [ClientRpc]
    private void RpcShowEffects(Vector3 _hitPoint, Vector3 _hitNormal, float healthRatio)
    {
        if (HitParticles == null)
        {
            Debug.LogWarning($"[Wall] hitParticle is not assigned on '{name}'.");
        }
        else
        {
            SpawnOneShotParticles(HitParticles, _hitPoint, _hitNormal);
        }

        PlayHitFeedback(healthRatio);
    }

    [ClientRpc]
    private void RpcPlayHitSound(Vector3 point)
    {
        if (wallHitSoundEmitter != null) wallHitSoundEmitter.PlayOneShot();
    }

    [ClientRpc]
    private void RpcPlayMetalSound()
    {
        if (wallMetalSoundEmitter != null)
            wallMetalSoundEmitter.PlayOneShot();

        PlayHitFeedback(1f, 0.4f);
    }

    private void OnWallDestroyed(bool oldState, bool newState)
    {
        if (newState && !oldState) ApplyBrokenVisuals();
    }

    // ClientRpc: the actual physical explosion.
    // Runs locally on every machine, saves a lot of network bandwidth.
    [ClientRpc]
    private void RpcTriggerExplosion(Vector3 _hitPoint)
    {
        // On clients this Rpc arrives BEFORE the isDestroyed SyncVar (Rpcs are sent immediately,
        // SyncVars only on the next sync). Without this call wallBroken would still be inactive
        // here, GetComponentsInChildren would find no rigidbodies, and the wall would simply
        // vanish without an explosion on every client except the host.
        ApplyBrokenVisuals();

        if (wallBreakdownSoundEmitter != null) wallBreakdownSoundEmitter.PlayOneShot();

        if (wallBroken != null)
        {
            foreach (Rigidbody rb in wallBroken.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = false;
                rb.useGravity = true;

                // The explosion is calculated locally on every machine
                rb.AddExplosionForce(explosionForce, _hitPoint, explosionRadius, 1f, ForceMode.Impulse);
            }
        }

        CameraEffects.ShakeAt(_hitPoint, GameFeelSettings.Instance.wallBreakTrauma);
    }

    /// <summary>Mesh swap for the destroyed state. Idempotent (both the Rpc and the SyncVar hook call it).</summary>
    private void ApplyBrokenVisuals()
    {
        if (brokenVisualsApplied) return;
        brokenVisualsApplied = true;

        if (wallNormal != null)
        {
            wallNormal.transform.DOKill(true);
            wallNormal.SetActive(false);
        }
        if (wallBroken != null) wallBroken.SetActive(true);

        WallDecorationsSetup();

        // Doors are children of this Wall, not of wallNormal, so they are NOT
        // deactivated by the line above. A destroyed wall has a hole, not a doorway,
        // so we explicitly disable them here — runs on every client.
        foreach (Door door in spawnedDoors)
        {
            if (door != null) door.DeactivateDoor();
        }
    }

    // ── Hit Feedback (local) ─────────────────────────────────────────────────

    private void PlayHitFeedback(float healthRatio, float strength = 1f)
    {
        if (wallNormal == null || !wallNormal.activeInHierarchy) return;

        GameFeelSettings settings = GameFeelSettings.Instance;

        if (settings.wallHitShake)
        {
            // Complete the previous shake first, otherwise the new one starts from the displaced
            // position and the wall wanders off a little with every hit.
            Transform t = wallNormal.transform;
            t.DOKill(true);
            t.DOShakePosition(0.18f, 0.035f * strength, 25, 90f, false, true).SetLink(wallNormal);
        }

        if (settings.wallDamageTint) ApplyDamageTint(healthRatio, settings.wallMaxDarken);
    }

    private void ApplyDamageTint(float healthRatio, float maxDarken)
    {
        if (normalRenderers == null)
        {
            normalRenderers = wallNormal.GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
        }

        float brightness = 1f - (1f - healthRatio) * maxDarken;

        foreach (Renderer r in normalRenderers)
        {
            if (r == null) continue;

            Material mat = r.sharedMaterial;
            if (mat == null) continue;

            int property = mat.HasProperty(BaseColorId) ? BaseColorId : mat.HasProperty(ColorId) ? ColorId : -1;
            if (property == -1) continue;

            Color baseColor = mat.GetColor(property);
            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(property, new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a));
            r.SetPropertyBlock(propertyBlock);
        }
    }

    private static void SpawnOneShotParticles(ParticleSystem prefab, Vector3 position, Vector3 normal)
    {
        Quaternion rotation = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        ParticleSystem ps = Instantiate(prefab, position, rotation);

        // The particle prefabs have no "Destroy" stop action – without this every instance
        // would stay in the scene for the rest of the round.
        ParticleSystem.MainModule main = ps.main;
        Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
    }

    // ── Wall Decorations ─────────────────────────────────────────────────────

    private void WallDecorationsSetup()
    {
        wallDecorations.RemoveAll(piece => piece == null);
        fadeOutPieces = wallDecorations.Count > 0;

        foreach (GameObject piece in wallDecorations)
        {
            piece.transform.parent = null;

            if (!piece.TryGetComponent(out Rigidbody _)) piece.AddComponent<Rigidbody>();

            SphereCollider sc = piece.AddComponent<SphereCollider>();
            sc.radius = 0.1f;

            // Purely local camera facing. BillboardObject used to be attached here – that is a
            // NetworkBehaviour without a NetworkIdentity and threw exceptions every frame.
            if (!piece.TryGetComponent(out CameraFacingSprite _)) piece.AddComponent<CameraFacingSprite>();
        }
    }

    private void FadeOutWallDecorations()
    {
        for (int i = wallDecorations.Count - 1; i >= 0; i--)
        {
            GameObject piece = wallDecorations[i];
            SpriteRenderer sr = piece != null ? piece.GetComponent<SpriteRenderer>() : null;

            if (sr == null)
            {
                if (piece != null) Destroy(piece);
                wallDecorations.RemoveAt(i);
                continue;
            }

            Color currentColor = sr.color;
            float newAlpha = currentColor.a - Time.deltaTime * wallDecorationsFadeOutSpeedMultiplier;
            sr.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);

            if (newAlpha <= 0f)
            {
                Destroy(piece);
                wallDecorations.RemoveAt(i);
            }
            // Alpha is between 0 and 1 – the old comparison against 80 triggered immediately.
            else if (newAlpha <= speedUpBelowAlpha && !fadeOutSpeedIncreased)
            {
                fadeOutSpeedIncreased = true;
                wallDecorationsFadeOutSpeedMultiplier = speedUpWallDecorationsFadeOutSpeedMultiplier;
            }
        }

        if (wallDecorations.Count == 0) fadeOutPieces = false;
    }


#if UNITY_EDITOR
    [Button("Generate Paired Doors")]
    private void GeneratePairedDoors()
    {
        if (doorPrefab == null)
        {
            Debug.LogError("[Wall] doorPrefab is not assigned.");
            return;
        }

        ClearDoors();

        if (!TryGetLocalBounds(out Bounds localBounds))
        {
            Debug.LogError("[Wall] Could not determine bounds (no BoxCollider or Renderer found).");
            return;
        }

        Vector3 axisDir = thicknessAxis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            _ => Vector3.forward
        };

        float halfExtent = Vector3.Scale(localBounds.extents, axisDir).magnitude;

        Door frontDoor = SpawnDoor(localBounds.center + axisDir * (halfExtent + doorSurfaceGap), axisDir);
        Door backDoor = SpawnDoor(localBounds.center - axisDir * (halfExtent + doorSurfaceGap), -axisDir);

        LinkDoors(frontDoor, backDoor);
        LinkDoors(backDoor, frontDoor);

        spawnedDoors.Add(frontDoor);
        spawnedDoors.Add(backDoor);

        // Without this, the change to the spawnedDoors list is never flagged as unsaved —
        // Unity won't write it to the scene/prefab even on explicit save, and it's lost
        // as soon as the scene is reloaded or reopened.
        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void LinkDoors(Door target, Door pair)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty("pairedDoor").objectReferenceValue = pair;
        so.ApplyModifiedProperties();
    }

    private Door SpawnDoor(Vector3 localPosition, Vector3 localOutwardDir)
    {
        Door door = (Door)PrefabUtility.InstantiatePrefab(doorPrefab, transform);
        Undo.RegisterCreatedObjectUndo(door.gameObject, "Generate Door");

        // Apply the door's own configurable Y offset (e.g. pivot correction)
        localPosition.y += door.YOffset;

        door.transform.localPosition = localPosition;
        door.transform.localRotation = Quaternion.LookRotation(localOutwardDir, Vector3.up);

        return door;
    }

    [Button("Clear Doors")]
    private void ClearDoors()
    {
        foreach (Door door in spawnedDoors)
        {
            if (door != null) Undo.DestroyObjectImmediate(door.gameObject);
        }
        spawnedDoors.Clear();

        foreach (Door child in GetComponentsInChildren<Door>(true).ToList())
        {
            Undo.DestroyObjectImmediate(child.gameObject);
        }

        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private bool TryGetLocalBounds(out Bounds localBounds)
    {
        BoxCollider box = GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            Vector3 worldCenter = box.transform.TransformPoint(box.center);
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            Vector3 localSize = Vector3.Scale(box.size, box.transform.lossyScale);
            localBounds = new Bounds(localCenter, localSize);
            return true;
        }

        Renderer rend = wallNormal != null ? wallNormal.GetComponentInChildren<Renderer>() : GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Vector3 localCenter = transform.InverseTransformPoint(rend.bounds.center);
            localBounds = new Bounds(localCenter, rend.bounds.size);
            return true;
        }

        localBounds = default;
        return false;
    }
#endif
}
