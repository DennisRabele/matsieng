using UnityEngine;
using UnityEngine.UI;

public class TourMessageUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text text;
    [SerializeField] private Image background;

    [Header("Layout")]
    [SerializeField] private int fontSize = 22;
    [SerializeField] private Vector2 padding = new Vector2(20f, 14f);
    [SerializeField] private float maxWidth = 900f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color textColor = Color.white;

    private float untilTime;
    private int activePriority = int.MinValue;
    private uint activeSequence;
    private uint nextSequence = 1;

    public static TourMessageUI GetOrCreate()
    {
        TourMessageUI existing = FindFirstObjectByType<TourMessageUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("TourMessageUI");
        DontDestroyOnLoad(go);
        TourMessageUI ui = go.AddComponent<TourMessageUI>();
        ui.BuildIfNeeded();
        return ui;
    }

    private void Awake()
    {
        BuildIfNeeded();
    }

    public void Show(string message, float seconds)
    {
        Show(message, seconds, 0);
    }

    public uint Show(string message, float seconds, int priority)
    {
        BuildIfNeeded();

        if (text == null)
            return 0;

        float requestUntilTime = Time.time + Mathf.Max(0f, seconds);
        bool isActive = untilTime > 0f && Time.time < untilTime;

        if (isActive && priority < activePriority)
            return 0;

        text.text = message ?? string.Empty;

        if (!isActive || priority > activePriority)
            untilTime = requestUntilTime;
        else
            untilTime = Mathf.Max(untilTime, requestUntilTime);

        activePriority = priority;
        activeSequence = nextSequence++;
        SetVisible(true);
        return activeSequence;
    }

    public void Clear()
    {
        Clear(0);
    }

    public void Clear(uint sequence)
    {
        if (sequence != 0 && sequence != activeSequence)
            return;

        if (text != null)
            text.text = string.Empty;

        untilTime = 0f;
        activePriority = int.MinValue;
        activeSequence = 0;
        SetVisible(false);
    }

    private void Update()
    {
        if (untilTime <= 0f)
            return;

        if (Time.time >= untilTime)
            Clear(0);
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null)
            canvas.enabled = visible;
    }

    private void BuildIfNeeded()
    {
        if (canvas != null && text != null && background != null)
            return;

        // Canvas
        GameObject canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel background
        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        background = panelGo.AddComponent<Image>();
        background.color = backgroundColor;

        RectTransform panelRect = background.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(24f, 24f);
        panelRect.sizeDelta = new Vector2(Mathf.Max(300f, maxWidth), 140f);

        // Text
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(panelGo.transform, false);
        text = textGo.AddComponent<Text>();
        // Unity no longer exposes Arial.ttf as a built-in font.
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(padding.x, padding.y);
        textRect.offsetMax = new Vector2(-padding.x, -padding.y);

        SetVisible(false);
    }
}
