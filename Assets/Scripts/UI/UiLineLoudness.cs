using UnityEngine;

public class UiLineLoudness : MonoBehaviour
{
    [SerializeField] private Transform upPoint;
    [SerializeField] private Transform downPoint;

    [SerializeField] private Transform line;
    
    [SerializeField] private float currentValue = 0f;
    private float currentY;
    private float targetY;
    
    // Lerp Parameter
    [SerializeField] private float slowLerpSpeed = 0.3f;  // Geschwindigkeit für Value 0 (langsam)
    [SerializeField] private float fastLerpSpeed = 1f;  // Geschwindigkeit für Value 1 (schneller)
    
    // Sinus Schwankung Parameter
    [SerializeField] private float swingAmplitude = 0.2f;  // Amplitude der Schwankung
    [SerializeField] private float swingFrequency = 2f;    // Aktuelle Frequenz
    private float swingTimer = 0f;
    private float frequencyChangeTimer = 0f;
    [SerializeField] private float frequencyChangeInterval = 3f;  // Frequenz alle 3 Sekunden ändern

    void Start()
    {
        if (line != null)
        {
            currentY = line.localPosition.y;
            targetY = currentY;
        }
        RandomizeSwingFrequency();
    }

    void Update()
    {
        if (line == null)
            return;

        currentValue = SoundManager.Instance.currentLoudness; // Normalisieren auf 0-1 (angenommen maxLoudness ist 100)

        // Zielposition basierend auf currentValue setzen
        targetY = Mathf.Lerp(
            downPoint.localPosition.y,
            upPoint.localPosition.y,
            currentValue
        );


        // Lerpen basierend auf Wert
        float lerpSpeed = Mathf.Lerp(slowLerpSpeed, fastLerpSpeed, currentValue);
        currentY = Mathf.Lerp(currentY, targetY, lerpSpeed * Time.deltaTime);

        // Sinus-Schwankung hinzufügen
        float swing = Mathf.Sin(swingTimer * swingFrequency) * swingAmplitude;
        float finalY = currentY + swing;

        // Position aktualisieren (nur Y-Achse)
        Vector3 newPos = line.localPosition;
        newPos.y = finalY;
        line.localPosition = newPos;

        // Timer für Sinus-Schwankung erhöhen
        swingTimer += Time.deltaTime;

        // Frequenz zufällig ändern
        frequencyChangeTimer += Time.deltaTime;
        if (frequencyChangeTimer >= frequencyChangeInterval)
        {
            RandomizeSwingFrequency();
            frequencyChangeTimer = 0f;
        }
    }

    /// <summary>
    /// Setzt das Value auf 0 - Linie lerpt langsam zum downPoint
    /// </summary>
    public void SetValueToZero()
    {
        currentValue = 0f;
    }

    /// <summary>
    /// Setzt das Value auf 1 - Linie lerpt schneller zum upPoint
    /// </summary>
    public void SetValueToOne()
    {
        currentValue = 1f;
    }

    /// <summary>
    /// Zufällige Frequenz für die Sinus-Schwankung setzen
    /// </summary>
    private void RandomizeSwingFrequency()
    {
        swingFrequency = Random.Range(1f, 4f);  // Frequenz zwischen 1 und 4 Hz
    }
}
