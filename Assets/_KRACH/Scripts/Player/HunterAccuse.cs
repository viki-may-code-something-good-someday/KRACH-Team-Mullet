using Mirror;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Hunter action: accusing with a charge mechanic.
///
/// Hold the button → the arm pulls back, chargeTimer runs (even without a target in the crosshair).
/// After chargeTime the charge is full, but it only fires on release – releasing earlier
/// cancels. The server re-checks role, state, distance and cooldown; the client cooldown only
/// exists for responsiveness.
///
/// Subscribe to the catch event (server only): HunterAccuse.OnVandalistCaught += HandleCaught;
/// </summary>
public class HunterAccuse : NetworkBehaviour, IRoleAction
{
    [Header("Accuse Settings")]
    [SerializeField] private float accuseRange = 6f;
    [Tooltip("Layer the player colliders are on (e.g. 'Player').")]
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("Layers that block line of sight (walls, floor, furniture). Without this mask the " +
             "hunter could accuse vandalists through walls.")]
    [SerializeField] private LayerMask lineOfSightBlockers = (1 << 0) | (1 << 3) | (1 << 8) | (1 << 9);
    [Tooltip("Extra range the server tolerates (latency: the target may have moved a little " +
             "since the client raycast).")]
    [SerializeField] private float serverRangeTolerance = 1.5f;

    [Header("Cooldown")]
    [Tooltip("Minimum time between two accusations. Additionally enforced on the server.")]
    [SerializeField] private float accuseCooldown = 1.5f;

    [Header("Charge")]
    [Tooltip("How long the button must be held to trigger the accusation.")]
    [SerializeField] private float chargeTime = 1.0f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Arm GameObject with DOTweenAnimation components for the strike animation.")]
    [SerializeField] private GameObject armVisual;
    [Tooltip("Optional: crosshair that contracts while charging. Empty = looks for a 'Crosshair' " +
             "next to the accuseReadyIndicator.")]
    [SerializeField] private RectTransform chargeCrosshair;

    [Header("Vertical Look Rotation")]
    [Tooltip("Parent of the arm that gets pitched with the camera.")]
    [SerializeField] private Transform armRotationContainer;
    [Tooltip("How strongly the arm follows the camera pitch. 1 = 1:1, smaller = damped.")]
    [SerializeField] private float pitchRotationMultiplier = 1f;
    [Tooltip("Maximum up/down rotation of the arm in degrees, independent of the camera pitch.")]
    [SerializeField] private float maxArmPitchAngle = 60f;

    [Header("UI Feedback")]
    [Tooltip("Shown when a vandalist is in the crosshair and in range.")]
    [SerializeField] private GameObject accuseReadyIndicator;

    [Header("Accuse Animation")]
    [Tooltip("Local Z offset of the arm while charging (negative = pull back)." +
             " The duration always equals chargeTime, so the arm is fully back exactly" +
             " when the charge is full.")]
    [SerializeField] private float pullbackDistance = -0.15f;
    [Tooltip("How fast the arm returns after cancelling.")]
    [SerializeField] private float returnDuration = 0.2f;
    [SerializeField] private Ease pullbackEase = Ease.InQuad;
    [SerializeField] private Ease returnEase = Ease.OutQuad;
    [Tooltip("How long to wait after the accuse animation was triggered before the arm" +
             " automatically returns to its rest position.")]
    [SerializeField] private float accuseAnimationReturnDelay = 1f;

    /// <summary>Fires ONLY on the server as soon as a vandalist was accused successfully.</summary>
    public static event System.Action<NetworkIdentity> OnVandalistCaught;

    // ── State ─────────────────────────────────────────────────────────────────

    private NetworkIdentity currentTarget;
    private float localNextAllowedAccuseTime;   // owner client only
    private float serverNextAllowedAccuseTime;  // server only (anti-cheat)

    // Base rotation of the CONTAINER (not of the arm model). Previously the arm model's own
    // rotation was applied to the container, so the arm got its yaw offset twice.
    private Quaternion containerBaseLocalRotation = Quaternion.identity;

    private bool isCharging;
    private bool isFullyCharged;
    private float chargeTimer;
    private Tween pullbackTween;
    private Tween returnToRestTween; // delayed return after the accuse animation
    private Vector3 armRestPosition;

    private Vector3 crosshairBaseScale = Vector3.one;
    private bool crosshairResolved;

    private void Awake()
    {
        if (armRotationContainer != null)
            containerBaseLocalRotation = armRotationContainer.localRotation;
    }

    // ── IRoleAction ───────────────────────────────────────────────────────────

    public void OnRoleActivated()
    {
        enabled = true;
        if (armVisual != null)
            armRestPosition = armVisual.transform.localPosition;

        ResolveCrosshair();

        if (isOwned && accuseReadyIndicator == null)
            Debug.LogWarning("[HunterAccuse] accuseReadyIndicator is not assigned – " +
                             "the hunter gets no feedback whether a target is in the crosshair.");
    }

    public void OnRoleDeactivated()
    {
        enabled = false;
        returnToRestTween?.Kill();
        CancelCharge();
        ClearTarget();
        ApplyChargeFeedback(0f);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!isOwned) return;

        ApplyVerticalArmRotation();
        UpdateTarget();

        if (InputManager.Instance == null) return;

        if (InputManager.Instance.CurrentInput.ActionHeld)
        {
            if (!isCharging && !isFullyCharged)
            {
                if (Time.time >= localNextAllowedAccuseTime) StartCharge();
            }
            else if (!isFullyCharged)
            {
                chargeTimer += Time.deltaTime;
                if (chargeTimer >= chargeTime) OnFullyCharged();
            }
        }
        else if (isFullyCharged) ExecuteAccuse();
        else if (isCharging) CancelCharge();

        ApplyChargeFeedback(isCharging || isFullyCharged ? Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, chargeTime)) : 0f);
    }

    // ── Arm ──────────────────────────────────────────────────────────────────

    private void ApplyVerticalArmRotation()
    {
        if (playerCamera == null || armRotationContainer == null) return;

        float pitch = GetCameraPitch();

        pitch = Mathf.Clamp(pitch, -maxArmPitchAngle, maxArmPitchAngle) * pitchRotationMultiplier;

        armRotationContainer.localRotation = containerBaseLocalRotation * Quaternion.Euler(-pitch, 0f, 0f);
    }

    private float GetCameraPitch()
    {
        // Aim ray instead of camera forward: shake/recoil should not make the arm wobble along.
        Vector3 forward = CameraEffects.GetAimRay(playerCamera).direction;
        return Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg; // positive = looking up
    }

    // ── Charge ────────────────────────────────────────────────────────────────

    private void StartCharge()
    {
        returnToRestTween?.Kill();

        isCharging = true;
        chargeTimer = 0f;
        TweenArmTo(armRestPosition + Vector3.forward * pullbackDistance, chargeTime, pullbackEase);
    }

    private void OnFullyCharged()
    {
        isFullyCharged = true;
        chargeTimer = chargeTime;
        CameraEffects.Shake(GameFeelSettings.Instance.chargeFullTrauma);
    }

    private void CancelCharge()
    {
        if (!isCharging && !isFullyCharged) return;

        returnToRestTween?.Kill();
        ResetChargeState();
        TweenArmTo(armRestPosition, returnDuration, returnEase);
    }

    private void ExecuteAccuse()
    {
        ResetChargeState();

        if (armVisual != null)
        {
            pullbackTween?.Kill();
            returnToRestTween?.Kill();
            armVisual.transform.localPosition = armRestPosition; // reset before the execute animation
        }

        localNextAllowedAccuseTime = Time.time + accuseCooldown;

        GameFeelSettings settings = GameFeelSettings.Instance;
        CameraEffects.KickFovLocal(settings.accuseReleaseFovKick);
        CameraEffects.KickLocal(-settings.accuseKickAngle);

        AccuseAnimation(); // immediate local feedback without latency
        CmdTryAccuse(currentTarget);
    }

    private void ResetChargeState()
    {
        isCharging = false;
        isFullyCharged = false;
        chargeTimer = 0f;
    }

    private void TweenArmTo(Vector3 localTarget, float duration, Ease ease)
    {
        if (armVisual == null) return;

        pullbackTween?.Kill();
        pullbackTween = armVisual.transform.DOLocalMove(localTarget, duration).SetEase(ease).SetLink(armVisual);
    }

    // ── Charge Feedback (owner only) ─────────────────────────────────────────

    private void ResolveCrosshair()
    {
        if (crosshairResolved) return;
        crosshairResolved = true;

        if (chargeCrosshair == null && accuseReadyIndicator != null && accuseReadyIndicator.transform.parent != null)
            chargeCrosshair = accuseReadyIndicator.transform.parent.Find("Crosshair") as RectTransform;

        if (chargeCrosshair != null) crosshairBaseScale = chargeCrosshair.localScale;
    }

    /// <summary>Zoom while charging + the crosshair contracts and vibrates when full.</summary>
    private void ApplyChargeFeedback(float progress)
    {
        if (!isOwned) return;

        if (CameraEffects.Local != null)
        {
            float eased = 1f - (1f - progress) * (1f - progress);
            CameraEffects.Local.SustainedFovOffset = -GameFeelSettings.Instance.hunterChargeZoom * eased;
        }

        if (chargeCrosshair == null) return;

        float scale = Mathf.Lerp(1f, 0.65f, progress);
        if (isFullyCharged) scale += Mathf.Sin(Time.time * 40f) * 0.05f;
        chargeCrosshair.localScale = crosshairBaseScale * scale;
    }

    // ── Target Detection ─────────────────────────────────────────────────────

    private void UpdateTarget()
    {
        if (playerCamera == null) return;

        // Players AND line-of-sight blockers in a single query: whatever is in the way first
        // gets hit. Only if that is a player does it count as a target. Previously only the
        // player layer was checked – the hunter could catch vandalists through walls.
        Ray aim = CameraEffects.GetAimRay(playerCamera);
        int mask = playerLayer.value | lineOfSightBlockers.value;

        if (Physics.Raycast(aim, out RaycastHit hit, accuseRange, mask, QueryTriggerInteraction.Ignore)
            && (playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0
            && IsValidTarget(hit.collider.GetComponentInParent<PlayerObjectController>(),
                             hit.collider.GetComponentInParent<PlayerRoleSetup>()))
        {
            currentTarget = hit.collider.GetComponentInParent<NetworkIdentity>();
            SetIndicator(true);
            return;
        }

        ClearTarget();
    }

    private static bool IsValidTarget(PlayerObjectController poc, PlayerRoleSetup setup)
    {
        if (poc == null || poc.playerRole != PlayerRole.Vandalist) return false;
        return setup == null || (!setup.IsCaught && !setup.IsInvulnerable);
    }

    private void ClearTarget()
    {
        currentTarget = null;
        SetIndicator(false);
    }

    private void SetIndicator(bool active)
    {
        if (accuseReadyIndicator != null && accuseReadyIndicator.activeSelf != active)
            accuseReadyIndicator.SetActive(active);
    }

    // ── Mirror ────────────────────────────────────────────────────────────────

    [Command]
    private void CmdTryAccuse(NetworkIdentity target)
    {
        // Some tolerance for network jitter: two legitimate accusations don't always arrive
        // at the server exactly one cooldown apart.
        if (Time.time < serverNextAllowedAccuseTime)
        {
            Debug.LogWarning("[HunterAccuse] Accusation ignored during cooldown (possible client manipulation).");
            return;
        }
        serverNextAllowedAccuseTime = Time.time + accuseCooldown * 0.8f;

        RpcPlayAccuseAnimationOnOthers();

        if (target == null) return;

        PlayerObjectController targetPoc = target.GetComponent<PlayerObjectController>();
        if (targetPoc == null || targetPoc.playerRole != PlayerRole.Vandalist)
        {
            Debug.LogWarning("[HunterAccuse] Target is not a vandalist.");
            return;
        }

        PlayerRoleSetup targetSetup = target.GetComponent<PlayerRoleSetup>();
        if (targetSetup != null && (targetSetup.IsCaught || targetSetup.IsInvulnerable))
        {
            Debug.LogWarning("[HunterAccuse] Target is already caught or invulnerable.");
            return;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > accuseRange + serverRangeTolerance)
        {
            Debug.LogWarning($"[HunterAccuse] Target too far away: {dist:F1}m (max: {accuseRange}m).");
            return;
        }

        // Raised directly on the server instead of via a ClientRpc – this way the catch also
        // works on a dedicated server, without a detour through the clients.
        OnVandalistCaught?.Invoke(target);
        TargetCatchConfirmed(connectionToClient);
    }

    /// <summary>Hit confirmation for the accusing hunter only.</summary>
    [TargetRpc]
    private void TargetCatchConfirmed(NetworkConnectionToClient target)
    {
        GameFeelSettings settings = GameFeelSettings.Instance;
        CameraEffects.Shake(settings.catchSuccessTrauma);
        GameFeelHUD.CenterPop("CAUGHT!", settings.hunterColor);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcPlayAccuseAnimationOnOthers() => AccuseAnimation();

    // ── Animation ─────────────────────────────────────────────────────────────

    private void AccuseAnimation()
    {
        if (armVisual == null) return;

        DOTweenAnimation[] anims = armVisual.GetComponents<DOTweenAnimation>();
        if (anims.Length == 0)
        {
            Debug.LogError("[HunterAccuse] DOTweenAnimation missing on: " + armVisual.name);
            return;
        }

        foreach (DOTweenAnimation anim in anims)
            anim.DORestart();

        ScheduleReturnToRestAfterAccuse();
    }

    private void ScheduleReturnToRestAfterAccuse()
    {
        returnToRestTween?.Kill();

        // SetLink: without it the delayed call would still fire after the player object was
        // destroyed (e.g. leaving the session) and tween a destroyed transform.
        returnToRestTween = DOVirtual.DelayedCall(accuseAnimationReturnDelay, () =>
        {
            TweenArmTo(armRestPosition, returnDuration, returnEase);
        }).SetLink(gameObject);
    }
}
