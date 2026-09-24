using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StatsTabUI 专用的小控件搭建方法——跟 GoalsUIUtil 是同一个思路(避免 MenuPanel/PausePanel
/// 两处 Tab 面板各画各的走样),独立成一份而不是塞进 GoalsUIUtil，因为这边多了一个 Goals
/// Tab 完全没用过的组件:ScrollRect。Stats 内容比 Goals 多不少(十几行)，固定高度装不下，
/// 需要能滚动;Goals 那边行数少，从来没遇到过这个问题。
/// </summary>
public static class StatsUIUtil
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

    /// <summary>在 panel 下搭一个可滚动的容器，返回真正装行内容的 Transform——调用方往这个
    /// Transform 底下加行(VerticalLayoutGroup + ContentSizeFitter 已经配好)，超出可视区域
    /// 自动可以拖动滚动，不用调用方操心。Viewport 用 RectMask2D 裁切(不需要专门贴一张遮罩图)。</summary>
    public static Transform BuildScrollView(Transform panel, float topPadding, float bottomPadding)
    {
        GameObject scrollObj = new GameObject("ScrollView", typeof(RectTransform));
        scrollObj.transform.SetParent(panel, false);
        RectTransform scrollRt = (RectTransform)scrollObj.transform;
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(40f, bottomPadding);
        scrollRt.offsetMax = new Vector2(-40f, -topPadding);

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform));
        viewportObj.transform.SetParent(scrollObj.transform, false);
        RectTransform viewportRt = (RectTransform)viewportObj.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();
        // RectMask2D 要靠同物体上的一个 Graphic 才会真的裁切子物体——完全透明，纯粹为了
        // 满足这个前提，不参与视觉也不吃点击。
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = false;

        GameObject rowsObj = new GameObject("Rows", typeof(RectTransform));
        rowsObj.transform.SetParent(viewportObj.transform, false);
        RectTransform rowsRt = (RectTransform)rowsObj.transform;
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot = new Vector2(0.5f, 1f);
        rowsRt.anchoredPosition = Vector2.zero;
        rowsRt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = rowsObj.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rowsObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = rowsRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        return rowsObj.transform;
    }

    /// <summary>一行统计数据:左边标签、右边数值，斑马纹交替底色区分相邻行(参考 Alto's
    /// Odyssey 截图里深浅相间的效果)。index 只用来决定纹路深浅，不代表这一行在数据里的位置。</summary>
    public static void BuildStatRow(Transform parent, int index, string label, string value)
    {
        GameObject row = new GameObject("StatRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 48f;

        Image stripe = row.AddComponent<Image>();
        stripe.color = index % 2 == 0 ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0.08f);
        stripe.raycastTarget = false;

        Text labelText = CreateText(row.transform, label, 24, FontStyle.Normal, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(0.65f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform labelRt = (RectTransform)labelText.transform;
        labelRt.offsetMin = new Vector2(20f, 0f);
        labelRt.offsetMax = new Vector2(-8f, 0f);

        Text valueText = CreateText(row.transform, value, 24, FontStyle.Bold, TextAnchor.MiddleRight,
            new Vector2(0.65f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform valueRt = (RectTransform)valueText.transform;
        valueRt.offsetMin = new Vector2(8f, 0f);
        valueRt.offsetMax = new Vector2(-20f, 0f);
    }
}
