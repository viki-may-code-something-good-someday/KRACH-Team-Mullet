using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    EndMenu,
    Playing,
    Paused,
    Sequence,
    GameOver
}

public enum WinningSide
{
    None,
    Hunter,
    Vandalist
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SyncVar(hook = nameof(OnGameStateChanged))]
    private GameState currentState;

    public GameState CurrentState => currentState;

    public float maxPlaytimeInSeconds;

    public bool testInEditor;
    public GameObject networkManager;

    // ── Timer ─────────────────────────────────────────────────────────────────

    // Match end as an absolute NetworkTime timestamp: NetworkTime runs in sync on the server
    // and all clients, so every client shows the same remaining time.
    [SyncVar]
    private double matchEndTime;

    // Remaining time while paused or after the round ended (frozen display). -1 = not frozen.
    // Without it the timer jumped back to the full playtime as soon as the game was over.
    [SyncVar]
    private float frozenTimeRemaining = -1f;

    /// <summary>Remaining round time in seconds, valid on the server and all clients.</summary>
    public float TimeRemaining
    {
        get
        {
            if (frozenTimeRemaining >= 0f) return frozenTimeRemaining;
            if (currentState != GameState.Playing || matchEndTime <= 0d) return maxPlaytimeInSeconds; // round not started / not synced yet
            return Mathf.Max(0f, (float)(matchEndTime - NetworkTime.time));
        }
    }

    /// <summary>Time already played in seconds.</summary>
    public float CurrentPlaytime => maxPlaytimeInSeconds - TimeRemaining;

    // ── SoundBox Counter ──────────────────────────────────────────────────────

    [SyncVar] private int totalSoundBoxCount;
    [SyncVar] private int remainingSoundBoxCount;

    public int TotalSoundBoxCount => totalSoundBoxCount;
    public int RemainingSoundBoxCount => remainingSoundBoxCount;
    public int DestroyedSoundBoxCount => totalSoundBoxCount - remainingSoundBoxCount;

    // ── Win Condition: Soundboxes ────────────────────────────────────────────

    [Header("Win Condition - Soundboxes")]
    [Tooltip("When a SoundBox from this list is destroyed, it gets removed. " +
             "Once the list is empty, the vandalists have won.")]
    [SerializeField] private List<SoundBox> trackedSoundBoxes = new List<SoundBox>();

#if UNITY_EDITOR
    [Button("Find All SoundBoxes In Scene")]
    private void FindAllSoundBoxesInScene()
    {
        trackedSoundBoxes = new List<SoundBox>(
            FindObjectsByType<SoundBox>(FindObjectsSortMode.None));
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GameManager] Found {trackedSoundBoxes.Count} SoundBox(es) in the scene.");
    }
