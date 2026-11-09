using System.Collections;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the role initialization when entering the gameplay scene as well as
/// catching/respawning vandalists (reacts to HunterAccuse.OnVandalistCaught).
/// </summary>
public class PlayerRoleSetup : NetworkBehaviour
{
    [Header("Movement Configs")]
    [SerializeField] private RoleMovementConfig hunterConfig;
    [SerializeField] private RoleMovementConfig vandalistConfig;

    [Header("References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerInteract interact;
    [SerializeField] private HunterAccuse accuse;

    [Tooltip("Parent of all visible player elements. Deactivated in the lobby AND in the caught state.")]
    [SerializeField] private GameObject playerVisuals;

    [Header("Role Models")]
    [SerializeField] private GameObject hunterModel;
    [SerializeField] private GameObject vandalistModel;
    [SerializeField] private GameObject hunterModelLocalOnlyShadow;
    [SerializeField] private GameObject vandalistModelLocalOnlyShadow;


    [Header("Respawn Settings")]
    [Tooltip("Whether caught vandalists respawn. If false, they stay caught permanently.")]
    [SerializeField] private bool allowRespawn = true;
    [SerializeField] private float respawnDelay = 5f;
    [Tooltip("Short invulnerability after respawning so the hunter can't catch again immediately.")]
    [SerializeField] private float respawnInvulnerability = 2f;
    [Tooltip("Catches until final elimination, even with allowRespawn. -1 = unlimited.")]
    [SerializeField] private int maxCatches = -1;
    [Tooltip("Optional visual (e.g. death indicator), only for the affected owner in the caught state.")]
    [SerializeField] private GameObject caughtStateVisual;

    [Tooltip("Only for ownerless objects (test dummies): layers that count as ground on respawn." +
             " Real players don't need this, their CharacterController falls by itself.")]
    [SerializeField] private LayerMask ownerlessGroundMask = 8;


    public static event System.Action<PlayerRoleSetup, bool> OnCaughtStateChangedServer;


    // ── State ────────────────────────────────────────────────────────────────

    // Must be declared BEFORE isCaught: Mirror deserializes SyncVars in declaration order,
    // so the value is already set when the isCaught hook shows the feedback.
    [SyncVar]
    private bool isEliminated;

    [SyncVar(hook = nameof(OnCaughtStateChanged))]
    private bool isCaught;
    public bool IsCaught => isCaught;

    // A SyncVar instead of a local timestamp: Time.time differs between server and clients,
    // and the hunter client needs to know the state for its crosshair.
    [SyncVar]
    private bool isInvulnerable;
    public bool IsInvulnerable => isInvulnerable;

    private int catchCount;

    private PlayerObjectController poc;
    private PlayerRole assignedRole;
    private bool initialized;
    private Coroutine initRoutine;
    private Collider[] bodyColliders;

    private string GameplayScene =>
        (NetworkManager.singleton as CustomNetworkManager)?.GameplayScene ?? string.Empty;

    private bool InGameplayScene => SceneManager.GetActiveScene().path == GameplayScene;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        poc = GetComponent<PlayerObjectController>();

        // Only collect colliders that are active in the prefab – deliberately disabled ones stay
        // disabled. The CharacterController is excluded: PlayerMovement manages it via
        // Freeze/Unfreeze, otherwise this state would have two owners.
        bodyColliders = System.Array.FindAll(
            GetComponentsInChildren<Collider>(true),
            col => col.enabled && !(col is CharacterController));
    }

    private void Start()
    {
        // Hide everything until the gameplay scene is loaded.
        // The root stays active – Mirror needs it for network sync.
        SetLobbyState();
        EvaluateScene();
    }

    private void OnEnable()
    {
        HunterAccuse.OnVandalistCaught += HandleVandalistCaught;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        if (poc != null) poc.RoleChanged += OnRoleSyncedFromServer;
    }

    private void OnDisable()
    {
        HunterAccuse.OnVandalistCaught -= HandleVandalistCaught;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (poc != null) poc.RoleChanged -= OnRoleSyncedFromServer;
    }

    private void OnActiveSceneChanged(Scene from, Scene to) => EvaluateScene();

    private void EvaluateScene()
    {
        if (InGameplayScene) BeginInitialization();
        else SetLobbyState();
    }

    /// <summary>
    /// The role arrives as a SyncVar and can overtake the scene change or lag behind it
    /// (lag, late join). If it arrives later than the initialization, it is caught up here –
    /// otherwise the player would stay stuck on the prefab's default value.
    /// </summary>
    private void OnRoleSyncedFromServer(PlayerRole newRole)
    {
        if (!initialized || newRole == assignedRole) return;
        if (!InGameplayScene) return;

        Debug.Log($"[PlayerRoleSetup] Role changed afterwards: {assignedRole} → {newRole}, re-running setup.");
        InitializeRole();
    }

    // ── States ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the initialization as soon as the gameplay scene's LevelManager is ready.
    /// The LevelManager is recreated on scene change, so it may not exist yet.
    /// </summary>
    private void BeginInitialization()
    {
        if (initialized || initRoutine != null) return;

        initRoutine = StartCoroutine(InitializeWhenLevelReady());
    }

    private IEnumerator InitializeWhenLevelReady()
    {
        while (LevelManager.Instance == null && InGameplayScene)
            yield return null;

        initRoutine = null;

        if (InGameplayScene) InitializeRole();
    }

    private void SetLobbyState()
    {
        initialized = false;

        if (initRoutine != null)
        {
            StopCoroutine(initRoutine);
            initRoutine = null;
        }

        if (playerVisuals != null) playerVisuals.SetActive(false);
        if (hunterModel != null) hunterModel.SetActive(false);
        if (vandalistModel != null) vandalistModel.SetActive(false);

        if (interact != null) interact.OnRoleDeactivated();
        if (accuse != null) accuse.OnRoleDeactivated();
    }

    private void InitializeRole()
    {
        initialized = true;

        if (poc == null)
        {
            Debug.LogError("[PlayerRoleSetup] No PlayerObjectController found!");
            return;
        }

        assignedRole = poc.playerRole;

        // Visuals and actions on all clients
        if (playerVisuals != null) playerVisuals.SetActive(true);
        ActivateRoleModel(assignedRole);
        ActivateRoleAction(assignedRole);

        // Config and spawn only for the local player (client-authoritative movement).
        // For ownerless objects (test dummies) TestDummySpawner takes care of the start position.
        if (!isOwned) return;

        // Hide own body mesh for the local camera, but keep the shadow.
        // Scoped to this instance only (isOwned is per-instance), so other players' models stay visible.
        ApplyLocalPlayerVisibility();

        RoleMovementConfig config = assignedRole == PlayerRole.Hunter ? hunterConfig : vandalistConfig;
        if (config == null || movement == null)
        {
            Debug.LogError($"[PlayerRoleSetup] Config or PlayerMovement missing for role {assignedRole}!");
            return;
        }

        movement.ApplyConfig(config);
        movement.TeleportTo(PickSpawnPosition(assignedRole));
    }

    private void ActivateRoleModel(PlayerRole role)
    {
        bool isHunter = role == PlayerRole.Hunter;
        if (hunterModel != null) hunterModel.SetActive(isHunter);
        if (vandalistModel != null) vandalistModel.SetActive(!isHunter);
    }

    /// <summary>
    /// Hides this player's own renderers from their own view while keeping the shadow visible.
    /// Only ever called on the active local player
    /// </summary>
    private void ApplyLocalPlayerVisibility()
    {
        GameObject shadowRoot = assignedRole switch
        {
            PlayerRole.Vandalist => vandalistModelLocalOnlyShadow,
            PlayerRole.Hunter => hunterModelLocalOnlyShadow,
            _ => null
        };

        if (shadowRoot == null)
        {
            Debug.LogWarning($"[PlayerRoleSetup] No LocalOnlyShadow object for role {assignedRole} – " +
                             "the own model stays visible to the local camera.");
            return;
        }

        foreach (Renderer r in shadowRoot.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }

    private void ActivateRoleAction(PlayerRole role)
    {
        SetInteractActive(role == PlayerRole.Vandalist);

        if (accuse != null)
        {
            if (role == PlayerRole.Hunter) accuse.OnRoleActivated();
            else accuse.OnRoleDeactivated();
        }
    }

    private void SetInteractActive(bool active)
    {
        if (interact == null) return;

        if (active) interact.OnRoleActivated();
        else interact.OnRoleDeactivated();
    }

    // ── Spawn / Respawn ──────────────────────────────────────────────────────

    private Vector3 PickSpawnPosition(PlayerRole role)
    {
        Transform[] spawnPoints = role == PlayerRole.Hunter
            ? LevelManager.Instance.hunterSpawnPositions
            : LevelManager.Instance.vandalistSpawnPositions;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[PlayerRoleSetup] No spawn positions for {role}!");
            return transform.position;
        }

        return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
    }

    /// <summary>
    /// Server-authoritative respawn. Real players get a TargetRpc (movement is
    /// client-authoritative); the server positions ownerless objects like test dummies directly.
    /// </summary>
    [Server]
    private void ServerRespawn(PlayerRole role)
    {
        Vector3 spawnPos = PickSpawnPosition(role);

        if (connectionToClient != null && movement != null)
        {
            TargetTeleport(connectionToClient, spawnPos);
            return;
        }

        // Ownerless (test dummy): no CharacterController to compensate the spawn point offset,
        // so put it on the ground ourselves. Otherwise the dummy floats after respawning.
        transform.position = SpawnPlacement.DropToGround(
            spawnPos, GetComponent<CapsuleCollider>(), ownerlessGroundMask);
    }

    [TargetRpc]
    private void TargetTeleport(NetworkConnectionToClient target, Vector3 position)
    {
        if (movement != null) movement.TeleportTo(position);
    }

    // ── Catch / Respawn Logic ────────────────────────────────────────────────

    private void HandleVandalistCaught(NetworkIdentity caught)
    {
        if (caught != netIdentity) return;
        if (!isServer) return;  // only the server decides about catch/respawn
        if (isCaught) return;   // already in the caught/respawn process

        HandleCaughtServer();
    }

    [Server]
    private void HandleCaughtServer()
    {
        catchCount++;
        isInvulnerable = false;
        isEliminated = !allowRespawn || (maxCatches >= 0 && catchCount > maxCatches);
        isCaught = true;
        ApplyCaughtState(true);

        OnCaughtStateChangedServer?.Invoke(this, true);

        if (isEliminated)
        {
            Debug.Log($"[PlayerRoleSetup] {name} was eliminated for good (catch #{catchCount}).");
            return;
        }

        StartCoroutine(RespawnAfterDelay());
    }

    [Server]
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        ServerRespawn(PlayerRole.Vandalist);
        isInvulnerable = true;
        isCaught = false;
        ApplyCaughtState(false);

        if (connectionToClient != null) TargetRespawnFeedback(connectionToClient);

        OnCaughtStateChangedServer?.Invoke(this, false);

        yield return new WaitForSeconds(respawnInvulnerability);
        isInvulnerable = false;
    }

