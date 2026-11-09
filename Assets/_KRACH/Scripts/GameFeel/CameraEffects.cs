using UnityEngine;

/// <summary>
/// Purely local camera effects for the own player: trauma shake, head bob, landing dip,
/// strafe tilt, recoil kicks and FOV kicks. All strengths/switches live in GameFeelSettings.
///
/// Sits on the player camera and is the only writer of its localPosition/localRotation.
/// Pitch/yaw still come from the CameraController via the parent (CameraTarget) – the effects
/// are pure offsets on top. Gameplay raycasts go through <see cref="GetAimRay"/> so that
/// shake and bob don't wobble the hit direction.
///
/// Attached at runtime by the CameraController for the local player – nothing needs to be
/// set up in the prefab.
/// </summary>
[DisallowMultipleComponent]
public class CameraEffects : MonoBehaviour
{
    public static CameraEffects Local { get; private set; }

    // Converts the "desired amplitude" into the initial velocity of the respective spring,
    // so the values in GameFeelSettings can be thought of in degrees or meters.
    private const float KickImpulseScale = 22f;
    private const float FovImpulseScale = 18f;
    private const float MaxDeltaTime = 0.05f;

    private PlayerMovement movement;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float noiseSeed;

    private float trauma;
    private float bobPhase;
    private float bobWeight;
    private float roll;

    private float dipOffset, dipVelocity;
    private float kickPitch, kickPitchVelocity;
    private float kickYaw, kickYawVelocity;
    private float fovKick, fovKickVelocity;

    /// <summary>Sustained FOV offset (e.g. hunter charge zoom). Whoever sets it resets it.</summary>
    public float SustainedFovOffset { get; set; }

    /// <summary>Added on top of the base FOV by PlayerMovement.</summary>
    public float FovOffset => GameFeelSettings.Instance.fovKicks ? fovKick + SustainedFovOffset : 0f;

    // ── Setup ────────────────────────────────────────────────────────────────

    public static CameraEffects Attach(Camera camera)
    {
        if (camera == null) return null;

        if (!camera.TryGetComponent(out CameraEffects fx))
            fx = camera.gameObject.AddComponent<CameraEffects>();

        return fx;
    }

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        noiseSeed = Random.value * 100f;

        movement = GetComponentInParent<PlayerMovement>();
        if (movement != null) movement.Landed += OnLanded;

