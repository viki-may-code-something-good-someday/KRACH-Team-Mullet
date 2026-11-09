using Mirror;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
    [Header("References")]
    [SerializeField] private PlayerObjectController gamePlayerPrefab;

    [Scene]
    [SerializeField]
    private string lobbyScene;

    [Scene]
    [SerializeField]
    private string gameplayScene;

    /// <summary>
    /// Path of the gameplay scene.
    /// Used by CameraController and PlayerRoleSetup.
    /// </summary>
    public string GameplayScene => gameplayScene;

    /// <summary>
    /// Path of the lobby scene. Needed for ReturnToLobby(), because OnServerAddPlayer only
    /// allows new players in the onlineScene and we don't want to create new player objects
    /// when switching back.
    /// </summary>
    public string LobbyScene => lobbyScene;


    public List<PlayerObjectController> gamePlayers { get; } = new List<PlayerObjectController>();

    /// <summary>
    /// Server-authoritative mirror of LobbyController.IsTestMode at start time.
    /// Set via StartGame() (LobbyController only exists client-side in the lobby scene and
    /// therefore can't be read by the server directly – the value has to be passed through
    /// the existing command chain).
    /// Used by TestDummySpawner to decide whether dummies get spawned.
    /// </summary>
    public bool IsTestMode { get; private set; }

    public WinningSide LastWinner { get; private set; }

    public void SetLastWinner(WinningSide winner) => LastWinner = winner;


    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (SceneManager.GetActiveScene().path != onlineScene) return;

        PlayerObjectController gamePlayerInstance = Instantiate(gamePlayerPrefab);

        bool isHost = conn is LocalConnectionToClient;

        gamePlayerInstance.connectionID = conn.connectionId;
        gamePlayerInstance.playerIdNumber = isHost ? 1 : NextFreePlayerIdNumber();
        gamePlayerInstance.playerSteamID = ResolveSteamID(conn, isHost);

        NetworkServer.AddPlayerForConnection(conn, gamePlayerInstance.gameObject);

        // Sensible initial default: first player (host) → Hunter, everyone else → Vandalist.
        gamePlayerInstance.SetPlayerRole(isHost ? PlayerRole.Hunter : PlayerRole.Vandalist);
    }

    /// <summary>
    /// Smallest free number starting at 2 (1 is reserved for the host). Previously
    /// gamePlayers.Count + 1 was used – after a leave/rejoin that produced duplicate numbers.
    /// </summary>
    private int NextFreePlayerIdNumber()
    {
        int candidate = 2;
        while (gamePlayers.Exists(p => p != null && p.playerIdNumber == candidate))
            candidate++;
        return candidate;
    }

    /// <summary>
    /// Steam ID of the player. The Fizzy Steamworks transport address IS the client's Steam ID.
    /// Previously the lobby member at index gamePlayers.Count was used – that no longer matches
    /// as soon as someone leaves the lobby (wrong avatar/name in the list).
    /// </summary>
    private static ulong ResolveSteamID(NetworkConnectionToClient conn, bool isHost)
    {
        if (!SteamManager.Initialized) return 0;

        if (isHost) return SteamUser.GetSteamID().m_SteamID;

        if (ulong.TryParse(conn.address, out ulong steamId)) return steamId;

        Debug.LogWarning($"[CustomNetworkManager] Could not read a Steam ID from address '{conn.address}'.");
        return 0;
    }

    /// <param name="testMode">
    /// Mirrors LobbyController.IsTestMode at start time. Has to be passed through the command
    /// chain (PlayerObjectController.CanStartGame → here).
    /// </param>
    public void StartGame(string sceneName, bool useCustomNetworkGameplayScene, bool testMode)
    {
        if (useCustomNetworkGameplayScene)
        {
            sceneName = gameplayScene;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[CustomNetworkManager] No gameplay scene set – game start aborted.");
            return;
        }

        IsTestMode = testMode;
        ServerChangeScene(sceneName);
    }

    // ── Return to Lobby ──────────────────────────────────────────────────────

    /// <summary>
    /// Switches all connected players back to the lobby scene.
    /// The existing PlayerObjectController instances (gamePlayers) are kept –
    /// ServerChangeScene only destroys scene-specific objects; player objects
    /// (DontDestroyOnLoad) live on through their connection.
    /// Ready state and caught state are reset so the lobby UI and PlayerRoleSetup
    /// start cleanly again.
    /// </summary>
    [Server]
    public void ReturnToLobby()
    {
        if (string.IsNullOrEmpty(lobbyScene))
        {
            Debug.LogError("[CustomNetworkManager] lobbyScene is not set!");
            return;
        }

        foreach (PlayerObjectController player in gamePlayers)
        {
            if (player == null) continue;

            player.ResetForLobby();

            PlayerRoleSetup roleSetup = player.GetComponent<PlayerRoleSetup>();
            if (roleSetup != null) roleSetup.ResetForLobby();
        }

        ServerChangeScene(lobbyScene);
    }

    /// <summary>
    /// Runs on the server once the new scene has fully loaded.
    /// Important for late-join-like situations: gamePlayers can be revalidated here
    /// in case connections drop during the switch.
    /// </summary>
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (sceneName == lobbyScene)
        {
            gamePlayers.RemoveAll(p => p == null);
            IsTestMode = false;
        }
    }

    // ── Session End ──────────────────────────────────────────────────────────

    /// <summary>
    /// Runs when leaving a session (client and host). Without LeaveLobby you stayed a member
    /// of the old Steam lobby – hosting/joining again then ran into state errors.
    /// </summary>
    public override void OnStopClient()
    {
        base.OnStopClient();

        gamePlayers.Clear();
        IsTestMode = false;
        LastWinner = WinningSide.None;

        if (SteamLobby.instance != null) SteamLobby.instance.LeaveCurrentLobby();
    }
}
