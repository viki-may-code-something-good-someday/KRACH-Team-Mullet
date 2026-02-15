using UnityEngine;

public class UIParallaxMovement : MonoBehaviour
{
    [SerializeField] private float intensity = 50f;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float maxOffset = 40f;

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Vector2 currentOffset;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * intensity;
        float mouseY = Input.GetAxis("Mouse Y") * intensity;

        currentOffset += new Vector2(mouseX, mouseY);
        currentOffset = Vector2.ClampMagnitude(currentOffset, maxOffset);

        Vector2 target = startPosition + currentOffset;

        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            target,
            smoothSpeed * Time.deltaTime
        );
    }
}
