using UnityEngine;
using UnityEngine.UI;

public class UiLineLoudness : MonoBehaviour
{
    [SerializeField] private Transform upPoint;
    [SerializeField] private Transform downPoint;

    [SerializeField] private Transform line;

    [SerializeField] private float currentValue = 0f;
    private float currentY;
    private float targetY;

    // Lerp parameters
    [SerializeField] private float slowLerpSpeed = 0.3f;  // speed for value 0 (slow)
    [SerializeField] private float fastLerpSpeed = 1f;  // speed for value 1 (faster)

    // Sine sway parameters
    [SerializeField] private float swingAmplitude = 0.2f;  // amplitude of the sway
    [SerializeField] private float swingFrequency = 2f;    // current frequency
    private float swingTimer = 0f;
    private float frequencyChangeTimer = 0f;
    [SerializeField] private float frequencyChangeInterval = 3f;  // change the frequency every 3 seconds

    // Color Lerp
    [SerializeField] private Color startColor = new Color(0.8431373f, 0.38039216f, 1f); // #D761FF
    [SerializeField] private Color endColor = new Color(0.5137255f, 1f, 0.3843137f);   // #83FF62
    [SerializeField] private float colorLerpSpeed = 5f;
    private Color currentColor;
    private SpriteRenderer spriteRenderer;
    private Image uiImage;
    private Renderer meshRenderer;
    private LineRenderer lineRenderer;

    // Rotation wiggle (left-right)
    [SerializeField] private float rotationAmplitude = 5f; // degrees
    [SerializeField] private float rotationFrequency = 1.5f; // Hz
    [SerializeField] private float rotationLerpSpeed = 8f;
    private float rotationTimer = 0f;
    private float currentRotationAngle = 0f;

    void Start()
    {
        if (line != null)
        {
            currentY = line.localPosition.y;
            targetY = currentY;
        }
        RandomizeSwingFrequency();

        // Cache renderer components for color updates
        if (line != null)
        {
            spriteRenderer = line.GetComponent<SpriteRenderer>();
            uiImage = line.GetComponent<Image>();
            meshRenderer = line.GetComponent<Renderer>();
            lineRenderer = line.GetComponent<LineRenderer>();
        }

        currentColor = startColor;
        ApplyColor(currentColor);
    }

    void Update()
    {
        if (line == null)
            return;

        // Set the target position based on currentValue
        targetY = Mathf.Lerp(
            downPoint.localPosition.y,
            upPoint.localPosition.y,
            currentValue
        );


        // Lerp based on the value
        float lerpSpeed = Mathf.Lerp(slowLerpSpeed, fastLerpSpeed, currentValue);
        currentY = Mathf.Lerp(currentY, targetY, lerpSpeed * Time.deltaTime);

        // Add the sine sway
        float swing = Mathf.Sin(swingTimer * swingFrequency) * swingAmplitude;
        float finalY = currentY + swing;

        // Update the position (Y axis only)
        Vector3 newPos = line.localPosition;
        newPos.y = finalY;
        line.localPosition = newPos;

        // Color lerp: target based on currentValue
        Color targetColor = Color.Lerp(startColor, endColor, currentValue);
        currentColor = Color.Lerp(currentColor, targetColor, colorLerpSpeed * Time.deltaTime);
        ApplyColor(currentColor);

        // Advance the timer for the sine sway
        swingTimer += Time.deltaTime;

        // Rotation wiggle
        rotationTimer += Time.deltaTime;
        float targetAngle = Mathf.Sin(rotationTimer * rotationFrequency * Mathf.PI * 2f) * rotationAmplitude;
        currentRotationAngle = Mathf.LerpAngle(currentRotationAngle, targetAngle, rotationLerpSpeed * Time.deltaTime);
        line.localRotation = Quaternion.Euler(0f, 0f, currentRotationAngle);

        // Randomly change the frequency
        frequencyChangeTimer += Time.deltaTime;
        if (frequencyChangeTimer >= frequencyChangeInterval)
        {
            RandomizeSwingFrequency();
            frequencyChangeTimer = 0f;
        }
    }

    /// <summary>
    /// Sets the value to 0 – the line lerps slowly to the downPoint
    /// </summary>
    public void SetValueToZero()
    {
        currentValue = 0f;
    }

    /// <summary>
    /// Sets the value to 1 – the line lerps faster to the upPoint
    /// </summary>
    public void SetValueToOne()
    {
        currentValue = 1f;
    }

    /// <summary>
    /// Sets a random frequency for the sine sway
    /// </summary>
    private void RandomizeSwingFrequency()
    {
        swingFrequency = Random.Range(1f, 4f);  // frequency between 1 and 4 Hz
    }

    private void ApplyColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }

        if (uiImage != null)
        {
            uiImage.color = color;
        }

        if (meshRenderer != null && spriteRenderer == null && uiImage == null)
        {
            meshRenderer.material.color = color;
        }

        if (lineRenderer != null)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }
}