    /// <summary>Runs on all clients (including the host) as soon as isCaught changes.</summary>
    private void OnCaughtStateChanged(bool oldValue, bool newValue)
    {
        ApplyCaughtState(newValue);

        // Feedback deliberately only here (not in ApplyCaughtState): the hook fires exactly once
        // per state change per client, while ApplyCaughtState runs twice on the host.
        // The "back in the game" feedback comes via TargetRpc from RespawnAfterDelay, so it
        // doesn't show up wrongly when resetting for the lobby (ResetForLobby).
        if (!isOwned || oldValue == newValue) return;

        if (newValue) PlayCaughtFeedback();
        else GameFeelHUD.HideCountdown();
    }

    [TargetRpc]
    private void TargetRespawnFeedback(NetworkConnectionToClient target)
    {
        GameFeelHUD.HideCountdown();
        GameFeelHUD.CenterPop("BACK IN THE GAME!", GameFeelSettings.Instance.vandalistColor, 0.8f);
    }

    private void PlayCaughtFeedback()
    {
        GameFeelSettings settings = GameFeelSettings.Instance;

        CameraEffects.Shake(settings.caughtTrauma);
        GameFeelHUD.Flash(settings.caughtFlashColor, 0.8f);

        if (isEliminated)
        {
            GameFeelHUD.Banner("ELIMINATED!", settings.caughtFlashColor, 2.5f);
        }
        else
        {
            GameFeelHUD.Banner("CAUGHT!", settings.hunterColor, 1.2f);
            GameFeelHUD.Countdown("Respawn in", respawnDelay);
        }
    }

