using Steamworks;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class LobbyController : MonoBehaviour
{
    public static LobbyController instance;

    //UI 
    public TextMeshProUGUI lobbyNameText;

    //Player Data
    public GameObject hunterPlayerListViewContent;
    public GameObject vandalistPlayerListViewContent;
    public GameObject playerListItemPrefab;
    public GameObject localPlayerObject;

    //Data
    public ulong currentLobbyID;
    public bool playerItemCreated = false;
    private List<PlayerListItem> totalPlayerbaseListItems = new List<PlayerListItem>();
    private List<PlayerListItem> hunterPlayerListItems = new List<PlayerListItem>();
    private List<PlayerListItem> vandalistPlayerListItems = new List<PlayerListItem>();
    public PlayerObjectController localPlayerController;

    //Manager
    private CustomNetworkManager manager;

    private CustomNetworkManager Manager
    {
        get
        {
            if (manager != null)
            {
                return manager;
            }
            return manager = CustomNetworkManager.singleton as CustomNetworkManager;
        }
    }


    private void Awake()
    {
        if (instance == null) { instance = this; }
    }

    public void UpdateLobbyName()
    {
        currentLobbyID = Manager.GetComponent<SteamLobby>().currentLobbyID;
        lobbyNameText.text = SteamMatchmaking.GetLobbyData(new CSteamID(currentLobbyID), "name");
    }

    public void UpdatePlayerList()
    {
        if (!playerItemCreated)
        {
            CreateHostPlayerItem();
        }

        if (totalPlayerbaseListItems.Count < Manager.gamePlayers.Count)
        {
            CreateClientPlayerItem();
        }

        if (totalPlayerbaseListItems.Count > Manager.gamePlayers.Count)
        {
            RemovePlayerItem();
        }

        if (totalPlayerbaseListItems.Count == Manager.gamePlayers.Count)
        {
            UpdatePlayerItem();
        }
    }

    public void FindLocalPlayer()
    {
        localPlayerObject = GameObject.Find("LocalGamePlayer");
        localPlayerController = localPlayerObject.GetComponent<PlayerObjectController>();
    }

    public void CreateHostPlayerItem()
    {
        foreach (PlayerObjectController player in Manager.gamePlayers)
        {
            GameObject newPlayerItem = Instantiate(playerListItemPrefab) as GameObject;
            PlayerListItem newPlayerItemScript = newPlayerItem.GetComponent<PlayerListItem>();

            newPlayerItemScript.playerName = player.playerName;
            newPlayerItemScript.connectionID = player.connectionID;
            newPlayerItemScript.playerSteamID = player.playerSteamID;

            AddPlayerToListAndSetValues(1, newPlayerItemScript);

            playerItemCreated = true;
        }
    }

    public void AddPlayerToListAndSetValues(int isHunterOrVandalist, PlayerListItem playerItem) // 0 = Hunter, 1 = Vandalist - maybe in the future 2 = Viewer
    {
        switch (isHunterOrVandalist)
        {
            case 0:
                playerItem.transform.SetParent(hunterPlayerListViewContent.transform);
                hunterPlayerListItems.Add(playerItem);
                playerItem.SetPlayerValues(PlayerRole.Hunter);
                break;
            case 1:
                playerItem.transform.SetParent(vandalistPlayerListViewContent.transform);
                vandalistPlayerListItems.Add(playerItem);
                playerItem.SetPlayerValues(PlayerRole.Vandalist);
                break;
        }
        playerItem.transform.localScale = Vector3.one;
        totalPlayerbaseListItems.Add(playerItem);
    }

    public void RemovePlayerFromList(PlayerListItem playerItem, GameObject objToRemove)
    {
        totalPlayerbaseListItems.Remove(playerItem);
        if (hunterPlayerListItems.Contains(playerItem))
        {
            hunterPlayerListItems.Remove(playerItem);
        }
        if (vandalistPlayerListItems.Contains(playerItem))
        {
            vandalistPlayerListItems.Remove(playerItem);
        }
        Destroy(objToRemove);
    }

    public void CreateClientPlayerItem()
    {
        foreach (PlayerObjectController player in Manager.gamePlayers)
        {
            if (!totalPlayerbaseListItems.Any(b => b.connectionID == player.connectionID))
            {
                GameObject newPlayerItem = Instantiate(playerListItemPrefab) as GameObject;
                PlayerListItem newPlayerItemScript = newPlayerItem.GetComponent<PlayerListItem>();

                newPlayerItemScript.playerName = player.playerName;
                newPlayerItemScript.connectionID = player.connectionID;
                newPlayerItemScript.playerSteamID = player.playerSteamID;

                AddPlayerToListAndSetValues(1, newPlayerItemScript);
            }
        }
    }

    public void UpdatePlayerItem()
    {
        foreach (PlayerObjectController player in Manager.gamePlayers)
        {
            foreach (PlayerListItem playerListItemScript in totalPlayerbaseListItems)
            {
                if (playerListItemScript.connectionID == player.connectionID) //is this us?
                {
                    playerListItemScript.playerName = player.playerName;
                    playerListItemScript.SetPlayerValues(PlayerRole.Default); //for testing default
                }
            }
        }
    }

    public void RemovePlayerItem()
    {
        List<PlayerListItem> playerListItemToRemove = new List<PlayerListItem>();

        foreach (PlayerListItem playerListItem in totalPlayerbaseListItems)
        {
            if (!Manager.gamePlayers.Any(b => b.connectionID == playerListItem.connectionID))
            {
                playerListItemToRemove.Add(playerListItem);
            }
        }
        if (playerListItemToRemove.Count > 0)
        {
            foreach (PlayerListItem playerlistItemToRemove in playerListItemToRemove)
            {
                GameObject objectToRemove = playerlistItemToRemove.gameObject;
                RemovePlayerFromList(playerlistItemToRemove, objectToRemove);
                objectToRemove = null;
            }
        }
    }
}
