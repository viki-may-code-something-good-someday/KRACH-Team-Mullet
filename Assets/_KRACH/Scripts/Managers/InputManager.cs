using UnityEngine;
using UnityEngine.InputSystem;

// Input snapshot for a single frame, consumed by gameplay systems.
public struct InputData
{
    public Vector2 MoveInput;
    public Vector2 LookInput;
    public bool SprintHeld;
    public bool ActionPressed;
    public bool ActionHeld;
    public bool InteractPressed;
    public bool JumpPressed;
    public bool JumpHeld;
    public bool CrouchHeld;
    public PlayerRole Role;
}

// Reads local input each frame and fires OnInputChanged when anything changes.
// Subscribe to OnInputChanged or poll CurrentInput directly.
// Requires a PlayerInput component with Action Map "Player" and actions:
// Move, Look, Sprint, Action, Interact, Jump, Crouch
//
// Runs before all gameplay scripts so they read the current input in the same frame
// (otherwise, depending on execution order, there's one frame of input latency).
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event System.Action<InputData> OnInputChanged;
    public InputData CurrentInput { get; private set; }

    /// <summary>
    /// Blocks all gameplay input (e.g. while the pause menu is open). Without the block a click
    /// on "Resume" also triggered a punch/accusation and WASD kept moving in the menu.
    /// </summary>
    public bool InputBlocked { get; set; }

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction actionAction;
    private InputAction interactAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        sprintAction = playerInput.actions["Sprint"];
        actionAction = playerInput.actions["Action"];
        interactAction = playerInput.actions["Interact"];
        jumpAction = playerInput.actions["Jump"];
        crouchAction = playerInput.actions["Crouch"];
    }

    private void Update()
    {
        ReadAndBroadcastInput();
    }

    public void SetRole(PlayerRole _newRole)
    {
        InputData updated = CurrentInput;
        updated.Role = _newRole;
        CurrentInput = updated;
        OnInputChanged?.Invoke(CurrentInput);
        Debug.Log($"[InputManager] Role set to: {_newRole}");
    }

    private void ReadAndBroadcastInput()
    {
        if (InputBlocked)
        {
            InputData blocked = new InputData { Role = CurrentInput.Role };
            if (HasInputChanged(CurrentInput, blocked))
            {
                CurrentInput = blocked;
                OnInputChanged?.Invoke(CurrentInput);
            }
            return;
        }

        InputData newInput = new InputData
        {
            MoveInput = moveAction.ReadValue<Vector2>(),
            LookInput = lookAction.ReadValue<Vector2>(),
            SprintHeld = sprintAction.IsPressed(),
            ActionPressed = actionAction.WasPressedThisFrame(),
            ActionHeld = actionAction.IsPressed(),
            InteractPressed = interactAction.WasPressedThisFrame(),
            JumpPressed = jumpAction.WasPressedThisFrame(),
            JumpHeld = jumpAction.IsPressed(),
            CrouchHeld = crouchAction.IsPressed(),
            Role = CurrentInput.Role  // the role is kept locally, not overwritten
        };

        if (HasInputChanged(CurrentInput, newInput))
        {
            CurrentInput = newInput;
            OnInputChanged?.Invoke(CurrentInput);
        }
    }

    private bool HasInputChanged(InputData _previous, InputData _current)
    {
        return _previous.MoveInput != _current.MoveInput
            || _previous.LookInput != _current.LookInput
            || _previous.SprintHeld != _current.SprintHeld
            || _previous.ActionPressed != _current.ActionPressed
            || _previous.ActionHeld != _current.ActionHeld
            || _previous.InteractPressed != _current.InteractPressed
            || _previous.JumpPressed != _current.JumpPressed
            || _previous.JumpHeld != _current.JumpHeld
            || _previous.CrouchHeld != _current.CrouchHeld;
    }
}
