using DG.Tweening;
using UnityEngine;

/// <summary>
/// A single debris piece of a destroyed wall. Becomes "live" when wallBroken is activated
/// and cleans itself up again after <see cref="lifetime"/> seconds.
///
/// Players pushing the debris around happens in PlayerMovement.OnControllerColliderHit –
/// CharacterControllers don't trigger OnCollisionEnter on rigidbodies, which is why the
/// previous approach here never fired.
/// </summary>
public class WallFracture : MonoBehaviour
{
    [Header("Lifetime")]
    [Tooltip("Seconds until the piece disappears.")]
    [SerializeField] private float lifetime = 5f;
    [Tooltip("How long the piece shrinks at the end (only if debrisShrinkOut is enabled in the GameFeelSettings).")]
    [SerializeField] private float shrinkDuration = 0.6f;

    private void OnEnable()
    {
        // TODO: pooling, in case many walls break at the same time.
        if (GameFeelSettings.Instance.debrisShrinkOut && shrinkDuration > 0f)
        {
            transform.DOScale(Vector3.zero, shrinkDuration)
                .SetDelay(Mathf.Max(0f, lifetime - shrinkDuration))
                .SetEase(Ease.InBack)
                .SetLink(gameObject);
        }

        Destroy(gameObject, lifetime);
    }
}
