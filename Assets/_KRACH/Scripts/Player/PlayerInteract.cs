using DG.Tweening;
using Mirror;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vandalist action: punching + interacting with the world.
/// Activated/deactivated by PlayerRoleSetup via IRoleAction.
///
/// The owner predicts the hit and plays sound/animation/camera feedback immediately –
/// without network latency. The actual effect (interact / damage / billboard punch) is
/// decided exclusively by the server; the other clients get the effects via Rpc.
/// </summary>
public class PlayerInteract : NetworkBehaviour, IRoleAction
{
    [Header("References")]
    [SerializeField] private List<GameObject> armsVisuals = new List<GameObject>();
    [SerializeField] private Camera playerCamera;

    [Header("Interaction Settings")]
    [SerializeField] private float hitRange = 2f;
    [SerializeField] private float hitDamage = 1f;
    [Tooltip("Radius in which billboards can be punched even without a raycast hit.")]
    [SerializeField] private float pointBlankRadius = 1.5f;
    [Tooltip("Facing tolerance: 0 = 180° cone, 1 = exactly straight ahead. 0.3 ≈ 72°.")]
    [SerializeField] private float pointBlankMinDot = 0.3f;
    [Tooltip("Within this distance the hit counts regardless of the facing direction.")]
    [SerializeField] private float pointBlankContactRadius = 0.8f;
    [Tooltip("Layers that block a punch without triggering anything themselves (e.g. floor, furniture). " +
             "The NEAREST object on the ray is always the one that gets hit.")]
    [SerializeField] private LayerMask punchBlockers = (1 << 0) | (1 << 3);

    [Header("Vertical Look Rotation")]
    [Tooltip("Parent of both arms that gets pitched with the camera.")]
    [SerializeField] private Transform armRotationContainer;
    [Tooltip("How strongly the arms follow the camera pitch. 1 = 1:1, smaller = damped.")]
    [SerializeField] private float pitchRotationMultiplier = 1f;
    [Tooltip("Maximum up/down rotation of the arms in degrees, independent of the camera pitch.")]
    [SerializeField] private float maxArmPitchAngle = 60f;

    [Header("Anti-Cheat")]
    [Tooltip("Minimum time between two punches. Applies locally and is enforced on the server.")]
    [SerializeField] private float punchCooldown = 0.15f;
    [Tooltip("How far the raycast origin reported by the client may be from the player at most. " +
             "Must cover the camera height, otherwise legitimate punches get discarded.")]
    [SerializeField] private float maxOriginDistance = 2f;

    [Header("Sound")]
    [Tooltip("LuaSoundEmitter on the player, script mode. Enable Multiplayer = false.")]
    [SerializeField] private LuaSoundEmitter punchSoundEmitter;
    [SerializeField] private LuaSoundEmitter punchAirSoundEmitter;

    private enum PunchTargetKind { None, Interactable, Destructable, Billboard }

    private struct PunchTarget
    {
        public PunchTargetKind Kind;
        public RaycastHit Hit;
        public Interactable Interactable;
        public IDestructable Destructable;
        public BillboardObject Billboard;

        public bool IsValid => Kind != PunchTargetKind.None;
    }

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private Quaternion armContainerBaseLocalRotation = Quaternion.identity;
    private bool rightArmPunching;
    private float localNextAllowedPunchTime;
    private float serverNextAllowedPunchTime;

    private int interactableLayer;
    private int destructableLayer;
    private int billboardLayer;
    private int targetMask;

    private void Awake()
    {
        interactableLayer = LayerMask.NameToLayer("Interactable");
        destructableLayer = LayerMask.NameToLayer("Destructable");
        billboardLayer = LayerMask.NameToLayer("Billboard");
        targetMask = LayerMask.GetMask("Interactable", "Destructable", "Billboard") | punchBlockers.value;

        // Remember the container's starting rotation so the pitch is only applied as an offset
        // and doesn't overwrite the base pose.
        if (armRotationContainer != null)
            armContainerBaseLocalRotation = armRotationContainer.localRotation;
    }

    // ── IRoleAction ───────────────────────────────────────────────────────────

