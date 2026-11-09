using UnityEngine;

/// <summary>
/// Scales a <see cref="LuaSoundEmitter"/> collision one-shot by impact speed using
/// FMOD's built-in "Speed (Absolute)" parameter.
///
/// Put this on a prop that has a Collider + Rigidbody + LuaSoundEmitter. On impact it
/// passes the collision velocity into the one-shot's 3D attributes; FMOD's built-in
/// Speed (Absolute) then equals the impact speed in m/s, and the sound designer maps
/// that to Volume (and optionally Pitch) in FMOD Studio — so the whole perceptual curve
/// lives in Studio, not in code.
///
/// IMPORTANT: set the emitter's OneShotTrigger to None — this component drives the
/// one-shot so it can pass the velocity along. Otherwise the emitter's own
/// CollisionEnter trigger would fire a second, velocity-less one-shot on every hit.
/// </summary>
[RequireComponent(typeof(LuaSoundEmitter))]
[AddComponentMenu("Lua/PropImpactSound")]
public class PropImpactSound : MonoBehaviour
{
    [Header("Impact")]
    [Tooltip("Impact speed (m/s) from which a sound plays at all (below = silent). " +
             "The volume curve over the speed lives in FMOD (built-in 'Speed (Absolute)').")]
    [SerializeField] private float minImpactSpeed = 1.5f;

    [Header("Speed Source")]
    [Tooltip("On: uses the prop's own speed in the frame BEFORE the impact (needs a Rigidbody). " +
             "Off: uses collision.relativeVelocity — the relative speed of both bodies at impact.")]
    [SerializeField] private bool usePreCollisionSpeed = false;

    private LuaSoundEmitter emitter;
    private Rigidbody body;
    private Vector3 lastVelocity; // prop velocity cached in the FixedUpdate before the impact

    private void Awake()
    {
        emitter = GetComponent<LuaSoundEmitter>();
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // PhysX overwrites the rigidbody velocity during the collision response, so we
        // remember the pre-impact velocity each physics step for the usePreCollisionSpeed path.
        if (body != null) lastVelocity = body.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 impactVelocity = usePreCollisionSpeed ? lastVelocity : collision.relativeVelocity;
        if (impactVelocity.magnitude < minImpactSpeed) return; // too soft → stay silent

        // Pass the raw velocity; FMOD's built-in Speed (Absolute) = its magnitude in m/s.
        emitter.PlayOneShot(impactVelocity);
    }
}
