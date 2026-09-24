using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// `GoalsTabUI`(菜单/暂停面板里的 Goals Tab)和 `GoalsRecapUI`(摔车结算前的一屏)共用的几个
/// 小控件搭建方法——两处都要画"目标行(带勾选/划线)"和"星星行"，抽出来一份避免两边视觉
/// 走样，是这个项目里少数值得共享的 UI 构件(其它面板各自的 CreateText/CreateButton 都是
/// 各画各的，因为足够简单、不共享也不会走样)。
/// </summary>
public static class GoalsUIUtil
{
    public static Text CreateText(Transform parent, string content, int fontSize, FontStyle style, TextAnchor alignment,
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

    /// <summary>一行目标：完成的话文字前面加勾选符号、变灰，再叠一条细横线做删除线——Unity
    /// 内置的 Text 组件不支持富文本删除线标签，用一条纯色 Image 叠在文字中间画假删除线，
    /// 比为了这一个效果去接 TextMeshPro 简单。挂在 parent 的 VerticalLayoutGroup 里，调用方
    /// 不用管具体摆在哪。</summary>
    public static void BuildGoalRow(Transform parent, float height, string title, bool completed)
    {
        GameObject row = new GameObject("GoalRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        Color color = completed ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
        string content = completed ? $"✓  {title}" : title;

        CreateText(row.transform, content, 26, FontStyle.Normal, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).color = color;

        if (completed)
        {
            GameObject line = new GameObject("StrikeThrough", typeof(RectTransform));
            line.transform.SetParent(row.transform, false);
            RectTransform lineRt = (RectTransform)line.transform;
            lineRt.anchorMin = new Vector2(0.2f, 0.5f);
            lineRt.anchorMax = new Vector2(0.8f, 0.5f);
            lineRt.sizeDelta = new Vector2(0f, 2f);
            Image lineImage = line.AddComponent<Image>();
            lineImage.color = color;
            lineImage.raycastTarget = false;
        }
    }

    /// <summary>参考 Alto's Odyssey 截图里的星星行——填充数量就是当前 Level 已完成的目标数
    /// (0~totalStars)，不另外算"评级"，最简单直接。</summary>
    public static void BuildStarRow(Transform parent, float height, int filledCount, int totalStars = 3)
    {
        GameObject row = new GameObject("StarRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        filledCount = Mathf.Clamp(filledCount, 0, totalStars);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < totalStars; i++)
        {
            sb.Append(i < filledCount ? '★' : '☆');
            if (i < totalStars - 1) sb.Append(' ');
        }

        CreateText(row.transform, sb.ToString(), 30, FontStyle.Normal, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero)
            .color = new Color(1f, 0.85f, 0.3f, 1f);
    }

    /// <summary>"Level N" 那一行——没有对应的奖章/丝带美术资源(项目里也没有生图工具能画)，
    /// 用加粗文字占位，以后有美术资源了直接在这一行旁边加个 Image 就行，不用改布局。</summary>
    public static void BuildLevelLabel(Transform parent, float height, int levelNumber)
    {
        GameObject row = new GameObject("LevelLabel", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        string content = string.Format(LocalizationManager.Get("goals.level"), levelNumber);
        CreateText(row.transform, content, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }
}