    public void OnRoleActivated() => enabled = true;
    public void OnRoleDeactivated() => enabled = false;

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!isOwned || InputManager.Instance == null) return;

        ApplyVerticalArmRotation();

        if (InputManager.Instance.CurrentInput.ActionPressed && Time.time >= localNextAllowedPunchTime)
            LocalPunch();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        int armsLayer = LayerMask.NameToLayer("Arms");
        if (armsLayer < 0)
        {
            Debug.LogError("[PlayerInteract] Layer 'Arms' does not exist – arms stay on their layer.");
            return;
        }

        foreach (GameObject arm in armsVisuals)
            SetLayerRecursively(arm, armsLayer);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;

        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    // ── Arms ─────────────────────────────────────────────────────────────────

    private void ApplyVerticalArmRotation()
    {
        if (playerCamera == null || armRotationContainer == null) return;

        float pitch = GetCameraPitch();

        pitch = Mathf.Clamp(pitch, -maxArmPitchAngle, maxArmPitchAngle) * pitchRotationMultiplier;

        armRotationContainer.localRotation = armContainerBaseLocalRotation * Quaternion.Euler(-pitch, 0f, 0f);
    }

    private float GetCameraPitch()
    {
        // Aim ray instead of camera forward: shake/recoil should not make the arms wobble along.
        Vector3 forward = CameraEffects.GetAimRay(playerCamera).direction;
        return Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg; // positive = looking up
    }

    // ── Punch ─────────────────────────────────────────────────────────────────

    private void LocalPunch()
    {
        if (playerCamera == null)
        {
            Debug.LogError("[PlayerInteract] playerCamera is not assigned.");
            return;
        }

        localNextAllowedPunchTime = Time.time + punchCooldown;

        Ray aim = CameraEffects.GetAimRay(playerCamera);
        bool predictedHit = FindPunchTarget(aim.origin, aim.direction).IsValid
                            || FindPointBlankBillboard(aim.direction) != null;

        // Immediate feedback for the puncher – everyone else gets it via Rpc. Previously the
        // owner only heard the punch after the server round trip.
        PunchAnimation();
        PlayPunchSound(predictedHit);
        PlayPunchCameraFeedback(predictedHit);

        CmdTryInteract(aim.origin, aim.direction);
    }

    private void PlayPunchSound(bool hitSomething)
    {
        LuaSoundEmitter emitter = hitSomething ? punchSoundEmitter : punchAirSoundEmitter;
        if (emitter != null) emitter.PlayOneShot();
    }

    private void PlayPunchCameraFeedback(bool hitSomething)
    {
        GameFeelSettings settings = GameFeelSettings.Instance;

        // Alternates slightly left/right – matching the punching arm.
        float yaw = rightArmPunching ? -0.4f : 0.4f;
        CameraEffects.KickLocal(settings.punchKickAngle * (hitSomething ? 1f : 0.5f), yaw);
        CameraEffects.Shake(hitSomething ? settings.punchHitTrauma : settings.punchMissTrauma);
    }

    /// <summary>
    /// Nearest punchable object on the ray. Runs identically on the client (prediction) and the
    /// server (authoritative). Triggers only count on the Billboard layer (their hit zone is a
    /// trigger); everything else ignores triggers – e.g. door triggers don't block a punch.
    /// A blocker (floor, furniture) or a collider without a matching component ends the search.
    /// </summary>
    private PunchTarget FindPunchTarget(Vector3 origin, Vector3 direction)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, hitRange, targetMask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hitBuffer, 0, count, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            Collider col = hit.collider;
            int layer = col.gameObject.layer;

            if (col.isTrigger && layer != billboardLayer) continue;
            if (col.transform.IsChildOf(transform)) continue; // own body

            PunchTarget target = new PunchTarget { Hit = hit };

            if (layer == interactableLayer && col.TryGetComponent(out Interactable interactable))
            {
                target.Kind = PunchTargetKind.Interactable;
                target.Interactable = interactable;
                return target;
            }

            if (layer == destructableLayer)
            {
                IDestructable destructable = col.GetComponentInParent<IDestructable>();
                if (destructable != null)
                {
                    target.Kind = PunchTargetKind.Destructable;
                    target.Destructable = destructable;
                }
                return target;
            }

            if (layer == billboardLayer)
            {
                BillboardObject billboard = col.GetComponent<BillboardObject>();
                if (billboard == null) continue; // skip pure trigger zones without a billboard

                target.Kind = PunchTargetKind.Billboard;
                target.Billboard = billboard;
                return target;
            }

            return target; // blocker
        }

        return default;
    }

    [Command]
    private void CmdTryInteract(Vector3 origin, Vector3 direction)
    {
        // ── Server-side validation (anti-cheat) ──
        // Origin and direction come from the client and can therefore be manipulated.

        // Slight tolerance for network jitter (packets don't arrive exactly on beat).
        if (Time.time < serverNextAllowedPunchTime) return;
        serverNextAllowedPunchTime = Time.time + punchCooldown * 0.8f;

        float originDistance = Vector3.Distance(origin, transform.position);
        if (originDistance > maxOriginDistance)
        {
            Debug.LogWarning($"[PlayerInteract] Raycast origin {originDistance:F1}m away from the player " +
                             $"(max: {maxOriginDistance}m) – punch discarded.");
            return;
        }

        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        bool hitSomething = true;
        PunchTarget target = FindPunchTarget(origin, direction);

        switch (target.Kind)
        {
            case PunchTargetKind.Interactable:
                target.Interactable.Interact();
                break;

            case PunchTargetKind.Destructable:
                target.Destructable.TakeDamage(hitDamage, target.Hit.point, target.Hit.normal);
                break;

            case PunchTargetKind.Billboard:
                target.Billboard.ServerTakePunch(transform.position);
                break;

            default:
                // Fallback when standing right inside the billboard's trigger.
                BillboardObject pointBlank = FindPointBlankBillboard(direction);
                if (pointBlank != null) pointBlank.ServerTakePunch(transform.position);
                else hitSomething = false;
                break;
        }

        RpcPlayPunchEffects(hitSomething);
    }

    /// <summary>
    /// Best billboard at point-blank range. Runs identically on the client (prediction) and
    /// the server (authoritative) so both come to the same result.
    /// </summary>
    private BillboardObject FindPointBlankBillboard(Vector3 forward)
    {
        Collider[] nearby = Physics.OverlapSphere(
            transform.position, pointBlankRadius, 1 << billboardLayer, QueryTriggerInteraction.Collide);

        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        bool haveForward = flatForward.sqrMagnitude > 0.0001f;
        if (haveForward) flatForward.Normalize();

        BillboardObject bestAligned = null;
        float bestFacing = pointBlankMinDot;

        foreach (Collider col in nearby)
        {
            BillboardObject billboard = col.GetComponentInParent<BillboardObject>();
            if (billboard == null || billboard.transform.IsChildOf(transform)) continue;

            Vector3 to = billboard.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist <= pointBlankContactRadius) return billboard; // right next to it → always hittable
            if (!haveForward) continue;

            float facing = Vector3.Dot(to / dist, flatForward);
            if (facing > bestFacing)
            {
                bestFacing = facing;
                bestAligned = billboard;
            }
        }

        return bestAligned;
    }

    [ClientRpc(includeOwner = false)]
    private void RpcPlayPunchEffects(bool hitSomething)
    {
        // Mirror also executes ClientRpcs on disabled components –
        // without this check there would be sounds/animations in the lobby.
        if (!enabled) return;

        PunchAnimation();
        PlayPunchSound(hitSomething);
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    private void PunchAnimation()
    {
        if (armsVisuals.Count != 2)
        {
            Debug.LogWarning("[PlayerInteract] PunchAnimation: expected 2 arms, found: " + armsVisuals.Count);
            return;
        }

        GameObject arm = armsVisuals[rightArmPunching ? 0 : 1];
        rightArmPunching = !rightArmPunching;
        if (arm == null) return;

        DOTweenAnimation[] anims = arm.GetComponents<DOTweenAnimation>();
        if (anims.Length == 0)
        {
            Debug.LogError("[PlayerInteract] DOTweenAnimation missing on: " + arm.name);
            return;
        }

        foreach (DOTweenAnimation anim in anims)
            anim.DORestart();
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