    /// <summary>
    /// Matches visibility, colliders and controls to the caught state.
    /// Deliberately idempotent: the SyncVar hook covers clients and host, the server call covers
    /// the dedicated server case, where Mirror doesn't fire the hook.
    /// </summary>
    private void ApplyCaughtState(bool caught)
    {
        if (playerVisuals != null) playerVisuals.SetActive(!caught);

        // A caught player must not be an invisible obstacle: no collision, no raycast target,
        // no triggers. Runs on all clients AND the server, so the server-side hit check doesn't
        // find them anymore either.
        SetBodyCollidersEnabled(!caught);

        if (isOwned && caughtStateVisual != null)
            caughtStateVisual.SetActive(caught);

        if (!isOwned) return;

        if (movement != null)
        {
            if (caught) movement.FreezeMovement();
            else movement.UnfreezeMovement();
        }

        if (assignedRole == PlayerRole.Vandalist)
            SetInteractActive(!caught);
    }

    private void SetBodyCollidersEnabled(bool value)
    {
        if (bodyColliders == null) return;

        foreach (Collider col in bodyColliders)
        {
            if (col != null) col.enabled = value;
        }
    }

    // ── Returning to the Lobby ───────────────────────────────────────────────

    [Server]
    public void ResetForLobby()
    {
        if (initRoutine != null)
        {
            StopCoroutine(initRoutine);
            initRoutine = null;
        }

        // Cancel a possibly running RespawnAfterDelay coroutine – otherwise it would still fire
        // after the scene change and try to teleport/respawn a player who is just arriving
        // in the lobby.
        StopAllCoroutines();

        catchCount = 0;
        isInvulnerable = false;
        isEliminated = false;

        if (isCaught)
        {
            isCaught = false;   // the SyncVar hook cleans up visuals/colliders on all clients
            ApplyCaughtState(false); // dedicated server: the hook doesn't fire there by itself
        }
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [Button("Validate Setup")]
    private void ValidateSetup()
    {
        bool ok = true;

        CustomNetworkManager mgr = NetworkManager.singleton as CustomNetworkManager;
        if (mgr == null)
            Debug.LogWarning("[PlayerRoleSetup] NetworkManager not found (can only be checked in Play Mode).");
        else if (string.IsNullOrEmpty(mgr.GameplayScene))
        { Debug.LogError("[PlayerRoleSetup] GameplayScene is not set in the NetworkManager!"); ok = false; }

        if (hunterConfig == null) { Debug.LogError("[PlayerRoleSetup] hunterConfig is missing!"); ok = false; }
        if (vandalistConfig == null) { Debug.LogError("[PlayerRoleSetup] vandalistConfig is missing!"); ok = false; }
        if (movement == null) { Debug.LogError("[PlayerRoleSetup] PlayerMovement is missing!"); ok = false; }
        if (playerVisuals == null) Debug.LogWarning("[PlayerRoleSetup] playerVisuals not set – lobby hiding won't work.");
        if (hunterModel == null) Debug.LogWarning("[PlayerRoleSetup] hunterModel not set.");
        if (vandalistModel == null) Debug.LogWarning("[PlayerRoleSetup] vandalistModel not set.");
        if (interact == null) Debug.LogWarning("[PlayerRoleSetup] PlayerInteract is missing.");
        if (accuse == null) Debug.LogWarning("[PlayerRoleSetup] HunterAccuse is missing.");
        if (allowRespawn && respawnDelay <= 0f)
            Debug.LogWarning("[PlayerRoleSetup] respawnDelay is 0 or negative – the player respawns instantly.");

        if (ok) Debug.Log("[PlayerRoleSetup] Setup complete.");
    }

    [Button("Debug: Force Catch")]
    private void DebugForceCatch()
    {
        if (!Application.isPlaying || !isServer)
        {
            Debug.LogWarning("[PlayerRoleSetup] Only usable in Play Mode as the server.");
            return;
        }
        if (isCaught)
        {
            Debug.LogWarning("[PlayerRoleSetup] Player is already caught.");
            return;
        }

        HandleCaughtServer();
    }
#endif
}
