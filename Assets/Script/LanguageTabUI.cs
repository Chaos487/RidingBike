using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 参考 Alto's Odyssey 的 Language 面板——7 个语言按钮的网格，点一个就切换
/// `LocalizationManager.CurrentLocale`。接进菜单/暂停面板里现成的 LanguagePanel 占位节点，
/// 跟 GoalsTabUI/StatsTabUI/SettingsTabUI 是同一套路。
///
/// 语言按钮本身显示各自的母语名字("简体中文"/"日本語"这种)，不跟着当前选中的语言翻译——
/// 这样不管玩家现在选的是哪个语言，都能一眼认出自己母语那个按钮，参考图和大部分 App 的
/// 语言选择器都是这么处理的。所以这个 Tab 不用订阅 LocalizationManager.OnLocaleChanged
/// 刷新按钮文字，只需要点击时更新"当前选中"的高亮状态。
/// </summary>
public class LanguageTabUI : MonoBehaviour
{
    static readonly (Locale locale, string nativeName)[] Languages =
    {
        (Locale.English, "English"),
        (Locale.SimplifiedChinese, "简体中文"),
        (Locale.TraditionalChinese, "繁體中文"),
        (Locale.Japanese, "日本語"),
        (Locale.German, "Deutsch"),
        (Locale.French, "Français"),
        (Locale.Spanish, "Español"),
    };

    readonly List<(Locale locale, Image background, Text label)> buttons = new List<(Locale, Image, Text)>();

    public void Initialize()
    {
        Transform menuLanguagePanel = transform.Find("MenuPanel/ContentArea/LanguagePanel");
        Transform pauseLanguagePanel = transform.Find("PausePanel/TabArea/ContentArea/LanguagePanel");

        if (menuLanguagePanel != null) BuildInto(menuLanguagePanel);
        if (pauseLanguagePanel != null) BuildInto(pauseLanguagePanel);

        RefreshSelection();
    }

    void BuildInto(Transform panel)
    {
        // 占位文字节点本身清空——Tab 按钮本身已经写着"Language"，面板里没必要再重复一次标题。
        Text placeholderText = panel.GetComponent<Text>();
        if (placeholderText != null) placeholderText.text = string.Empty;

        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(panel, false);
        RectTransform gridRect = (RectTransform)grid.transform;
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = new Vector2(620f, 340f);

        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(190f, 60f);
        layout.spacing = new Vector2(20f, 16f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        foreach ((Locale locale, string nativeName) in Languages)
        {
            BuildLanguageButton(grid.transform, locale, nativeName);
        }
    }

    void BuildLanguageButton(Transform parent, Locale locale, string nativeName)
    {
        GameObject buttonObj = new GameObject("LanguageButton", typeof(RectTransform));
        buttonObj.transform.SetParent(parent, false);

        Image background = buttonObj.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => HandleLanguageClicked(locale));

        Text label = CreateText(buttonObj.transform, nativeName, 22, FontStyle.Normal, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        label.color = Color.white;
        label.raycastTarget = false;

        buttons.Add((locale, background, label));
    }

    void HandleLanguageClicked(Locale locale)
    {
        LocalizationManager.SetLocale(locale);
        RefreshSelection();
    }

    void RefreshSelection()
    {
        foreach ((Locale locale, Image background, Text label) in buttons)
        {
            bool selected = locale == LocalizationManager.CurrentLocale;
            background.color = selected ? new Color(1f, 1f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.6f);
            label.color = selected ? Color.black : Color.white;
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    Text CreateText(Transform parent, string content, int fontSize, FontStyle style, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)obj.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Text text = obj.AddComponent<Text>();
        text.font = LocalizationManager.GetFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }
}
