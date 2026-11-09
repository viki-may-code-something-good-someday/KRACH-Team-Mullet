using DG.Tweening;
using UnityEngine;
using TMPro;

/// <summary>
/// Shows "destroyed / total" speakers. Only rebuilds the text when a value changes and pops
/// briefly when a speaker gets destroyed (can be turned off in GameFeelSettings → HUD).
/// </summary>
public class SoundBoxCounterUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI counterText;

    private int lastDestroyed = -1;
    private int lastTotal = -1;
    private Vector3 defaultScale = Vector3.one;

    private void Start()
    {
        if (counterText != null) defaultScale = counterText.rectTransform.localScale;
    }

    private void Update()
    {
        if (GameManager.Instance == null || counterText == null) return;

        int destroyed = GameManager.Instance.DestroyedSoundBoxCount;
        int total = GameManager.Instance.TotalSoundBoxCount;
        if (destroyed == lastDestroyed && total == lastTotal) return;

        bool speakerDestroyed = lastDestroyed >= 0 && destroyed > lastDestroyed;
        lastDestroyed = destroyed;
        lastTotal = total;

        counterText.text = $"{destroyed}/{total}";

        if (speakerDestroyed && GameFeelSettings.Instance.counterPop)
        {
            RectTransform rt = counterText.rectTransform;
            rt.DOKill(true);
            rt.localScale = defaultScale;
            rt.DOPunchScale(defaultScale * 0.4f, 0.4f, 6, 0.5f).SetLink(gameObject);
        }
    }
}
