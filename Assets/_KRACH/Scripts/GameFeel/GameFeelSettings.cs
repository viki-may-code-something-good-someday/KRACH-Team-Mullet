using UnityEngine;

/// <summary>
/// Central switches and strengths for all game-feel effects (camera, HUD, object feedback).
/// Every feature can be turned off here individually without touching code.
///
/// Loaded from Resources/GameFeelSettings. If the asset is missing, the default values from
/// this class are used – the game also runs without the asset.
/// </summary>
[CreateAssetMenu(fileName = "GameFeelSettings", menuName = "Game/Game Feel Settings")]
public class GameFeelSettings : ScriptableObject
{
    private const string ResourcePath = "GameFeelSettings";
    private static GameFeelSettings instance;

    public static GameFeelSettings Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = Resources.Load<GameFeelSettings>(ResourcePath);
            if (instance == null)
            {
                instance = CreateInstance<GameFeelSettings>();
                instance.name = "GameFeelSettings (Defaults)";
            }
            return instance;
        }
    }

    // ── Camera ───────────────────────────────────────────────────────────────

    [Header("Camera Shake (trauma based)")]
    public bool cameraShake = true;
    [Tooltip("Maximum rotation in degrees at trauma = 1.")]
    public float maxShakeAngle = 3f;
    [Tooltip("Maximum position offset in meters at trauma = 1.")]
    public float maxShakeOffset = 0.06f;
    [Tooltip("Frequency of the Perlin noise. Higher = more jittery.")]
    public float shakeFrequency = 22f;
    [Tooltip("How much trauma is removed per second.")]
    public float traumaDecay = 1.6f;
    [Tooltip("Range for world events (wall breaks, speaker destroyed) – further away = weaker.")]
    public float worldShakeRadius = 14f;

    [Header("Head Bob")]
    public bool headBob = true;
    [Tooltip("Vertical movement in meters while walking.")]
    public float bobAmplitudeY = 0.03f;
    [Tooltip("Sideways movement in meters while walking.")]
    public float bobAmplitudeX = 0.018f;
    [Tooltip("Meters per stride (two steps). Smaller = faster bobbing.")]
    public float bobStrideLength = 3.6f;
    [Tooltip("Above this speed the bobbing stops getting faster (prevents jitter while sprinting).")]
    public float bobMaxSpeed = 9f;

    [Header("Landing")]
    public bool landingDip = true;
    [Tooltip("How much the camera dips per m/s of fall speed.")]
    public float landingDipPerSpeed = 0.12f;
    public float maxLandingDipImpulse = 2.2f;
    [Tooltip("From this fall speed (m/s) on, landing additionally adds a small shake.")]
    public float hardLandingSpeed = 11f;
    public float hardLandingTrauma = 0.25f;

    [Header("Strafe Tilt")]
    public bool strafeTilt = true;
    [Tooltip("Camera roll in degrees while strafing.")]
    public float strafeTiltAngle = 1.2f;
    public float strafeTiltSmoothing = 8f;

    [Header("FOV Kicks")]
    public bool fovKicks = true;
    [Tooltip("How far the hunter zooms in while charging (degrees).")]
    public float hunterChargeZoom = 7f;
    public float accuseReleaseFovKick = 8f;
    public float doorTeleportFovKick = 14f;

    [Header("Recoil")]
    public bool cameraKick = true;
    [Tooltip("Upward camera recoil when punching (degrees).")]
    public float punchKickAngle = 1.6f;
    public float accuseKickAngle = 2.5f;

    [Header("Trauma per Event (0–1)")]
    public float punchHitTrauma = 0.16f;
    public float punchMissTrauma = 0.04f;
    public float wallBreakTrauma = 0.45f;
    public float soundBoxDestroyedTrauma = 0.55f;
    public float caughtTrauma = 0.75f;
    public float catchSuccessTrauma = 0.3f;
    public float chargeFullTrauma = 0.06f;

    // ── World / Objects ──────────────────────────────────────────────────────

    [Header("Object Feedback")]
    [Tooltip("Billboards get squashed when hit.")]
    public bool squashOnHit = true;
    [Tooltip("Walls shake when hit.")]
    public bool wallHitShake = true;
    [Tooltip("Walls get darker as their health drops.")]
    public bool wallDamageTint = true;
    [Range(0f, 1f)] public float wallMaxDarken = 0.35f;
    [Tooltip("Debris shrinks away briefly instead of vanishing abruptly.")]
    public bool debrisShrinkOut = true;

    [Header("Debris Pushing")]
    [Tooltip("Players can kick wall debris around while walking.")]
    public bool pushDebris = true;
    public float debrisPushSpeed = 5f;

    // ── HUD ──────────────────────────────────────────────────────────────────

    [Header("HUD")]
    [Tooltip("Big announcements (round end, speaker destroyed, player caught).")]
    public bool hudBanners = true;
    [Tooltip("Colored full-screen flashes (e.g. red when getting caught).")]
    public bool screenFlash = true;
    [Tooltip("Short hit text in the middle of the screen (hunter: 'CAUGHT!').")]
    public bool hitConfirmText = true;
    [Tooltip("Respawn countdown for caught vandalists.")]
    public bool respawnCountdown = true;
    [Tooltip("The speaker counter pops when a speaker is destroyed.")]
    public bool counterPop = true;

    [Header("Timer")]
    public bool timerWarning = true;
    [Tooltip("The timer turns yellow from here on.")]
    public float timerWarningSeconds = 30f;
    [Tooltip("The timer turns red and pulses every second from here on.")]
    public float timerCriticalSeconds = 10f;
    public Color timerWarningColor = new Color(1f, 0.8f, 0.2f);
    public Color timerCriticalColor = new Color(1f, 0.25f, 0.2f);

    [Header("Lobby")]
    [Tooltip("The ready checkmark pops briefly.")]
    public bool lobbyReadyPop = true;

    // ── Round End ────────────────────────────────────────────────────────────

    [Header("Round End")]
    [Tooltip("Short slow motion as soon as one side wins.")]
    public bool endSlowMotion = true;
    [Range(0.05f, 1f)] public float endSlowMotionScale = 0.3f;
    [Tooltip("How long (real time) it takes until time runs at normal speed again.")]
    public float endSlowMotionRecover = 2.5f;

    // ── Colors ───────────────────────────────────────────────────────────────

    [Header("Colors")]
    public Color hunterColor = new Color(0.35f, 0.65f, 1f);
    public Color vandalistColor = new Color(1f, 0.45f, 0.2f);
    public Color caughtFlashColor = new Color(1f, 0.1f, 0.1f, 0.45f);
}
