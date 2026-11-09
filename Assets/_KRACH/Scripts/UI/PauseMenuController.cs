using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;


public class PauseMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Button/GameObject only the host should be able to use, e.g. 'End Game for Everyone'.")]
    [SerializeField] private GameObject hostOnlyControls;

    [Header("Settings")]
    [Tooltip("Only allow pausing while in the gameplay scene, not in the lobby.")]
    [SerializeField] private bool restrictToGameplayScene = true;

    private bool isPaused;

    private string GameplayScene =>
        (NetworkManager.singleton as CustomNetworkManager)?.GameplayScene ?? string.Empty;

    private bool InGameplayScene => SceneManager.GetActiveScene().path == GameplayScene;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Start()
    {
        SetPaused(false);
    }

    private void OnDestroy()
    {
        // When the scene changes while the menu is open, the input block must not stay stuck.
        if (isPaused && InputManager.Instance != null) InputManager.Instance.InputBlocked = false;
    }

    private void Update()
    {
        if (restrictToGameplayScene && !InGameplayScene) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    // ── Pause Toggle ─────────────────────────────────────────────────────────

    public void TogglePause() => SetPaused(!isPaused);

    public void SetPaused(bool paused)
    {
        isPaused = paused;

        if (pausePanel != null) pausePanel.SetActive(paused);

        // Only host-relevant controls (e.g. "End Game") are shown to the host.
        // NetworkServer.active is true for both dedicated server and host.
        if (hostOnlyControls != null)
            hostOnlyControls.SetActive(paused && NetworkServer.active);

        // Block gameplay input while the menu is open – otherwise clicking "Resume" also
        // punched/accused and WASD kept moving the player.
        if (InputManager.Instance != null)
            InputManager.Instance.InputBlocked = paused;

        // Outside the gameplay scene (e.g. win screen) the mouse must stay free,
        // otherwise the buttons there aren't clickable.
        bool lockCursor = !paused && InGameplayScene;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
    }

    /// <summary>Called by the "Resume" button.</summary>
    public void Resume() => SetPaused(false);

    // ── Session Control ──────────────────────────────────────────────────────

    public void EndGameAsHost()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[PauseMenuController] EndGameAsHost() called, but not host/server – ignored.");
            return;
        }

        CustomNetworkManager manager = NetworkManager.singleton as CustomNetworkManager;
        if (manager == null)
        {
            Debug.LogError("[PauseMenuController] No CustomNetworkManager found – can't return to the lobby.");
            return;
        }

        manager.ReturnToLobby();
        SetPaused(false);
    }

    /// <summary>
    /// Called by the "Leave Game" button for non-host clients: disconnects only
    /// the local player, the session continues for everyone else.
    /// </summary>
    public void LeaveGameAsClient()
    {
        if (NetworkServer.active)
        {
            Debug.LogWarning("[PauseMenuController] LeaveGameAsClient() called as host – " +
                             "use EndGameAsHost() instead.");
            return;
        }

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null)
        {
            Debug.LogError("[PauseMenuController] No NetworkManager found – can't leave the session.");
            return;
        }

        manager.StopClient();
    }

    /// <summary>Called by the "Quit to Desktop" button.</summary>
    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
