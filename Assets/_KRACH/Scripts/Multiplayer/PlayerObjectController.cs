using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;

public class PlayerObjectController : NetworkBehaviour
{
    //Player Data
    [SyncVar] public int connectionID;
    [SyncVar] public int playerIdNumber;
    [SyncVar] public ulong playerSteamID;

    [SyncVar(hook = nameof(PlayerNameUpdate))] public string playerName;
    [SyncVar(hook = nameof(PlayerReadyUpdate))] public bool ready;

    [SyncVar(hook = nameof(OnPlayerRoleChanged))]
    public PlayerRole playerRole = PlayerRole.Vandalist;

    /// <summary>
    /// Fires on every client as soon as the role arrives via the SyncVar or changes.
    /// PlayerRoleSetup hooks in here so that a late-arriving role (lag, late join)
    /// doesn't get lost.
    /// </summary>
    public event System.Action<PlayerRole> RoleChanged;


    private CustomNetworkManager networkManager;

    public TMP_Text nameText;
    public Canvas nameCanvas;

    private CustomNetworkManager NetworkManager
    {
        get
        {
            if (networkManager != null)
            {
                return networkManager;
            }
            return networkManager = CustomNetworkManager.singleton as CustomNetworkManager;
        }
    }

    private void Start()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    private void PlayerReadyUpdate(bool oldValue, bool newValue)
    {
        if (isServer)
        {
            this.ready = newValue;
        }
        if (isClient && LobbyController.instance != null)
        {
            LobbyController.instance.UpdatePlayerList();
        }
    }

    [Command]
    private void CmdSetPlayerReady()
    {
        this.PlayerReadyUpdate(this.ready, !this.ready);
    }

    [Command]
    private void CmdSetReadyState(bool newState)
    {
        this.PlayerReadyUpdate(this.ready, newState);
    }

    public void ChangeReady()
    {
        if (isOwned)
        {
            CmdSetPlayerReady();
        }
    }

    public override void OnStartAuthority()
    {
        // Test dummies and other ownerless objects have no authority – only the real local
        // player ends up here.
        string personaName = SteamManager.Initialized ? SteamFriends.GetPersonaName() : $"Player {playerIdNumber}";
        CmdSetPlayerName(personaName);
        gameObject.name = "LocalGamePlayer";

        // When starting directly from the gameplay scene (GameManager.testInEditor) there is no lobby.
        if (LobbyController.instance != null)
        {
            LobbyController.instance.FindLocalPlayer();
            LobbyController.instance.UpdateLobbyName();
        }

        if (isServer)
        {
            CmdSetReadyState(true);
        }
    }

    public override void OnStartClient()
    {
        if (NetworkManager == null)
        {
            Debug.LogError("CustomNetworkManager not found!");
            return;
        }

        networkManager.gamePlayers.Add(this);

        // The LobbyController only lives in the lobby scene. Test dummies are only spawned in
        // the gameplay scene – without this check every dummy spawn throws here.
        if (LobbyController.instance != null)
        {
            LobbyController.instance.UpdateLobbyName();
            LobbyController.instance.UpdatePlayerList();
        }

        if ((isOwned || isLocalPlayer) && nameCanvas != null)
        {
            nameCanvas.gameObject.SetActive(false);
        }
    }

    public override void OnStopClient()
    {
        if (networkManager != null) networkManager.gamePlayers.Remove(this);
        if (LobbyController.instance != null) LobbyController.instance.UpdatePlayerList();
    }

    [Command]
    private void CmdSetPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) playerName = $"Player {playerIdNumber}";
        if (playerName.Length > 32) playerName = playerName.Substring(0, 32);
        this.PlayerNameUpdate(this.playerName, playerName);
    }


    public void PlayerNameUpdate(string oldValue, string newValue)
    {
        if (isServer)
        {
            this.playerName = newValue;
        }
        if (isClient)
        {
            if (LobbyController.instance != null) LobbyController.instance.UpdatePlayerList();

            if (nameText != null)
            {
                nameText.text = newValue;
            }
        }
    }


    /// <param name="testMode">
    /// Mirrors LobbyController.IsTestMode at start time. Passed through to the server
    /// (CustomNetworkManager.StartGame), because the LobbyController only exists client-side
    /// and the server can't read this value directly.
    /// </param>
    public void CanStartGame(string sceneName, bool useCustomNetworkGameplayScene, bool testMode)
    {
        if (isOwned)
        {
            CmdCanStartGame(sceneName, useCustomNetworkGameplayScene, testMode);
        }
    }

    [Command]
    public void CmdCanStartGame(string sceneName, bool useCustomNetworkGameplayScene, bool testMode)
    {
        // The start button is only active for the host – still secure it on the server side,
        // otherwise any client could start the game.
        if (!(connectionToClient is LocalConnectionToClient)) return;
        NetworkManager.StartGame(sceneName, useCustomNetworkGameplayScene, testMode);
    }

    [Command]
    public void CmdChangeRole(PlayerRole newRole)
    {
        SetPlayerRole(newRole);
    }

    public void SetPlayerRole(PlayerRole role)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[PlayerObjectController] SetPlayerRole may only be called on the server.");
            return;
        }
        playerRole = role; // SyncVar → replicated to all clients automatically
    }

    private void OnPlayerRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        Debug.Log($"[PlayerObjectController] {playerName}: {oldRole} → {newRole}");

        if (isClient && LobbyController.instance != null)
        {
            // The role changed on the network – refresh the UI!
            LobbyController.instance.UpdatePlayerList();
        }

        RoleChanged?.Invoke(newRole);
    }

    [Command]
    public void CmdReturnToLobby()
    {
        // Only the host may trigger this – recognizable by its local connection.
        if (!(connectionToClient is LocalConnectionToClient)) return;
        (Mirror.NetworkManager.singleton as CustomNetworkManager)?.ReturnToLobby();
    }

    [Server]
    public void ResetForLobby()
    {
        ready = false;
        // Deliberately NOT resetting playerRole – LobbyController.UpdatePlayerItem() reads the
        // role from the SyncVar and re-sorts the UI list automatically.
    }
}
