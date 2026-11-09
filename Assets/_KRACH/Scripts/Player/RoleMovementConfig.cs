using UnityEngine;

/// <summary>
/// All movement-related values of a role as a ScriptableObject.
/// Create a separate asset instance per role (right click → Game → Role Movement Config).
/// This way values can be tweaked in the editor without changing code.
/// </summary>
[CreateAssetMenu(fileName = "New RoleMovementConfig", menuName = "Game/Role Movement Config")]
public class RoleMovementConfig : ScriptableObject
{
    [Header("Walk")]
    public float walkSpeed = 6f;

    [Header("Acceleration (Momentum)")]
    [Tooltip("Ground acceleration in m/s². Very high values (e.g. 1000) = old, instant behavior.")]
    public float groundAcceleration = 70f;
    [Tooltip("Ground deceleration without input in m/s².")]
    public float groundDeceleration = 55f;
    [Tooltip("Change of direction in the air in m/s². Smaller = more momentum, less air control.")]
    public float airAcceleration = 28f;
    [Tooltip("Deceleration in the air without input in m/s². Small = jumps keep their momentum.")]
    public float airDeceleration = 6f;

    [Header("Sprint")]
    public bool canSprint = true;
    public float baseSprintSpeed = 9f;
    public float maxSprintSpeed = 15f;
    public float sprintBurstThreshold = 2.5f;
    public float sprintAcceleration = 1.7f;
    public float sprintBurstAcceleration = 14f;
    public float sprintDecaySpeed = 3f;

    [Header("Jump")]
    [Tooltip("Target jump height in meters (for a full ascent, riseGravityMultiplier taken into account).")]
    public float jumpHeight = 1.6f;
    [Tooltip("Gravity multiplier during the ascent. 1 = normal/realistic gravity." +
             " Lower = a bit more hang time, higher = an even more direct, shorter ascent.")]
    public float riseGravityMultiplier = 1f;
    [Tooltip("Gravity multiplier while falling. Clearly higher than riseGravityMultiplier" +
             " gives a snappy, direct landing feel instead of a floaty fall.")]
    public float fallGravityMultiplier = 2.2f;
    [Tooltip("Gravity multiplier right after releasing the jump button early during the ascent." +
             " Should be >= fallGravityMultiplier so short hops get cut off especially snappily.")]
    public float jumpCutGravityMultiplier = 3.2f;
    [Tooltip("How much of the remaining upward velocity is cut immediately when releasing early." +
             " 1 = no cut, 0.4 = 60% of the remaining velocity cut off immediately.")]
    [Range(0f, 1f)]
    public float jumpCutVelocityMultiplier = 0.45f;
    [Tooltip("Coyote time: how long (s) you can still jump after leaving a ledge. 0 = off.")]
    public float coyoteTime = 0.12f;
    [Tooltip("Jump buffer: a jump pressed too early (s) before landing is still executed. 0 = off.")]
    public float jumpBufferTime = 0.12f;

    [Header("Crouch")]
    public float crouchSpeedMultiplier = 0.5f;
    [Tooltip("CharacterController height while crouching.")]
    public float crouchHeight = 1.0f;
    [Tooltip("CharacterController height while standing.")]
    public float standHeight = 2.0f;
    [Tooltip("Local Y position of the CameraHolder while crouching – relative to the root pivot," +
             " which does not sit at the feet. Must be small enough that the camera stays inside the" +
             " crouch capsule, otherwise you can see through low ceilings. Limit:" +
             " crouchCameraY - groundCheck.localPosition.y < crouchHeight." +
             " PlayerMovement.ApplyConfig warns if the value doesn't fit.")]
    public float crouchCameraY = -0.04f;
    [Tooltip("Local Y position of the CameraHolder while standing.")]
    public float standCameraY = 0.75f;
    public float crouchTransitionSpeed = 12f;

    [Header("Fall")]
    [Tooltip("Base gravity in m/s². Combined with riseGravityMultiplier / fallGravityMultiplier /" +
             " jumpCutGravityMultiplier – see header 'Jump'.")]
    public float baseFallGravity = 20f;
    [Tooltip("Maximum fall speed (terminal velocity) in m/s. Prevents unrealistically fast" +
             " falling during long drops. NOTE: replaces the old field 'maxFallGravity' –" +
             " for existing assets please re-check/set the value once in the Inspector.")]
    public float maxFallSpeed = 25f;

    [Header("Camera FOV")]
    public float normalFOV = 60f;
    public float sprintFOV = 70f;
    public float fovChangeSpeed = 8f;
}
