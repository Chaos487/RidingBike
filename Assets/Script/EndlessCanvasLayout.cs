using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Layout for the authored EndlessRunCanvas controls only. Keep the original object names
/// and HUD paths so gameplay bindings still work. Text stays at 40; boxes grow and wrap.
/// Also runs in Prefab Mode, so the saved UI can be inspected without starting a run.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class EndlessCanvasLayout : MonoBehaviour
{
    const float Margin = 24f;
    const float Gap = 16f;
    RectTransform root;
    [SerializeField, HideInInspector] Text[] authoredTexts;
    TMP_Text hpValue;
    bool dirty = true;
    bool arranging;
    Vector2 lastSize;
    Vector2 lastBoostPosition;

    void OnEnable()
    {
        root = (RectTransform)transform;
        // Serialized references keep runtime-created Goals/Stats/summary rows out of scope,
        // including after disabling/re-enabling the Canvas or an Editor domain reload.
        if (authoredTexts == null) return;
        foreach (Text text in authoredTexts)
        {
            if (text == null) continue;
            text.fontSize = 40;
            text.resizeTextForBestFit = false;
            text.font = LocalizationManager.GetFont();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.RegisterDirtyLayoutCallback(RequestLayout);
        }
        hpValue = Rect("HpBarRoot/HpValueText")?.GetComponent<TMP_Text>();
        if (hpValue != null) hpValue.RegisterDirtyLayoutCallback(RequestLayout);
        dirty = true;
    }

    void OnDisable()
    {
        if (hpValue != null) hpValue.UnregisterDirtyLayoutCallback(RequestLayout);
        if (authoredTexts == null) return;
        foreach (Text text in authoredTexts)
            if (text != null) text.UnregisterDirtyLayoutCallback(RequestLayout);
    }

    void OnRectTransformDimensionsChange() => RequestLayout();
    void OnTransformChildrenChanged() => RequestLayout();
    void RequestLayout() { if (!arranging) dirty = true; }

    void LateUpdate()
    {
        RectTransform boost = Rect("BoostButton");
        Vector2 boostPosition = boost != null ? boost.anchoredPosition : Vector2.zero;
        if (root != null && (dirty || root.rect.size != lastSize || boostPosition != lastBoostPosition))
        {
            Rebuild();
            lastBoostPosition = boostPosition;
        }
    }

    public void Rebuild()
    {
        if (root == null) root = (RectTransform)transform;
        float width = root.rect.width;
        float height = root.rect.height;
        if (width <= 0f || height <= 0f) return;
        arranging = true;
        LayoutHud(width, height);
        LayoutMenu(Rect("MenuPanel"));
        LayoutPause(Rect("PausePanel"));
        LayoutChoices(Rect("NodePanel"));
        lastSize = root.rect.size;
        dirty = false;
        arranging = false;
    }

    RectTransform Rect(string path) => transform.Find(path) as RectTransform;
    static Text Label(Transform parent) => parent != null ? parent.GetComponentInChildren<Text>(true) : null;
    static RectTransform Child(Transform parent, string path) => parent != null ? parent.Find(path) as RectTransform : null;

    // Preferred height is measured at the allocated width, including wrapped CJK and Latin text.
    static float TextHeight(Text text, float width)
    {
        if (text == null || string.IsNullOrEmpty(text.text)) return 0f;
        var settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
        return Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit) + 8f;
    }

    static void Box(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static float FitText(RectTransform rect, float x, float y, float maxWidth, bool right = false)
    {
        Text text = rect != null ? rect.GetComponent<Text>() : null;
        if (text == null) return 0f;
        float width = Mathf.Min(maxWidth, Mathf.Max(1f, text.preferredWidth + 8f));
        float height = TextHeight(text, width);
        Box(rect, right ? x - width : x, y, width, height);
        return height;
    }

    static float ButtonHeight(RectTransform button, float width)
        => Mathf.Max(64f, TextHeight(Label(button), width - 32f) + 20f);

    static void ButtonBox(RectTransform button, float x, float y, float width, float height)
    {
        Box(button, x, y, width, height);
        Text label = Label(button);
        if (label != null && label.transform != button)
            Stretch(label.rectTransform, 16f, 10f, 16f, 10f);
    }

    void LayoutHud(float width, float height)
    {
        float leftWidth = Mathf.Min(600f, width * 0.32f);
        float rightWidth = Mathf.Min(540f, width * 0.28f);
        float y = Margin;
        foreach (string path in LeftHud)
            y += FitText(Rect(path), Margin, y, leftWidth) + Gap;

        float right = width - Margin;
        float rightY = Margin + FitText(Rect("HpLabelText"), right, Margin, rightWidth, true) + 8f;
        RectTransform hp = Rect("HpBarRoot");
        TMP_Text hpText = hp != null ? hp.GetComponentInChildren<TMP_Text>(true) : null;
        float hpHeight = 64f;
        if (hpText != null)
        {
            hpText.fontSize = 40f;
            hpText.enableAutoSizing = false;
            hpHeight = Mathf.Max(hpHeight, hpText.GetPreferredValues(hpText.text, rightWidth - 16f, 0f).y + 12f);
        }
        Box(hp, right - rightWidth, rightY, rightWidth, hpHeight);
        rightY += hpHeight + Gap;
        foreach (string path in RightHud)
            rightY += FitText(Rect(path), right, rightY, rightWidth, true) + Gap;

        RectTransform feats = Rect("FeatListRoot");
        if (feats != null)
        {
            // Position the existing list below the resized HUD, preserving its own content fitter.
            feats.anchorMin = feats.anchorMax = Vector2.one;
            feats.pivot = Vector2.one;
            feats.anchoredPosition = new Vector2(-Margin, -rightY);
        }

        RectTransform toast = Rect("ToastText");
        float centerWidth = Mathf.Max(1f, width - leftWidth - rightWidth - 4f * Margin);
        Text toastLabel = Label(toast);
        Box(toast, leftWidth + 2f * Margin, Margin, centerWidth, TextHeight(toastLabel, centerWidth));
        RectTransform status = Rect("StatusText");
        float statusWidth = Mathf.Min(900f, width - 2f * Margin);
        float statusHeight = TextHeight(Label(status), statusWidth);
        Box(status, (width - statusWidth) / 2f, Mathf.Max(y, rightY) + Margin, statusWidth, statusHeight);

        RectTransform start = Rect("StartButton/Text");
        float startWidth = width - 2f * Margin;
        float startHeight = TextHeight(Label(start), startWidth);
        Box(start, Margin, (height - startHeight) / 2f, startWidth, startHeight);
        RectTransform menu = Rect("MenuButton");
        float menuWidth = Mathf.Min(width * 0.4f, Mathf.Max(120f, (Label(menu)?.preferredWidth ?? 0f) + 32f));
        ButtonBox(menu, Margin, Margin, menuWidth, ButtonHeight(menu, menuWidth));

        RectTransform boost = Rect("BoostButton");
        bool boostOnLeft = boost != null && boost.anchorMin.x < 0.5f;
        ButtonBox(Rect("PauseButton"), Margin, height - (boostOnLeft ? 320f : 114f), 90f, 90f);
    }

    static readonly string[] LeftHud = { "DistanceText", "SpeedText", "BoostText" };
    static readonly string[] RightHud = { "BestDistanceText", "GearText", "ScoreText" };
    static readonly string[] Tabs = { "GoalsTabButton", "SettingsTabButton", "LanguageTabButton", "StatsTabButton" };
    static readonly string[] Actions = { "HomeButton", "RestartButton", "ResumeButton" };

    void LayoutMenu(RectTransform panel)
    {
        if (panel == null) return;
        RectTransform back = Child(panel, "BackButton");
        float width = Mathf.Min(panel.rect.width * 0.4f, Mathf.Max(140f, (Label(back)?.preferredWidth ?? 0f) + 32f));
        float height = ButtonHeight(back, width);
        ButtonBox(back, panel.rect.width - width - Margin, panel.rect.height - height - Margin, width, height);
        LayoutTabs(panel, height + 2f * Margin);
    }

    void LayoutPause(RectTransform panel)
    {
        if (panel == null) return;
        float actionWidth = Mathf.Min(440f, panel.rect.width * 0.28f);
        RectTransform actions = Child(panel, "ActionList");
        Box(actions, Margin, Margin, actionWidth, panel.rect.height - 2f * Margin);
        float y = 0f;
        foreach (string name in Actions)
        {
            RectTransform button = Child(actions, name);
            float height = ButtonHeight(button, actionWidth);
            ButtonBox(button, 0f, y, actionWidth, height);
            y += height + Gap;
        }
        RectTransform tabs = Child(panel, "TabArea");
        Box(tabs, actionWidth + 2f * Margin, 0f, panel.rect.width - actionWidth - 3f * Margin, panel.rect.height);
        LayoutTabs(tabs, Margin);
    }

    void LayoutTabs(RectTransform panel, float bottom)
    {
        if (panel == null) return;
        RectTransform bar = Child(panel, "TabBar");
        float available = Mathf.Max(1f, panel.rect.width - 2f * Margin);
        int columns = available < 800f ? 2 : 4;
        float y = 0f;
        for (int start = 0; start < Tabs.Length; start += columns)
        {
            float natural = 0f;
            for (int i = start; i < start + columns; i++)
                natural += (Label(Child(bar, Tabs[i]))?.preferredWidth ?? 0f) + 32f;
            float space = available - (columns - 1) * Gap;
            float scale = Mathf.Min(1f, space / Mathf.Max(1f, natural));
            float rowHeight = 64f;
            for (int i = start; i < start + columns; i++)
            {
                RectTransform button = Child(bar, Tabs[i]);
                float width = ((Label(button)?.preferredWidth ?? 0f) + 32f) * scale;
                rowHeight = Mathf.Max(rowHeight, ButtonHeight(button, width));
            }
            float x = Mathf.Max(0f, (available - natural * scale - (columns - 1) * Gap) / 2f);
            for (int i = start; i < start + columns; i++)
            {
                RectTransform button = Child(bar, Tabs[i]);
                float width = ((Label(button)?.preferredWidth ?? 0f) + 32f) * scale;
                ButtonBox(button, x, y, width, rowHeight);
                x += width + Gap;
            }
            y += rowHeight + Gap;
        }
        Box(bar, Margin, Margin, available, y - Gap);
        Stretch(Child(panel, "ContentArea"), Margin, Margin + y, Margin, bottom);
    }

    void LayoutChoices(RectTransform panel)
    {
        if (panel == null) return;
        RectTransform confirm = Child(panel, "ConfirmButton");
        float confirmWidth = Mathf.Min(panel.rect.width - 2f * Margin, Mathf.Max(260f, (Label(confirm)?.preferredWidth ?? 0f) + 32f));
        float confirmHeight = ButtonHeight(confirm, confirmWidth);
        ButtonBox(confirm, (panel.rect.width - confirmWidth) / 2f, panel.rect.height - confirmHeight - Margin, confirmWidth, confirmHeight);
        RectTransform content = Child(panel, "ChoicesLayout");
        if (content == null) return;
        float width = Mathf.Max(1f, panel.rect.width - 2f * Margin);
        int columns = width >= 1100f ? 3 : (width >= 650f ? 2 : 1);
        float cardWidth = (width - (columns - 1) * Gap) / columns;
        float y = 0f;
        for (int start = 0; start < 3; start += columns)
        {
            float rowHeight = 0f;
            for (int i = start; i < Mathf.Min(3, start + columns); i++)
            {
                RectTransform card = Child(content, "Choice" + i);
                RectTransform title = Child(card, "TitleText");
                RectTransform desc = Child(card, "DescriptionText");
                float textWidth = cardWidth - 2f * Margin;
                float titleHeight = TextHeight(Label(title), textWidth);
                float descHeight = TextHeight(Label(desc), textWidth);
                Box(title, Margin, Margin, textWidth, titleHeight);
                Box(desc, Margin, Margin + titleHeight + Gap, textWidth, descHeight);
                rowHeight = Mathf.Max(rowHeight, titleHeight + descHeight + 2f * Margin + Gap);
            }
            for (int i = start; i < Mathf.Min(3, start + columns); i++)
                Box(Child(content, "Choice" + i), (i - start) * (cardWidth + Gap), y, cardWidth, rowHeight);
            y += rowHeight + Gap;
        }
        // No scrolling or hidden overflow: fit the whole card group above Confirm.
        // Font size remains 40. Extreme translations / narrow screens scale the group uniformly.
        float naturalHeight = y - Gap;
        float availableHeight = Mathf.Max(1f, panel.rect.height - confirmHeight - 3f * Margin);
        float scale = Mathf.Min(1f, availableHeight / Mathf.Max(1f, naturalHeight));
        content.localScale = new Vector3(scale, scale, 1f);
        Box(content, (panel.rect.width - width * scale) / 2f,
            Margin + (availableHeight - naturalHeight * scale) / 2f, width, naturalHeight);
    }
}