        Local = this;
    }

    private void OnDestroy()
    {
        if (movement != null) movement.Landed -= OnLanded;
        if (Local == this) Local = null;
    }

    private void OnDisable()
    {
        transform.localPosition = basePosition;
        transform.localRotation = baseRotation;
    }

    // ── Static convenience API (no-op without a local player, e.g. dedicated server) ─────

    public static void Shake(float amount)
    {
        if (Local != null) Local.AddTrauma(amount);
    }

    /// <summary>Shake for a world event: weaker the further away the camera is.</summary>
    public static void ShakeAt(Vector3 worldPosition, float amount)
    {
        if (Local == null) return;

        float radius = Mathf.Max(0.01f, GameFeelSettings.Instance.worldShakeRadius);
        float distance = Vector3.Distance(Local.transform.position, worldPosition);
        float falloff = 1f - Mathf.Clamp01(distance / radius);
        if (falloff > 0f) Local.AddTrauma(amount * falloff);
    }

    public static void KickLocal(float pitchUpDegrees, float yawDegrees = 0f)
    {
        if (Local != null) Local.Kick(pitchUpDegrees, yawDegrees);
    }

    public static void KickFovLocal(float degrees)
    {
        if (Local != null) Local.KickFov(degrees);
    }

    /// <summary>
    /// Aim ray of the camera WITHOUT shake/bob/recoil. Use this for all gameplay raycasts.
    /// Also works without CameraEffects (then it's simply the camera itself).
    /// </summary>
    public static Ray GetAimRay(Camera camera)
    {
        Transform t = camera.transform;

        if (Local == null || Local.transform != t)
            return new Ray(t.position, t.forward);

        Transform parent = t.parent;
        Vector3 position = parent != null ? parent.TransformPoint(Local.basePosition) : Local.basePosition;
        Quaternion rotation = parent != null ? parent.rotation * Local.baseRotation : Local.baseRotation;
        return new Ray(position, rotation * Vector3.forward);
    }

    // ── Instance API ─────────────────────────────────────────────────────────

    public void AddTrauma(float amount)
    {
        if (!GameFeelSettings.Instance.cameraShake) return;
        trauma = Mathf.Clamp01(trauma + amount);
    }

    public void Kick(float pitchUpDegrees, float yawDegrees = 0f)
    {
        if (!GameFeelSettings.Instance.cameraKick) return;
        kickPitchVelocity += pitchUpDegrees * KickImpulseScale;
        kickYawVelocity += yawDegrees * KickImpulseScale;
    }

    public void KickFov(float degrees)
    {
        if (!GameFeelSettings.Instance.fovKicks) return;
        fovKickVelocity += degrees * FovImpulseScale;
    }

    private void OnLanded(float impactSpeed)
    {
        GameFeelSettings s = GameFeelSettings.Instance;
        if (!s.landingDip) return;

        dipVelocity -= Mathf.Min(impactSpeed * s.landingDipPerSpeed, s.maxLandingDipImpulse);

        if (impactSpeed >= s.hardLandingSpeed)
            AddTrauma(s.hardLandingTrauma);
    }

    // ── Update ───────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
        if (dt <= 0f) return; // paused (timeScale 0): hold the last pose

        GameFeelSettings s = GameFeelSettings.Instance;

        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;

        // Trauma shake: strength = trauma², so small values feel subtle and large values
        // feel strong. Perlin instead of Random → coherent shaking.
        trauma = Mathf.Max(0f, trauma - s.traumaDecay * dt);
        if (s.cameraShake && trauma > 0f)
        {
            float shake = trauma * trauma;
            float t = Time.time * s.shakeFrequency;
            rotationOffset += new Vector3(Noise(1, t), Noise(2, t), Noise(3, t)) * (s.maxShakeAngle * shake);
            positionOffset += new Vector3(Noise(4, t), Noise(5, t), 0f) * (s.maxShakeOffset * shake);
        }

        if (movement != null)
        {
            positionOffset += HeadBob(s, dt);
            UpdateStrafeTilt(s, dt);
        }

        Spring(ref dipOffset, ref dipVelocity, 140f, 16f, dt);
        Spring(ref kickPitch, ref kickPitchVelocity, 160f, 18f, dt);
        Spring(ref kickYaw, ref kickYawVelocity, 160f, 18f, dt);
        Spring(ref fovKick, ref fovKickVelocity, 90f, 14f, dt);

        positionOffset.y += dipOffset;
        rotationOffset += new Vector3(-kickPitch, kickYaw, roll);

        transform.localPosition = basePosition + positionOffset;
        transform.localRotation = baseRotation * Quaternion.Euler(rotationOffset);
    }

    private Vector3 HeadBob(GameFeelSettings s, float dt)
    {
        Vector3 velocity = movement.HorizontalVelocity;
        float speed = velocity.magnitude;
        bool active = s.headBob && movement.IsGrounded && speed > 0.5f;

        float targetWeight = active ? Mathf.Clamp01(speed / Mathf.Max(0.1f, movement.WalkSpeed)) : 0f;
        bobWeight = Mathf.MoveTowards(bobWeight, targetWeight, dt * 4f);
        if (bobWeight <= 0f) return Vector3.zero;

        float cyclesPerSecond = Mathf.Min(speed, s.bobMaxSpeed) / Mathf.Max(0.1f, s.bobStrideLength);
        bobPhase = Mathf.Repeat(bobPhase + cyclesPerSecond * dt, 1f);

        float angle = bobPhase * Mathf.PI * 2f;
        float crouchFactor = movement.IsCrouching ? 0.5f : 1f;

        // Two dips per stride (one per foot), one sideways sway per stride.
        return new Vector3(
            Mathf.Sin(angle) * s.bobAmplitudeX,
            (Mathf.Cos(angle * 2f) - 1f) * 0.5f * s.bobAmplitudeY,
            0f) * (bobWeight * crouchFactor);
    }

    private void UpdateStrafeTilt(GameFeelSettings s, float dt)
    {
        float targetRoll = 0f;
        if (s.strafeTilt)
        {
            float lateral = Vector3.Dot(movement.HorizontalVelocity, movement.transform.right)
                            / Mathf.Max(0.1f, movement.WalkSpeed);
            targetRoll = -Mathf.Clamp(lateral, -1.5f, 1.5f) * s.strafeTiltAngle;
        }

        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-s.strafeTiltSmoothing * dt));
    }

    private float Noise(int channel, float t) => Mathf.PerlinNoise(noiseSeed + channel * 13.7f, t) * 2f - 1f;

    /// <summary>Damped spring towards 0 (semi-implicit Euler, stable for dt ≤ MaxDeltaTime).</summary>
    private static void Spring(ref float value, ref float velocity, float stiffness, float damping, float dt)
    {
        float acceleration = -stiffness * value - damping * velocity;
        velocity += acceleration * dt;
        value += velocity * dt;
    }
}
