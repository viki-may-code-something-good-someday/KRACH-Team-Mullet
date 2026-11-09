using Mirror;
using Steamworks;
using UnityEngine;

public class SteamLobby : MonoBehaviour
{
    public static SteamLobby instance;

    //Callbacks
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> JoinRequest;
    protected Callback<LobbyEnter_t> LobbyEntered;

    public ulong currentLobbyID;
    private const string hostAddressKey = "HostAddress";
    private CustomNetworkManager customManager;

    private void Start()
    {
        if (!SteamManager.Initialized) { Debug.LogError("[SteamLobby] Steam is not running."); return; }
        if (instance == null) { instance = this; }

        customManager = GetComponent<CustomNetworkManager>();

        LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        JoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void HostLobby()
    {
        if (customManager == null) customManager = GetComponent<CustomNetworkManager>();

        // Still in an old lobby (e.g. after "Leave Game")? Leave it cleanly first.
        LeaveCurrentLobby();
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, customManager.maxConnections);
    }

    /// <summary>Leaves the current Steam lobby if you're in one. Safe to call multiple times.</summary>
    public void LeaveCurrentLobby()
    {
        if (currentLobbyID == 0 || !SteamManager.Initialized) return;

        SteamMatchmaking.LeaveLobby(new CSteamID(currentLobbyID));
        currentLobbyID = 0;
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobby] Lobby could not be created: {callback.m_eResult}");
            return;
        }

        Debug.Log("[SteamLobby] Lobby created successfully");

        customManager.StartHost();

        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), hostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "name", SteamFriends.GetPersonaName());
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("[SteamLobby] Request to join lobby");

        // Anyone currently hosting or connected has to leave first – otherwise you end up
        // half in two sessions.
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[SteamLobby] Invite ignored – already in a running session.");
            return;
        }

        LeaveCurrentLobby();
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = callback.m_ulSteamIDLobby;

        //Clients
        if (NetworkServer.active) { return; }

        customManager.networkAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), hostAddressKey);

        customManager.StartClient();
    }
}
