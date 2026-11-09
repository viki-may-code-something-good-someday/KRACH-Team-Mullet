using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CameraController : NetworkBehaviour
{
    [Header("References")]
    public Transform playerBody;
    public Transform cameraPivot;
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    public float mouseSensitivity = 1.5f;

    // Gameplay scene path comes directly from the NetworkManager – no duplicate field needed.
    private string GameplayScene => (NetworkManager.singleton as CustomNetworkManager)?.GameplayScene ?? string.Empty;

    private float xRotation = 0f;
    private bool wasInGameplay = false;

    // ── Start ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Always release the cursor in the lobby.
        // Locking only happens when entering the gameplay scene.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ── Local Player Setup ────────────────────────────────────────────────────

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Shake, head bob, landing dip etc. – only for the own camera.
        CameraEffects.Attach(playerCamera != null ? playerCamera : GetComponentInChildren<Camera>());
    }

    // ── OnStartClient: Clean Up Remote Players ────────────────────────────────

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isLocalPlayer) return;

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null) cam.enabled = false;

        AudioListener audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null) audioListener.enabled = false;

        FMODUnity.StudioListener fmodListener = GetComponentInChildren<FMODUnity.StudioListener>();
        if (fmodListener != null) fmodListener.enabled = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!isLocalPlayer) return;

        bool inGameplay = SceneManager.GetActiveScene().path == GameplayScene;

        // Lobby → gameplay: lock the cursor
        if (inGameplay && !wasInGameplay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        // Gameplay → lobby: release the cursor, reset the rotation
        else if (!inGameplay && wasInGameplay)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            xRotation = 0f;
        }

        wasInGameplay = inGameplay;

        if (!inGameplay) return;

        HandleCursorToggle();
        HandleCameraRotation();
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.T))
            SwitchCursorMode();
    }

    public void SwitchCursorMode()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ── Camera Rotation ───────────────────────────────────────────────────────

    private void HandleCameraRotation()
    {
        if (Mouse.current == null) return;
        if (Cursor.lockState == CursorLockMode.None) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseDelta.x);
    }

    // ── Camera Shake ──────────────────────────────────────────────────────────

    /// <summary>
    /// Compatibility API. Runs through the trauma system of CameraEffects – the old
    /// coroutine shake permanently displaced the camera when calls overlapped.
    /// New callers please use CameraEffects.Shake / ShakeAt directly.
    /// </summary>
    public void StartCameraShake(float duration, float magnitude)
    {
        if (!isLocalPlayer) return;
        CameraEffects.Shake(Mathf.Clamp01(magnitude * 4f + duration * 0.2f));
    }
}
