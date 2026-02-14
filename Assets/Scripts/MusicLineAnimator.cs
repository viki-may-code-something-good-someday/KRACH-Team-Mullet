using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways]
public class LineFollowerVisualizer : MonoBehaviour
{
    [Header("Line Settings")]
    [SerializeField] private int pointCount = 100;
    [SerializeField] private float lengthX = 10f;
    [SerializeField] private float heightMultiplier = 5f;

    [Header("Animation")]
    [SerializeField] private float followSpeed = 10f; // Wie schnell die Punkte folgen
    [SerializeField] private bool useUnscaledTime = false;

    private LineRenderer line;
    private float[] yValues;
    private float xSpacing;

    public float targetValue01; // Extern gesetzter Wert (0–1)

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        Initialize();
    }

    void Initialize()
    {
        line.positionCount = pointCount;

        xSpacing = lengthX / (pointCount - 1);
        yValues = new float[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float x = i * xSpacing;
            line.SetPosition(i, new Vector3(x, 0, 0));
        }
    }

    public void SetValue(float value01)
    {
        targetValue01 = Mathf.Clamp01(value01);
    }

    void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        float targetHeight = targetValue01 * heightMultiplier;

        // Punkt 0 bewegt sich zum Zielwert
        yValues[0] = Mathf.Lerp(
            yValues[0],
            targetHeight,
            delta * followSpeed
        );

        // Rest folgt jeweils dem Vorgänger
        for (int i = 1; i < pointCount; i++)
        {
            float lerpFactor = 1f - Mathf.Exp(-followSpeed * delta);
            yValues[i] = Mathf.Lerp(yValues[i], yValues[i - 1], lerpFactor);
        }

        UpdateLine();
    }

    void UpdateLine()
    {
        for (int i = 0; i < pointCount; i++)
        {
            float x = i * xSpacing;
            line.SetPosition(i, new Vector3(x, yValues[i], 0));
        }
    }
}