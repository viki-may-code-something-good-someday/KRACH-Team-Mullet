using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight overlay for game feedback that builds itself at runtime – no scene or prefab
/// needs to be touched. Lives in the currently active scene and is recreated automatically on
/// the next call after a scene change.
///
///   GameFeelHUD.Banner("HUNTERS WIN!", color);   // big announcement
///   GameFeelHUD.Flash(color);                    // full-screen flash
///   GameFeelHUD.CenterPop("CAUGHT!", color);     // short text above the crosshair
///   GameFeelHUD.Countdown("Respawn in", 5f);     // countdown, runs out by itself
///
/// All calls are no-ops if the respective feature is disabled in GameFeelSettings or there is
/// no graphics output (dedicated server / batch mode).
/// </summary>
public class GameFeelHUD : MonoBehaviour
{
    private static GameFeelHUD instance;

    private TextMeshProUGUI bannerText;
    private CanvasGroup bannerGroup;
    private Sequence bannerSequence;

    private TextMeshProUGUI centerText;
    private CanvasGroup centerGroup;
    private Sequence centerSequence;

    private TextMeshProUGUI countdownText;
    private CanvasGroup countdownGroup;
    private string countdownPrefix;
    private float countdownEndTime;
    private int lastCountdownValue = -1;
    private bool countdownActive;

    private Image flashImage;

    // ── Public API ───────────────────────────────────────────────────────────

    public static void Banner(string text, Color color, float holdDuration = 2.2f)
    {
        if (!GameFeelSettings.Instance.hudBanners) return;
        GameFeelHUD hud = Get();
        if (hud != null) hud.ShowBanner(text, color, holdDuration);
    }

    public static void Flash(Color color, float duration = 0.45f)
    {
        if (!GameFeelSettings.Instance.screenFlash) return;
        GameFeelHUD hud = Get();
        if (hud != null) hud.ShowFlash(color, duration);
    }

    public static void CenterPop(string text, Color color, float holdDuration = 0.5f)
    {
        if (!GameFeelSettings.Instance.hitConfirmText) return;
        GameFeelHUD hud = Get();
        if (hud != null) hud.ShowCenterPop(text, color, holdDuration);
    }

    /// <summary>Shows "prefix N" and counts down in game time. Disappears by itself at 0.</summary>
    public static void Countdown(string prefix, float seconds)
    {
        if (!GameFeelSettings.Instance.respawnCountdown) return;
        GameFeelHUD hud = Get();
        if (hud != null) hud.StartCountdown(prefix, seconds);
    }

    public static void HideCountdown()
    {
        if (instance != null) instance.StopCountdown();
    }

    // ── Construction ─────────────────────────────────────────────────────────

    private static GameFeelHUD Get()
    {
        if (instance != null) return instance;
        if (Application.isBatchMode) return null;

        GameObject go = new GameObject("GameFeelHUD");
        instance = go.AddComponent<GameFeelHUD>();
        instance.Build();
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // Deliberately no GraphicRaycaster: the overlay must never swallow clicks.

        flashImage = CreateFlash();
        bannerText = CreateText("Banner", new Vector2(0.5f, 0.72f), 84f, out bannerGroup);
        centerText = CreateText("CenterPop", new Vector2(0.5f, 0.58f), 46f, out centerGroup);
        countdownText = CreateText("Countdown", new Vector2(0.5f, 0.36f), 40f, out countdownGroup);
    }

    private Image CreateFlash()
    {
        GameObject go = new GameObject("Flash", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string objectName, Vector2 anchor, float fontSize, out CanvasGroup group)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1600f, 220f);
        rt.anchoredPosition = Vector2.zero;

        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        if (text.font != null)
        {
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 220);
        }

        return text;
    }

    // ── Effects ──────────────────────────────────────────────────────────────

    private void ShowBanner(string text, Color color, float holdDuration)
    {
        bannerSequence?.Kill();

        bannerText.text = text;
        bannerText.color = color;
        bannerGroup.alpha = 0f;
        bannerText.rectTransform.localScale = Vector3.one * 1.6f;

        // Unscaled, so banners also play at normal speed during the round-end slow motion.
        bannerSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(bannerGroup.DOFade(1f, 0.12f))
            .Join(bannerText.rectTransform.DOScale(1f, 0.4f).SetEase(Ease.OutBack))
            .AppendInterval(holdDuration)
            .Append(bannerGroup.DOFade(0f, 0.45f));
    }

    private void ShowCenterPop(string text, Color color, float holdDuration)
    {
        centerSequence?.Kill();

        centerText.text = text;
        centerText.color = color;
        centerGroup.alpha = 1f;
        centerText.rectTransform.localScale = Vector3.one * 0.6f;

        centerSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(centerText.rectTransform.DOScale(1.15f, 0.12f).SetEase(Ease.OutQuad))
            .Append(centerText.rectTransform.DOScale(1f, 0.1f))
            .AppendInterval(holdDuration)
            .Append(centerGroup.DOFade(0f, 0.25f));
    }

    private void ShowFlash(Color color, float duration)
    {
        flashImage.DOKill();
        flashImage.color = color;
        flashImage.DOFade(0f, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void StartCountdown(string prefix, float seconds)
    {
        countdownPrefix = prefix;
        countdownEndTime = Time.time + seconds;
        lastCountdownValue = -1;
        countdownActive = true;
        countdownGroup.DOKill();
        countdownGroup.alpha = 1f;
    }

    private void StopCountdown()
    {
        if (!countdownActive) return;
        countdownActive = false;
        countdownGroup.DOKill();
        countdownGroup.DOFade(0f, 0.25f).SetUpdate(true).SetLink(gameObject);
    }

    private void Update()
    {
        if (!countdownActive) return;

        int remaining = Mathf.CeilToInt(countdownEndTime - Time.time);
        if (remaining <= 0)
        {
            StopCountdown();
            return;
        }

        if (remaining == lastCountdownValue) return;
        lastCountdownValue = remaining;

        countdownText.text = $"{countdownPrefix} {remaining}";
        countdownText.rectTransform.DOKill(true);
        countdownText.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f).SetLink(gameObject);
    }
}
