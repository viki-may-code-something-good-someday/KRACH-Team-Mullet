using UnityEngine;

/// <summary>
/// Subtle parallax effect for menu backgrounds: the element shifts depending on the mouse
/// position relative to the screen center. (Previously mouse deltas were accumulated – the
/// element therefore got stuck at the maximum offset.)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIParallaxMovement : MonoBehaviour
{
    [Tooltip("Offset in UI units when the mouse is at the screen edge.")]
    [SerializeField] private float intensity = 50f;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float maxOffset = 40f;

    private RectTransform rectTransform;
    private Vector2 startPosition;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 mouse = (Vector2)Input.mousePosition;
        Vector2 normalized = new Vector2(
            (mouse.x - screenCenter.x) / Mathf.Max(1f, screenCenter.x),
            (mouse.y - screenCenter.y) / Mathf.Max(1f, screenCenter.y));

        Vector2 offset = Vector2.ClampMagnitude(normalized * intensity, maxOffset);
        Vector2 target = startPosition + offset;

        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            target,
            1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime));
    }
}
