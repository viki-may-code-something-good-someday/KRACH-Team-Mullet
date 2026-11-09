using DG.Tweening;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyController : MonoBehaviour
{
    public static LobbyController instance;

    // ── UI ───────────────────────────────────────────────────────────────────

    public TextMeshProUGUI lobbyNameText;

    // ── Player Data ──────────────────────────────────────────────────────────

    public GameObject hunterPlayerListViewContent;
    public GameObject vandalistPlayerListViewContent;
    public GameObject playerListItemPrefab;

    // ── State ────────────────────────────────────────────────────────────────

    public ulong currentLobbyID;
    public bool playerItemCreated = false;

    private List<PlayerListItem> totalPlayerbaseListItems = new List<PlayerListItem>();
    private List<PlayerListItem> hunterPlayerListItems = new List<PlayerListItem>();
    private List<PlayerListItem> vandalistPlayerListItems = new List<PlayerListItem>();

    [HideInInspector] public PlayerObjectController localPlayerController;

    // ── Ready & Start ────────────────────────────────────────────────────────

    public Button startGameButton;
    public Button switchSidesButton;
    public Toggle readyToggle;
    public TextMeshProUGUI readyToggleText;

    // ── Test Mode ─────────────────────────────────────────────────────────────

    [Header("Test Mode")]
    [Tooltip("Skips role validation and the ready check. Allows a solo start with COM dummies.")]
    [SerializeField] private bool isTestMode = false;
    [Tooltip("Activated when test mode is on (e.g. a text or panel).")]
    [SerializeField] private GameObject testModeIndicator;

    // ── Role Limits ──────────────────────────────────────────────────────────

    private const int hunterMaxNumber = 1;
    private const int vandalistMaxNumber = 4;

    public TextMeshProUGUI hunterCurrent;
    public TextMeshProUGUI hunterMax;

    public TextMeshProUGUI vandalistCurrent;
    public TextMeshProUGUI vandalistMax;

    public Color defaultTextColor;
    public Color overshootTextColor;

    // ── Manager ──────────────────────────────────────────────────────────────

    private CustomNetworkManager manager;
    private CustomNetworkManager Manager
    {
        get
        {
            if (manager != null) return manager;
            return manager = CustomNetworkManager.singleton as CustomNetworkManager;
        }
    }

    // ── Awake ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Exactly one LobbyController per lobby scene – the new instance after returning from
        // the game replaces the destroyed old one.
        instance = this;

        if (startGameButton != null)
            startGameButton.interactable = false;
        if (switchSidesButton != null)
            switchSidesButton.interactable = false;

        UpdateTestModeIndicator();
    }

    private void Start()
    {
        // When returning from the gameplay scene, the PlayerObjectController instances already
        // exist (DontDestroyOnLoad) – their OnStartClient()/OnStartAuthority() therefore doesn't
        // fire again. This new LobbyController instance has to initialize itself instead of
        // relying on an external trigger.
        FindLocalPlayer();
        UpdateLobbyName();
        UpdatePlayerList();
        CheckIfAllReady();
        UpdateReadyText();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ── Game Start ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the Start Game button.
    /// Uses the gameplay scene from the CustomNetworkManager.
    /// </summary>
    public void StartGame()
    {
        StartGameInternal(useManagerScene: true, sceneName: "");
    }

    /// <summary>
    /// Starts the game with an explicit scene (e.g. for tests with a different scene).
    /// </summary>
    public void StartGame(string sceneName)
    {
        StartGameInternal(useManagerScene: false, sceneName: sceneName);
    }

    private void StartGameInternal(bool useManagerScene, string sceneName)
    {
        if (localPlayerController == null) FindLocalPlayer();
        if (localPlayerController == null) return;

        if (!ValidateRoleLimits())
        {
            PunchRoleCounters();
            return;
        }
        AssignRolesToAllPlayers();

        // isTestMode is passed through explicitly here, because the CustomNetworkManager (server)
        // can't read this value directly from the LobbyController (client-side only, lobby scene only).
        localPlayerController.CanStartGame(useManagerScene ? "" : sceneName, useManagerScene, isTestMode);
    }

    private bool ValidateRoleLimits()
    {
        if (isTestMode)
        {
            Debug.Log("[Lobby] Test mode – role validation skipped.");
            return true;
        }

        if (hunterPlayerListItems.Count == 0)
        {
            Debug.LogWarning("[Lobby] No hunter assigned.");
            return false;
        }
        if (vandalistPlayerListItems.Count == 0)
        {
            Debug.LogWarning("[Lobby] No vandalist assigned.");
            return false;
        }
        if (hunterPlayerListItems.Count > hunterMaxNumber)
        {
            Debug.LogWarning($"[Lobby] Too many hunters ({hunterPlayerListItems.Count}/{hunterMaxNumber}).");
            return false;
        }
        if (vandalistPlayerListItems.Count > vandalistMaxNumber)
        {
            Debug.LogWarning($"[Lobby] Too many vandalists ({vandalistPlayerListItems.Count}/{vandalistMaxNumber}).");
            return false;
        }

        return true;
    }

    private void AssignRolesToAllPlayers()
    {
        foreach (PlayerListItem item in hunterPlayerListItems)
        {
            PlayerObjectController poc = GetPlayerControllerByConnectionID(item.connectionID);
            poc?.SetPlayerRole(PlayerRole.Hunter);
        }

        foreach (PlayerListItem item in vandalistPlayerListItems)
        {
            PlayerObjectController poc = GetPlayerControllerByConnectionID(item.connectionID);
            poc?.SetPlayerRole(PlayerRole.Vandalist);
        }

        // Test mode: players that aren't in any list get the default role Vandalist
        if (isTestMode)
        {
            foreach (PlayerObjectController poc in Manager.gamePlayers)
            {
                bool assigned = hunterPlayerListItems.Any(i => i.connectionID == poc.connectionID)
                             || vandalistPlayerListItems.Any(i => i.connectionID == poc.connectionID);
                if (!assigned)
                    poc.SetPlayerRole(PlayerRole.Vandalist);
            }
        }
    }

    private PlayerObjectController GetPlayerControllerByConnectionID(int connectionID)
    {
        return Manager.gamePlayers.FirstOrDefault(p => p.connectionID == connectionID);
    }

    // ── Lobby Name ────────────────────────────────────────────────────────────

    public void UpdateLobbyName()
    {
        if (Manager == null || lobbyNameText == null) return;

        SteamLobby steamLobby = Manager.GetComponent<SteamLobby>();
        if (steamLobby == null || !SteamManager.Initialized) return;

        currentLobbyID = steamLobby.currentLobbyID;
        lobbyNameText.text = SteamMatchmaking.GetLobbyData(new CSteamID(currentLobbyID), "name");
    }

    // ── Find the Local Player ─────────────────────────────────────────────────

    public void FindLocalPlayer()
    {
        GameObject localPlayerObject = GameObject.Find("LocalGamePlayer");
        if (localPlayerObject == null)
        {
            Debug.LogError("[Lobby] LocalGamePlayer not found!");
            return;
        }
        localPlayerController = localPlayerObject.GetComponent<PlayerObjectController>();
    }

    // ── Player List ───────────────────────────────────────────────────────────

    public void UpdatePlayerList()
    {
        if (Manager == null) return;

        if (!playerItemCreated)
            CreateHostPlayerItem();
        else if (totalPlayerbaseListItems.Count < Manager.gamePlayers.Count)
            CreateClientPlayerItem();
        else if (totalPlayerbaseListItems.Count > Manager.gamePlayers.Count)
            RemovePlayerItem();
        else
            UpdatePlayerItem();
    }

    public void CreateHostPlayerItem()
    {
        foreach (PlayerObjectController player in Manager.gamePlayers)
        {
            PlayerListItem item = CreatePlayerListItem(player);

            int role = player.playerRole == PlayerRole.Hunter ? 0 : 1;
            AddPlayerToList(role, item);
        }

        playerItemCreated = true;
    }

    public void CreateClientPlayerItem()
    {
        foreach (PlayerObjectController player in Manager.gamePlayers)
        {
            if (totalPlayerbaseListItems.Any(i => i.connectionID == player.connectionID))
                continue;

            // Use the real role from the SyncVar instead of always Vandalist – otherwise the
            // entry only jumps into the right column on the next update.
            PlayerListItem item = CreatePlayerListItem(player);
            AddPlayerToList(player.playerRole == PlayerRole.Hunter ? 0 : 1, item);
        }

        // Re-evaluate the ready state/start button when someone joins, too.
        CheckIfAllReady();
    }

    private PlayerListItem CreatePlayerListItem(PlayerObjectController player)
    {
        GameObject obj = Instantiate(playerListItemPrefab);
        PlayerListItem item = obj.GetComponent<PlayerListItem>();

        item.playerName = player.playerName;
        item.connectionID = player.connectionID;
        item.playerSteamID = player.playerSteamID;
        item.isReady = player.ready;

        return item;
    }

    /// <param name="role">0 = Hunter, 1 = Vandalist</param>
    public void AddPlayerToList(int role, PlayerListItem item)
    {
        switch (role)
        {
            case 0:
                item.transform.SetParent(hunterPlayerListViewContent.transform);
                hunterPlayerListItems.Add(item);
                item.SetPlayerValues(PlayerRole.Hunter);
                break;
            default:
                item.transform.SetParent(vandalistPlayerListViewContent.transform);
                vandalistPlayerListItems.Add(item);
                item.SetPlayerValues(PlayerRole.Vandalist);
                break;
        }

        item.transform.localScale = Vector3.one;
        totalPlayerbaseListItems.Add(item);
        UpdateRoleCountTexts();
    }

    public void RemovePlayerFromList(PlayerListItem item)
    {
        totalPlayerbaseListItems.Remove(item);
        hunterPlayerListItems.Remove(item);
        vandalistPlayerListItems.Remove(item);
        Destroy(item.gameObject);
        UpdateRoleCountTexts();
    }

    public void UpdatePlayerItem()
    {
        foreach (PlayerObjectController player in Manager.gamePlayers)
        {
            PlayerListItem item = totalPlayerbaseListItems
                .FirstOrDefault(i => i.connectionID == player.connectionID);

            if (item == null) continue;

            item.playerName = player.playerName;
            item.isReady = player.ready;

            // IMPORTANT: we read the real role directly from the network (SyncVar)!
            PlayerRole networkedRole = player.playerRole;
            item.SetPlayerValues(networkedRole);
            item.UpdateReadyStatusText();

            // Move the UI element into the right scroll list (parent)
            if (networkedRole == PlayerRole.Hunter && !hunterPlayerListItems.Contains(item))
            {
                if (vandalistPlayerListItems.Contains(item)) vandalistPlayerListItems.Remove(item);
                hunterPlayerListItems.Add(item);
                item.transform.SetParent(hunterPlayerListViewContent.transform);
                item.transform.localScale = Vector3.one;
            }
            else if (networkedRole == PlayerRole.Vandalist && !vandalistPlayerListItems.Contains(item))
            {
                if (hunterPlayerListItems.Contains(item)) hunterPlayerListItems.Remove(item);
                vandalistPlayerListItems.Add(item);
                item.transform.SetParent(vandalistPlayerListViewContent.transform);
                item.transform.localScale = Vector3.one;
            }

            if (player == localPlayerController)
                UpdateReadyText();
        }

        CheckIfAllReady();
        UpdateRoleCountTexts();
    }

    /// <summary>Short shake of the role counters when the start fails because of the role limits.</summary>
    private void PunchRoleCounters()
    {
        foreach (TextMeshProUGUI text in new[] { hunterCurrent, vandalistCurrent })
        {
            if (text == null) continue;
            text.rectTransform.DOKill(true);
            text.rectTransform.DOShakeAnchorPos(0.35f, 12f, 30).SetLink(text.gameObject);
        }
    }

    public void RemovePlayerItem()
    {
        List<PlayerListItem> toRemove = totalPlayerbaseListItems
            .Where(item => item == null || !Manager.gamePlayers.Any(p => p.connectionID == item.connectionID))
            .ToList();

        foreach (PlayerListItem item in toRemove)
        {
            if (item == null)
            {
                totalPlayerbaseListItems.Remove(item);
                hunterPlayerListItems.Remove(item);
                vandalistPlayerListItems.Remove(item);
            }
            else
            {
                RemovePlayerFromList(item);
            }
        }

        UpdateRoleCountTexts();
    }

    // ── Ready ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the ready toggle in the UI.
    /// </summary>
    public void ToggleReady()
    {
        if (localPlayerController == null) return;
        localPlayerController.ChangeReady();
    }

    public void UpdateReadyText()
    {
        if (localPlayerController == null || readyToggleText == null) return;
        readyToggleText.text = localPlayerController.ready ? "Ready!" : "Ready?";
    }

    public void CheckIfAllReady()
    {
        if (localPlayerController == null || startGameButton == null) return;

        bool isHost = localPlayerController.playerIdNumber == 1;

        if (isTestMode)
        {
            startGameButton.interactable = isHost;
            return;
        }

        bool allReady = Manager.gamePlayers.Count > 0
                     && Manager.gamePlayers.All(p => p != null && p.ready);

        startGameButton.interactable = allReady && isHost;
    }

    // ── Test Mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the test mode button in the UI.
    /// </summary>
    public void ToggleTestMode()
    {
        isTestMode = !isTestMode;
        UpdateTestModeIndicator();
        CheckIfAllReady();
        Debug.Log($"[Lobby] Test mode: {(isTestMode ? "ON" : "OFF")}");
    }

    private void UpdateTestModeIndicator()
    {
        if (testModeIndicator != null)
            testModeIndicator.SetActive(isTestMode);
    }

    public bool IsTestMode => isTestMode;

    // ── Role Switching ────────────────────────────────────────────────────────

    /// <summary>
    /// Switches the local player's role between Hunter and Vandalist.
    /// Called by a UI button.
    /// The button is greyed out by UpdateSwitchSidesButton() when the target side is full.
    /// </summary>
    public void ToggleLocalPlayerRole()
    {
        if (localPlayerController == null) return;
        if (!CanLocalPlayerSwitch()) return;

        PlayerListItem localItem = totalPlayerbaseListItems
            .FirstOrDefault(i => i.connectionID == localPlayerController.connectionID);

        if (localItem != null)
        {
            PlayerRole newRole = hunterPlayerListItems.Contains(localItem) ? PlayerRole.Vandalist : PlayerRole.Hunter;

            localPlayerController.CmdChangeRole(newRole);
        }
    }

    /// <summary>
    /// Whether the local player can switch sides.
    /// Not possible if the target side is already full.
    /// </summary>
    private bool CanLocalPlayerSwitch()
    {
        if (localPlayerController == null) return false;

        PlayerListItem localItem = totalPlayerbaseListItems
            .FirstOrDefault(i => i.connectionID == localPlayerController.connectionID);

        if (localItem == null) return false;

        if (hunterPlayerListItems.Contains(localItem))
            return vandalistPlayerListItems.Count < vandalistMaxNumber;

        if (vandalistPlayerListItems.Contains(localItem))
            return hunterPlayerListItems.Count < hunterMaxNumber;

        return false;
    }

    /// <summary>
    /// Updates the interactable state of the Switch Sides button.
    /// Called whenever the player lists change.
    /// </summary>
    private void UpdateSwitchSidesButton()
    {
        if (switchSidesButton == null) return;
        switchSidesButton.interactable = CanLocalPlayerSwitch();
    }

    public void SwitchPlayerRole(PlayerListItem item)
    {
        if (hunterPlayerListItems.Contains(item))
        {
            hunterPlayerListItems.Remove(item);
            item.transform.SetParent(vandalistPlayerListViewContent.transform);
            vandalistPlayerListItems.Add(item);
            item.SetPlayerValues(PlayerRole.Vandalist);
        }
        else if (vandalistPlayerListItems.Contains(item))
        {
            vandalistPlayerListItems.Remove(item);
            item.transform.SetParent(hunterPlayerListViewContent.transform);
            hunterPlayerListItems.Add(item);
            item.SetPlayerValues(PlayerRole.Hunter);
        }

        item.transform.localScale = Vector3.one;
        UpdateRoleCountTexts();
    }

    // ── Role Display ──────────────────────────────────────────────────────────

    public void UpdateRoleCountTexts()
    {
        int hCount = hunterPlayerListItems.Count;
        int vCount = vandalistPlayerListItems.Count;

        Color hColor = hCount > hunterMaxNumber ? overshootTextColor : defaultTextColor;
        Color vColor = vCount > vandalistMaxNumber ? overshootTextColor : defaultTextColor;

        SetCounter(hunterCurrent, hCount.ToString(), hColor);
        SetCounter(hunterMax, hunterMaxNumber.ToString(), hColor);
        SetCounter(vandalistCurrent, vCount.ToString(), vColor);
        SetCounter(vandalistMax, vandalistMaxNumber.ToString(), vColor);

        // Re-evaluate the Switch Sides button after every list change
        UpdateSwitchSidesButton();
    }

    private static void SetCounter(TextMeshProUGUI text, string value, Color color)
    {
        if (text == null) return;
        text.text = value;
        text.color = color;
    }
}
