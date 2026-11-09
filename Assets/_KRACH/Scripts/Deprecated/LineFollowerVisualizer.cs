using DG.Tweening;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways]
public class LineFollowerVisualizer : MonoBehaviour
{
    [Header("Line Settings")]
    [SerializeField] private int pointCount = 100;
    [SerializeField] private float lengthX = 0.5f;
    [SerializeField] private float heightMultiplier = 0.8f;

    public RMF_State rmfState;
    private float rmfHighValue;

    public GameObject rmfIndicator;

    [Header("Animation")]
    [SerializeField] private float followSpeed = 100f; // how fast the points follow
    [SerializeField] private bool useUnscaledTime = false;

    private LineRenderer line;
    private float[] yValues;
    private float xSpacing;

    public float targetValue01; // value set from outside (0–1)

    // internal sine variables
    private float sinePhase;
    private float sineCenter;
    private float sineAmplitude;
    private Tween phaseTween;
    private Coroutine sineCoroutine;


    // Sine / DOTween control
    [Header("Sine DOTween Settings")]
    [SerializeField] private float lowMin = 0f;
    [SerializeField] private float lowMax = 0.4f;
    [SerializeField] private float highMin = 0.6f;
    [SerializeField] private float highMax = 1f;
    [SerializeField] private float minAmplitude = 0.02f;
    [SerializeField] private float maxAmplitude = 0.1f;   // maximum sine amplitude
    [SerializeField] private float minPeriodLow = 0.5f;   // minimum period (s)
    [SerializeField] private float maxPeriodLow = 1f;     // maximum period (s)
    [SerializeField] private float minPeriodHigh = 0.05f;
    [SerializeField] private float maxPeriodHigh = 0.5f;
    [SerializeField] private float changeIntervalMin = 2f; // how often new random values are picked
    [SerializeField] private float changeIntervalMax = 5f;

    public SpriteRenderer upperHalf;
    public float upperHalfInitialAlpha;
    public SpriteRenderer lowerHalf;
    public float lowerHalfInitialAlpha;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        Initialize();
    }

    void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (phaseTween != null && phaseTween.IsActive())
        {
            float sineValue = Mathf.Clamp01(sineCenter + sineAmplitude * Mathf.Sin(sinePhase));
            SetValue(sineValue); // <-- the result is passed into the existing method here
            SetTransparencyOfHalf(rmfState, sineValue);
        }

        float targetHeight = targetValue01 * heightMultiplier;

        // Point 0 moves towards the target value
        yValues[0] = Mathf.Lerp(
            yValues[0],
            targetHeight,
            delta * followSpeed
        );

        // Every other point follows its predecessor
        for (int i = 1; i < pointCount; i++)
        {
            float lerpFactor = 1f - Mathf.Exp(-followSpeed * delta);
            yValues[i] = Mathf.Lerp(yValues[i], yValues[i - 1], lerpFactor);
        }

        UpdateLine();
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

        SetValue(0f);
        StartSine(RMF_State.Low);
    }

    public void SetRmfHighValue(float value)
    {
        Debug.Log("Setting RMF High Value to: " + value);
        rmfHighValue = value;

        // previously: 1 * value * heightMultiplier
        Vector3 indicatorPos = new Vector3(rmfIndicator.transform.localPosition.x, 1, rmfIndicator.transform.localPosition.z);
        rmfIndicator.gameObject.transform.SetLocalPositionAndRotation(indicatorPos, Quaternion.identity);
    }

    public void SetValue(float value01)
    {
        targetValue01 = Mathf.Clamp01(value01);

        if(targetValue01 >= rmfHighValue)
        {
            rmfState = RMF_State.High;
        }
        else
        {
            rmfState = RMF_State.Low;
        }
    }

    public void StartSine(RMF_State forState)
    {
        // stop an existing sine animation if there is one
        if (forState != rmfState)
        {
            Debug.Log("Starting sine for state: " + forState);
            StopSine();
            rmfState = forState;
            // start a new coroutine that picks new random values in intervals
            sineCoroutine = StartCoroutine(SineControllerCoroutine(forState));
        }
    }

    public void StopSine()
    {
        if (sineCoroutine != null)
        {
            StopCoroutine(sineCoroutine);
            sineCoroutine = null;
        }

        if (phaseTween != null && phaseTween.IsActive())
        {
            phaseTween.Kill();
            phaseTween = null;
        }
    }

    public void SetTransparencyOfHalf(RMF_State state, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (state == RMF_State.Low)
        {
            alpha *= lowerHalfInitialAlpha;
            Color c = lowerHalf.color;
            c.a = alpha;
            lowerHalf.color = c;

            alpha *= upperHalfInitialAlpha;
            Color d = upperHalf.color;
            d.a = upperHalfInitialAlpha;
            upperHalf.color = d;
        }
        else
        {
            alpha *= upperHalfInitialAlpha;
            Color c = upperHalf.color;
            c.a = alpha;
            upperHalf.color = c;

            alpha *= lowerHalfInitialAlpha;
            Color d = lowerHalf.color;
            d.a = lowerHalfInitialAlpha;
            lowerHalf.color = d;
        }
    }

    private IEnumerator SineControllerCoroutine(RMF_State forState)
    {
        Debug.Log("Sine Coroutine started for state: " + forState);
        ChooseNewSineParameters(forState);

        while (true)
        {
            // Create a periodic tween that continuously increases sinePhase
            if (phaseTween != null && phaseTween.IsActive())
            {
                phaseTween.Kill();
                phaseTween = null;
            }


            // periodic rotation: increase phase by 2PI over 'period' seconds, loops forever incrementally
            float period = 0f;
            if(forState == RMF_State.High)
            {
                period = Random.Range(minPeriodHigh, maxPeriodHigh);
            }
            else
            {
                period = Random.Range(minPeriodLow, maxPeriodLow);
            }

            phaseTween = DOTween.To(() => sinePhase, x => sinePhase = x, sinePhase + Mathf.PI * 2f, period)
                                .SetEase(Ease.Linear)
                                .SetLoops(-1, LoopType.Incremental);

            // Wait a random interval before new parameters are picked (stylistic variation)
            float wait = Random.Range(changeIntervalMin, changeIntervalMax);
            yield return new WaitForSeconds(wait);

            // Pick new parameters (center/amplitude/maybe faster/slower)
            ChooseNewSineParameters(forState);
            // The phase tween is recreated in the next loop with the new period value
        }
    }

    private void ChooseNewSineParameters(RMF_State state)
    {
        float min, max;
        if (state == RMF_State.Low)
        {
            min = lowMin;
            max = lowMax;
        }
        else
        {
            min = highMin;
            max = highMax;
        }

        // maximum possible half range
        float maxHalfRange = (max - min) * 0.5f;
        float ampMax = Mathf.Max(minAmplitude, maxHalfRange);

        // random amplitude, but not larger than half the range
        float amp = Random.Range(minAmplitude, maxAmplitude);
        // pick the center so that center +/- amp stays within [min,max]
        float centerMin = min + amp;
        float centerMax = max - amp;
        if (centerMax < centerMin) centerMax = centerMin; // fallback
        float center = Random.Range(centerMin, centerMax);

        sineAmplitude = amp;
        sineCenter = center;

        // Debug.Log($"Sine new params: center={sineCenter:F2}, amp={sineAmplitude:F2}");
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
