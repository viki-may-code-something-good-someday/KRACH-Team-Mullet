using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the remaining round time. Reads GameManager.TimeRemaining, which is in sync on all
/// clients. In the last seconds the timer turns yellow and then red and pulses
/// (can be turned off in GameFeelSettings → Timer).
/// </summary>
public class TimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;

    private Color defaultColor;
    private Vector3 defaultScale;
    private int lastShownSeconds = -1;

    private void Start()
    {
        if (timerText == null)
        {
            Debug.LogWarning("[TimerUI] timerText is not assigned – the timer won't be displayed.");
            enabled = false;
            return;
        }

        defaultColor = timerText.color;
        defaultScale = timerText.rectTransform.localScale;
    }

    private void Update()
    {
        if (GameManager.Instance != null)
        {
            UpdateTimer(GameManager.Instance.TimeRemaining);
        }
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timeRemaining < 0f) timeRemaining = 0f;

        // Round up: "00:01" stays until the last second is really over.
        int totalSeconds = Mathf.CeilToInt(timeRemaining);
        if (totalSeconds == lastShownSeconds) return; // only rebuild the string when it changes
        lastShownSeconds = totalSeconds;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";

        ApplyWarningStyle(totalSeconds);
    }

    private void ApplyWarningStyle(int totalSeconds)
    {
        GameFeelSettings settings = GameFeelSettings.Instance;

        if (!settings.timerWarning || totalSeconds <= 0)
        {
            timerText.color = defaultColor;
            return;
        }

        if (totalSeconds <= settings.timerCriticalSeconds)
        {
            timerText.color = settings.timerCriticalColor;

            RectTransform rt = timerText.rectTransform;
            rt.DOKill(true);
            rt.localScale = defaultScale;
            rt.DOPunchScale(defaultScale * 0.3f, 0.35f, 6, 0.6f).SetLink(gameObject);
        }
        else if (totalSeconds <= settings.timerWarningSeconds)
        {
            timerText.color = settings.timerWarningColor;
        }
        else
        {
            timerText.color = defaultColor;
        }
    }
}
