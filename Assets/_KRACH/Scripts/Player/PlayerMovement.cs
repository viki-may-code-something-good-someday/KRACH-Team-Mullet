using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Player movement: walking, sprinting, jumping, crouching, footsteps, FOV.
/// Contains no role logic – PlayerRoleSetup passes in the matching RoleMovementConfig.
///
/// Walking: acceleration/deceleration via RoleMovementConfig (momentum, especially in the air).
/// Jump: asymmetric gravity (low while rising, high while falling). Releasing early cuts the
/// remaining velocity and switches to the strongest gravity stage → short hops.
/// Coyote time and jump buffering make jumps at ledges and just before landing more forgiving.
///
/// Prefab rig:
///   Player (CharacterController, NetworkTransform, PlayerMovement, ...)
///   ├── GroundCheck   ← must sit exactly at foot level (compensates pivot offsets)
///   └── CameraHolder
///       └── Camera
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private CharacterController controller;
    [Tooltip("Direct child of the player root, exactly at foot level. Compensates pivot offsets of the model.")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Camera playerCamera;
    [Tooltip("Empty transform that carries the camera. Moved for the crouch offset.")]
    [SerializeField] private Transform cameraHolder;
    [Tooltip("Capsule on the Player layer that acts as the hit zone (target of the hunter raycast)." +
             " Matched to the controller capsule on all clients. Empty = CapsuleCollider on the root.")]
    [SerializeField] private CapsuleCollider hitCollider;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundDistance = 0.4f;

    [Header("Audio")]
    [SerializeField] private float baseStepInterval = 0.3f;
    [Tooltip("LuaSoundEmitter at the feet, script mode. Enable Multiplayer = false.")]
    [SerializeField] private LuaSoundEmitter footstepSoundEmitter;
    [Tooltip("From this fall speed (m/s) on, landing plays a footstep sound.")]
    [SerializeField] private float landingSoundMinSpeed = 5f;

    // ── State ────────────────────────────────────────────────────────────────

    private RoleMovementConfig config;

    private Vector3 velocity;           // vertical
    private Vector3 horizontalVelocity; // set in HandleMovement, applied in Update
    private bool isGrounded;

    private bool sprinting;
    private float currentSprintSpeed;
    private float sprintTimer;

    private bool jumpButtonHeld;
    private bool isJumping;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;

    private bool isCrouching;
    private float footstepTimer;
    private float baseFov;

    /// <summary>Fires on the owner when landing. Parameter: impact speed in m/s.</summary>
    public event System.Action<float> Landed;

    public bool IsCrouching => isCrouching;
    public bool IsSprinting => sprinting;
    public bool IsGrounded => isGrounded;
    public Vector3 HorizontalVelocity => horizontalVelocity;
    public float WalkSpeed => config != null ? config.walkSpeed : 6f;

    /// <summary>Local Y position of the feet relative to the root pivot.</summary>
    private float FeetOffsetY => groundCheck != null ? groundCheck.localPosition.y : 0f;

    // Height of the body capsule. The owner reports every change (standing/crouching) via
    // Command so the hit zone matches the actual state on all clients.
    [SyncVar(hook = nameof(OnBodyHeightChanged))]
    private float syncedBodyHeight = 2f;

    // ── Init ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (hitCollider == null) hitCollider = GetComponent<CapsuleCollider>();
        if (controller == null) controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        controller.enabled = false; // locked until ApplyConfig is called

        // Remote players get their capsule dimensions exclusively via the SyncVar.
        if (!isOwned) ApplyHitColliderHeight(syncedBodyHeight);
    }

    /// <summary>
    /// Called by PlayerRoleSetup as soon as the role is known. Only on the owner client.
    /// </summary>
    public void ApplyConfig(RoleMovementConfig roleConfig)
    {
        config = roleConfig;
        currentSprintSpeed = config.baseSprintSpeed;
        isCrouching = false;
        isJumping = false;
        velocity = Vector3.zero;
        horizontalVelocity = Vector3.zero;

        SetBodyHeight(config.standHeight);

        if (cameraHolder != null)
        {
            Vector3 pos = cameraHolder.localPosition;
            pos.y = config.standCameraY;
            cameraHolder.localPosition = pos;
        }

        baseFov = config.normalFOV;
        ApplyFov();

        controller.enabled = true;

        WarnIfCameraLeavesCapsule();
    }

    /// <summary>
    /// Camera heights are given relative to the root pivot, capsule heights relative to the
    /// feet. If a camera sits above its capsule, you can see through low ceilings – which
    /// easily goes unnoticed while crouching.
    /// </summary>
    private void WarnIfCameraLeavesCapsule()
    {
        CheckEyeHeight("Crouch", config.crouchCameraY, config.crouchHeight);
        CheckEyeHeight("Stand", config.standCameraY, config.standHeight);
    }

    private void CheckEyeHeight(string label, float cameraY, float capsuleHeight)
    {
        float eyeAboveFeet = cameraY - FeetOffsetY;
        if (eyeAboveFeet <= capsuleHeight) return;

        Debug.LogWarning($"[PlayerMovement] {label}: camera sits {eyeAboveFeet:F2}m above the feet, " +
                         $"but the capsule is only {capsuleHeight:F2}m tall – you can see through ceilings. " +
                         $"{label}CameraY must be smaller than {capsuleHeight + FeetOffsetY:F2}.");
    }

    /// <summary>
    /// Sets the controller and hit capsule so the bottom always sits at groundCheck height –
    /// regardless of where the model's pivot is. Only the owner calls this.
    /// </summary>
    private void SetBodyHeight(float height)
    {
        controller.height = height;
        controller.center = CapsuleCenter(height);

        ApplyHitColliderHeight(height);

        if (isOwned && NetworkClient.ready) CmdSetBodyHeight(height);
    }

    /// <summary>Matches the hit zone to the body height. Runs on all clients.</summary>
    private void ApplyHitColliderHeight(float height)
    {
        if (hitCollider == null || height <= 0f) return;

        hitCollider.height = height;
        hitCollider.center = CapsuleCenter(height);
    }

    private Vector3 CapsuleCenter(float height) => new Vector3(0f, FeetOffsetY + height / 2f, 0f);

    [Command]
    private void CmdSetBodyHeight(float height) => syncedBodyHeight = Mathf.Clamp(height, 0.5f, 3f);

    private void OnBodyHeightChanged(float oldHeight, float newHeight)
    {
        // The owner already set its own capsule locally.
        if (!isOwned) ApplyHitColliderHeight(newHeight);
    }

    /// <summary>
    /// Teleports the player. Called by PlayerRoleSetup on spawn/respawn
    /// (only meaningful for the owner, since movement is client-authoritative).
    /// The controller's enabled state is preserved – a teleport must not make a frozen
    /// player (caught state) movable again prematurely.
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        bool wasEnabled = controller.enabled;

        controller.enabled = false;
        transform.position = position;
        velocity = Vector3.zero;
        horizontalVelocity = Vector3.zero;
        controller.enabled = wasEnabled;
    }

    // ── Update ───────────────────────────────────────────────────────────────

    void Update()
    {
        // Disabled controller = frozen (FreezeMovement). Without this check the entire
        // movement logic would keep running: crouching, gravity build-up and Move calls on
        // a disabled CharacterController.
        if (config == null || !isOwned || !controller.enabled) return;

        UpdateGrounded();
        HandleMovement();
        HandleJump();
        HandleCrouch();
        HandleFOV();

        // A single Move per frame: horizontal + vertical combined. Two separate calls cost
        // an extra capsule sweep and behave differently at edges.
        CollisionFlags flags = controller.Move((horizontalVelocity + velocity) * Time.deltaTime);
        ResolveCollisions(flags);
    }

    /// <summary>
    /// Corrects the velocity after collisions: hitting a ceiling ends the ascent (otherwise
    /// you "stick" up there until gravity has eaten the remaining velocity), and against a
    /// wall the blocked part is discarded (otherwise players with momentum briefly stick to
    /// walls when turning away).
    /// </summary>
    private void ResolveCollisions(CollisionFlags flags)
    {
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
        {
            velocity.y = 0f;
            jumpButtonHeld = false;
        }

        if ((flags & CollisionFlags.Sides) != 0)
        {
            Vector3 actual = controller.velocity;
            actual.y = 0f;
            if (actual.sqrMagnitude < horizontalVelocity.sqrMagnitude)
                horizontalVelocity = actual;
        }
    }

    // ── Ground ───────────────────────────────────────────────────────────────

    private void UpdateGrounded()
    {
        bool wasGrounded = isGrounded;
        isGrounded = groundCheck != null &&
                     Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            lastGroundedTime = Time.time;

            if (!wasGrounded && velocity.y <= 0f)
                OnLanded(-velocity.y);

            if (velocity.y < 0f) velocity.y = -2f;
            if (velocity.y <= 0f) isJumping = false;
        }
        else
        {
            ApplyAirGravity();
        }
    }

    private void OnLanded(float impactSpeed)
    {
        // Ignore tiny "landings" (stair steps, ground check flickering at edges).
        if (impactSpeed < 3f) return;

        Landed?.Invoke(impactSpeed);

        if (impactSpeed >= landingSoundMinSpeed)
        {
            footstepTimer = 0f;
            PlayFootstep();
        }
    }

    // ── Movement ─────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        Vector2 moveInput = InputManager.Instance != null
            ? InputManager.Instance.CurrentInput.MoveInput
            : Vector2.zero;
        bool isMoving = moveInput != Vector2.zero;

        bool sprintInput = InputManager.Instance != null
            && InputManager.Instance.CurrentInput.SprintHeld;
        sprinting = sprintInput && config.canSprint && !isCrouching;

        UpdateSprintSpeed(isMoving);

        float speed = isCrouching
            ? config.walkSpeed * config.crouchSpeedMultiplier
            : (sprinting && isMoving ? currentSprintSpeed : config.walkSpeed);

        HandleFootsteps(isMoving, speed);

        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (direction.sqrMagnitude > 1f) direction.Normalize();
        Vector3 targetVelocity = direction * speed;

        float rate = isGrounded
            ? (isMoving ? config.groundAcceleration : config.groundDeceleration)
            : (isMoving ? config.airAcceleration : config.airDeceleration);

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);
    }

    /// <summary>Sprint accelerates slowly and gets a burst after sprintBurstThreshold.</summary>
    private void UpdateSprintSpeed(bool isMoving)
    {
        if (sprinting && isMoving)
        {
            sprintTimer += Time.deltaTime;
            float accel = sprintTimer < config.sprintBurstThreshold
                ? config.sprintAcceleration
                : config.sprintBurstAcceleration;
            currentSprintSpeed = Mathf.Min(currentSprintSpeed + accel * Time.deltaTime, config.maxSprintSpeed);
        }
        else
        {
            sprintTimer = 0f;
            currentSprintSpeed = Mathf.Max(
                currentSprintSpeed - config.sprintDecaySpeed * Time.deltaTime,
                config.baseSprintSpeed);
        }
    }

    /// <summary>
    /// Asymmetric gravity: low while rising (matching the sqrt(2gh) formula in HandleJump),
    /// stronger while falling, strongest after releasing the jump button early.
    /// </summary>
    private void ApplyAirGravity()
    {
        float gravityMultiplier;

        if (velocity.y > 0f)
        {
            gravityMultiplier = jumpButtonHeld
                ? config.riseGravityMultiplier
                : config.jumpCutGravityMultiplier;
        }
        else
        {
            gravityMultiplier = config.fallGravityMultiplier;
        }

        velocity.y -= config.baseFallGravity * gravityMultiplier * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -config.maxFallSpeed);
    }

    // ── Jumping ──────────────────────────────────────────────────────────────

    private void HandleJump()
    {
        if (InputManager.Instance == null) return;

        InputData input = InputManager.Instance.CurrentInput;

        if (input.JumpPressed) lastJumpPressedTime = Time.time;

        // Jump buffer: a jump pressed shortly before landing still counts.
        // Coyote time: shortly after leaving a ledge you can still jump.
        bool jumpRequested = Time.time - lastJumpPressedTime <= Mathf.Max(0f, config.jumpBufferTime);
        bool groundAvailable = isGrounded || Time.time - lastGroundedTime <= Mathf.Max(0f, config.coyoteTime);

        if (jumpRequested && groundAvailable && !isJumping && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(config.jumpHeight * 2f * config.baseFallGravity * config.riseGravityMultiplier);
            jumpButtonHeld = input.JumpHeld || input.JumpPressed;
            isJumping = true;

            // Consume both, so neither buffer nor coyote time can trigger a second jump.
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            return; // the cut must not apply in the start frame (JumpHeld may still be false)
        }

        // Releasing early during the ascent: cut the velocity and switch to the strongest
        // gravity stage (see ApplyAirGravity).
        if (jumpButtonHeld && !input.JumpHeld && velocity.y > 0f)
        {
            jumpButtonHeld = false;
            velocity.y *= config.jumpCutVelocityMultiplier;
        }
        else if (velocity.y <= 0f)
        {
            jumpButtonHeld = false;
        }
    }

    // ── Crouching ────────────────────────────────────────────────────────────

    private void HandleCrouch()
    {
        if (InputManager.Instance == null) return;

        bool crouchInput = InputManager.Instance.CurrentInput.CrouchHeld;

        if (crouchInput && !isCrouching) SetCrouching(true);
        else if (!crouchInput && isCrouching && CanStandUp()) SetCrouching(false);

        if (cameraHolder != null)
        {
            float targetY = isCrouching ? config.crouchCameraY : config.standCameraY;
            Vector3 pos = cameraHolder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-config.crouchTransitionSpeed * Time.deltaTime));
            cameraHolder.localPosition = pos;
        }
    }

    private void SetCrouching(bool crouch)
    {
        isCrouching = crouch;
        SetBodyHeight(crouch ? config.crouchHeight : config.standHeight);
    }

    private bool CanStandUp()
    {
        // Check sphere at the top of the standing capsule – relative to the feet, not the pivot.
        Vector3 top = transform.position
            + Vector3.up * (FeetOffsetY + config.standHeight - controller.radius);
        return !Physics.CheckSphere(top, controller.radius, groundMask, QueryTriggerInteraction.Ignore);
    }

    // ── FOV ──────────────────────────────────────────────────────────────────

    private void HandleFOV()
    {
        float targetFOV = sprinting && sprintTimer >= config.sprintBurstThreshold
            ? config.sprintFOV
            : config.normalFOV;

        baseFov = Mathf.Lerp(baseFov, targetFOV, 1f - Mathf.Exp(-config.fovChangeSpeed * Time.deltaTime));
        ApplyFov();
    }

    /// <summary>Base FOV (sprint) plus short-lived kicks/zooms from CameraEffects.</summary>
    private void ApplyFov()
    {
        if (playerCamera == null) return;

        float offset = CameraEffects.Local != null ? CameraEffects.Local.FovOffset : 0f;
        playerCamera.fieldOfView = Mathf.Clamp(baseFov + offset, 20f, 120f);
    }

    // ── Footstep Audio ───────────────────────────────────────────────────────

    private void HandleFootsteps(bool isMoving, float currentSpeed)
    {
        if (!isGrounded || !isMoving)
        {
            footstepTimer = 0f;
            return;
        }

        // Reference speed at which baseStepInterval applies. Set much lower while sprinting
        // → noticeably faster step rate.
        float referenceSpeed = sprinting ? config.maxSprintSpeed * 0.25f : config.walkSpeed;
        float interval = baseStepInterval * referenceSpeed / Mathf.Max(0.0001f, currentSpeed);

        footstepTimer += Time.deltaTime;
        if (footstepTimer < interval) return;

        footstepTimer = 0f;
        PlayFootstep();
    }

    private void PlayFootstep()
    {
        if (footstepSoundEmitter != null) footstepSoundEmitter.PlayOneShot();
        CmdBroadcastFootstep();
    }

    // ── Debris Pushing ───────────────────────────────────────────────────────

    /// <summary>
    /// CharacterControllers don't trigger OnCollisionEnter on rigidbodies – wall debris would
    /// otherwise block the way like concrete. Loose, non-kinematic rigidbodies are kicked
    /// away in the walking direction here. Purely local: debris physics is per client anyway.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isOwned) return;

        GameFeelSettings settings = GameFeelSettings.Instance;
        if (!settings.pushDebris) return;

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return; // standing on it ≠ pushing it

        Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (pushDirection.sqrMagnitude < 0.0001f) return;
        pushDirection.Normalize();

        float speedFactor = Mathf.Clamp(horizontalVelocity.magnitude / Mathf.Max(0.1f, WalkSpeed), 0.3f, 1.6f);
        float targetSpeed = settings.debrisPushSpeed * speedFactor;

        // The callback fires every frame while walking against it: only push if the piece is
        // slower than desired – otherwise the velocity keeps building up.
        if (body.linearVelocity.sqrMagnitude >= targetSpeed * targetSpeed * 0.25f) return;

        body.AddForce(pushDirection * targetSpeed + Vector3.up * (targetSpeed * 0.3f), ForceMode.VelocityChange);
        body.AddTorque(Random.insideUnitSphere * targetSpeed, ForceMode.VelocityChange);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void FreezeMovement()
    {
        controller.enabled = false;
        velocity = Vector3.zero;
        horizontalVelocity = Vector3.zero;
        currentSprintSpeed = config != null ? config.baseSprintSpeed : 0f;
        sprinting = false;
        jumpButtonHeld = false;
        isJumping = false;

        if (config != null)
        {
            baseFov = config.normalFOV;
            ApplyFov();
        }
    }

    public void UnfreezeMovement() => controller.enabled = true;

    // ── Mirror – Footstep Sync ────────────────────────────────────────────────

    [Command]
    private void CmdBroadcastFootstep() => RpcPlayFootstepOnOthers();

    [ClientRpc(includeOwner = false)]
    private void RpcPlayFootstepOnOthers()
    {
        if (footstepSoundEmitter != null) footstepSoundEmitter.PlayOneShot();
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [Button("Debug: Jump State")]
    private void DebugJumpState()
    {
        Debug.Log($"[PlayerMovement] config={config != null}, isOwned={isOwned}, isGrounded={isGrounded}");
        Debug.Log($"[PlayerMovement] groundMask={groundMask.value} (0 = Nothing → jumping impossible!)");
        Debug.Log($"[PlayerMovement] groundCheck pos={groundCheck?.position}, groundDistance={groundDistance}");
        Debug.Log($"[PlayerMovement] velocity.y={velocity.y}, jumpButtonHeld={jumpButtonHeld}, isJumping={isJumping}");

        if (groundMask.value == 0)
            Debug.LogError("[PlayerMovement] groundMask is 'Nothing' – isGrounded will never be true! Set the layers in the Inspector.");
        if (groundCheck == null)
            Debug.LogError("[PlayerMovement] groundCheck is not assigned!");
        if (config == null)
            Debug.LogError("[PlayerMovement] config is null – ApplyConfig() has not been called yet.");
    }
#endif
}