#endif

    // ── Win Screen ────────────────────────────────────────────────────────────

    [Header("Win Screen")]
    [Scene]
    [SerializeField] private string winScreenScene;
    [Tooltip("Wait time (real time) after the game ends before switching to the win screen scene " +
             "(lets the game-over sound/UI play out for a moment).")]
    [SerializeField] private float winScreenTransitionDelay = 3f;

    private float defaultFixedDeltaTime;
    private Tween slowMotionTween;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (testInEditor && NetworkManager.singleton == null && networkManager != null)
            StartEditorTestSession();
    }

    /// <summary>
    /// Starts a host directly from the gameplay scene (without menu/lobby).
    /// With Steam via a lobby, without Steam as a plain local host.
    /// </summary>
    private void StartEditorTestSession()
    {
        GameObject nm = Instantiate(networkManager);
        CustomNetworkManager manager = nm.GetComponent<CustomNetworkManager>();
        manager.onlineScene = SceneManager.GetActiveScene().path; // the path, not Scene.ToString()

        // SteamLobby.HostLobby starts the host itself once the lobby is created –
        // an additional StartHost() here would have started the host twice.
        SteamLobby lobby = nm.GetComponent<SteamLobby>();
        if (lobby != null && SteamManager.Initialized)
            StartCoroutine(HostLobbyNextFrame(lobby)); // SteamLobby only registers its callbacks in Start()
        else
            manager.StartHost();
    }

    private static IEnumerator HostLobbyNextFrame(SteamLobby lobby)
    {
        yield return null;
        lobby.HostLobby();
    }

    private void OnEnable()
    {
        PlayerRoleSetup.OnCaughtStateChangedServer += HandlePlayerCaughtStateChanged;
        SoundBox.OnDestroyedServer += HandleSoundBoxDestroyed;
    }

    private void OnDisable()
    {
        PlayerRoleSetup.OnCaughtStateChangedServer -= HandlePlayerCaughtStateChanged;
        SoundBox.OnDestroyedServer -= HandleSoundBoxDestroyed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Slow motion/pause must never carry over into the next scene (win screen, lobby).
        slowMotionTween?.Kill();
        Time.timeScale = 1f;
        if (defaultFixedDeltaTime > 0f) Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    /// <summary>
    /// OnStartServer instead of Start(): for scene objects Start() can run before Mirror has
    /// spawned the object, and then isServer is still false.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        trackedSoundBoxes.RemoveAll(box => box == null);
        StartGame();
    }

    private void Update()
    {
        if (!isServer) { return; }
        if (currentState == GameState.Playing)
        {
            UpdateInternalTimer();
        }
    }

    // ── Server API ─────────────────────────────────────────────────────────────

    [Server]
    public void StartGame()
    {
        matchEndTime = NetworkTime.time + maxPlaytimeInSeconds;
        frozenTimeRemaining = -1f;

        totalSoundBoxCount = trackedSoundBoxes.Count;
        remainingSoundBoxCount = trackedSoundBoxes.Count;

        SetState(GameState.Playing);
    }

    [Server]
    public void PauseGame()
    {
        if (currentState != GameState.Playing) return;

        frozenTimeRemaining = TimeRemaining;
        SetState(GameState.Paused);
        RpcSetTimeScale(0f);
    }

    [Server]
    public void ResumeGame()
    {
        if (currentState != GameState.Paused) return;

        // Shift the end time by the paused duration – otherwise the pause would eat round time.
        matchEndTime = NetworkTime.time + Mathf.Max(0f, frozenTimeRemaining);
        frozenTimeRemaining = -1f;
        SetState(GameState.Playing);
        RpcSetTimeScale(1f);
    }

    /// <summary>
    /// Ends the game server-authoritatively for the given winning side.
    /// Idempotent: does nothing after the first call if the game is already over.
    /// Can also be called directly from the Inspector (e.g. debug button/UnityEvent) with a
    /// WinningSide dropdown parameter.
    /// </summary>
    [Server]
    public void EndGame(WinningSide winner)
    {
        if (currentState == GameState.GameOver) return;

        frozenTimeRemaining = TimeRemaining;
        SetState(GameState.GameOver);
        RpcGameOver(winner);

        CustomNetworkManager manager = NetworkManager.singleton as CustomNetworkManager;
        if (manager != null)
            manager.SetLastWinner(winner);
        else
            Debug.LogError("[GameManager] No CustomNetworkManager found – can't pass the winner to the win screen scene.");

        StartCoroutine(TransitionToWinScreenAfterDelay());
    }

    [Server]
    private IEnumerator TransitionToWinScreenAfterDelay()
    {
        // Real time: the round-end slow motion would otherwise delay the switch a lot.
        yield return new WaitForSecondsRealtime(winScreenTransitionDelay);

        CustomNetworkManager manager = NetworkManager.singleton as CustomNetworkManager;
        if (manager == null || string.IsNullOrEmpty(winScreenScene))
        {
            Debug.LogError("[GameManager] No CustomNetworkManager or winScreenScene not set – " +
                           "can't switch to the win screen scene.");
            yield break;
        }

        manager.ServerChangeScene(winScreenScene);
    }

    [Server]
    private void SetState(GameState _newState)
    {
        currentState = _newState;
    }

    // ── Win Condition: Timer ────────────────────────────────────────────────────

    [Server]
    private void UpdateInternalTimer()
    {
        if (NetworkTime.time >= matchEndTime)
        {
            EndGame(WinningSide.Hunter);
        }
    }

    // ── Win Condition: Soundboxes ────────────────────────────────────────────

    [Server]
    private void HandleSoundBoxDestroyed(SoundBox box)
    {
        if (!isServer) return;
        if (currentState != GameState.Playing) return;
        if (!trackedSoundBoxes.Remove(box)) return;

        trackedSoundBoxes.RemoveAll(b => b == null);
        remainingSoundBoxCount = trackedSoundBoxes.Count;

        if (remainingSoundBoxCount == 0)
            EndGame(WinningSide.Vandalist);
        else
            RpcSoundBoxDestroyed(remainingSoundBoxCount);
    }

    // ── Win Condition: All Vandalists Caught ────────────────────────────────────

    [Server]
    private void HandlePlayerCaughtStateChanged(PlayerRoleSetup setup, bool caught)
    {
        if (!isServer) return;
        if (currentState != GameState.Playing) return;

        if (caught)
        {
            PlayerObjectController poc = setup != null ? setup.GetComponent<PlayerObjectController>() : null;
            string playerName = poc != null && !string.IsNullOrEmpty(poc.playerName) ? poc.playerName : "A vandalist";
            RpcPlayerCaught(playerName, setup != null ? setup.netIdentity : null);
        }

        CheckAllVandalistsCaughtWinCondition();
    }

    /// <summary>
    /// Hunters win when no vandalist is active (not caught) anymore.
    /// Deliberately counts whether there are any vandalists at all – otherwise a match
    /// without vandalists (e.g. a test case) would immediately count as a hunter win.
    /// NOTE: as long as vandalists respawn automatically (PlayerRoleSetup.allowRespawn),
    /// "caught" is only a temporary state – this check only becomes reliably final once
    /// auto-respawn is disabled as planned.
    /// </summary>
    [Server]
    private void CheckAllVandalistsCaughtWinCondition()
    {
        CustomNetworkManager manager = NetworkManager.singleton as CustomNetworkManager;
        if (manager == null) return;

        int vandalistCount = 0;
        bool anyActiveVandalist = false;

        foreach (PlayerObjectController player in manager.gamePlayers)
        {
            if (player == null || player.playerRole != PlayerRole.Vandalist) continue;
            vandalistCount++;

            PlayerRoleSetup setup = player.GetComponent<PlayerRoleSetup>();
            if (setup == null || !setup.IsCaught)
            {
                anyActiveVandalist = true;
                break;
            }
        }

        if (vandalistCount == 0) return; // no vandalists in the match, ignore the check
        if (!anyActiveVandalist) EndGame(WinningSide.Hunter);
    }

    // ── RPCs — broadcast to all clients ───────────────────────────────────────

    [ClientRpc]
    private void RpcGameOver(WinningSide winner)
    {
        bool vandalistsWon = winner == WinningSide.Vandalist;

        //RuntimeManager.PlayOneShot(vandalistsWon ? "event:/SFX/GameWon" : "event:/SFX/GameOver");

        // Win/loss from the LOCAL player's point of view – previously everyone saw the result
        // from the vandalists' perspective (a winning hunter got "lost").
        if (UI_GameOver.Instance != null)
            UI_GameOver.Instance.SetGameOverScreen(LocalPlayerWon(winner));

        GameFeelSettings settings = GameFeelSettings.Instance;
        GameFeelHUD.Banner(
            vandalistsWon ? "VANDALISTS WIN!" : "HUNTERS WIN!",
            vandalistsWon ? settings.vandalistColor : settings.hunterColor,
            winScreenTransitionDelay);
        CameraEffects.Shake(0.4f);
        PlayEndSlowMotion(settings);

        Debug.Log($"[GameManager] Game Over — {winner} wins.");
    }

    private static bool LocalPlayerWon(WinningSide winner)
    {
        PlayerObjectController localPlayer = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<PlayerObjectController>()
            : null;
        if (localPlayer == null) return false;

        return (winner == WinningSide.Hunter && localPlayer.playerRole == PlayerRole.Hunter)
            || (winner == WinningSide.Vandalist && localPlayer.playerRole == PlayerRole.Vandalist);
    }

    /// <summary>Short slow motion that ramps back to 1 over the wait time until the win screen.</summary>
    private void PlayEndSlowMotion(GameFeelSettings settings)
    {
        if (!settings.endSlowMotion) return;

        slowMotionTween?.Kill();
        SetTimeScale(settings.endSlowMotionScale);
        slowMotionTween = DOTween.To(() => Time.timeScale, SetTimeScale, 1f, settings.endSlowMotionRecover)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        // Scale the physics step along, otherwise debris & billboards stutter in slow motion.
        Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(0.05f, scale);
    }

    [ClientRpc]
    private void RpcSoundBoxDestroyed(int remaining)
    {
        string text = remaining == 1
            ? "SPEAKER DESTROYED!\n<size=60%>Only one left!</size>"
            : $"SPEAKER DESTROYED!\n<size=60%>{remaining} left</size>";
        GameFeelHUD.Banner(text, GameFeelSettings.Instance.vandalistColor, 1.6f);
    }

    [ClientRpc]
    private void RpcPlayerCaught(string playerName, NetworkIdentity caughtPlayer)
    {
        // The caught player gets their own feedback via PlayerRoleSetup.
        if (caughtPlayer != null && caughtPlayer.isOwned) return;

        GameFeelHUD.Banner($"{playerName} got caught!", GameFeelSettings.Instance.hunterColor, 1.4f);
    }

    [ClientRpc]
    private void RpcSetTimeScale(float _timeScale)
    {
        Time.timeScale = _timeScale;
    }

    // ── SyncVar hook — fires on all clients when state changes ─────────────────

    private void OnGameStateChanged(GameState _oldState, GameState _newState)
    {
        Debug.Log($"[GameManager] State: {_oldState} → {_newState}");
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
