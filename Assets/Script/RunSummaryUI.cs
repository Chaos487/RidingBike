using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 结算面板(Run Complete):RunManager 摔车 runSummaryDelaySeconds 秒之后把这一局的
/// RunSummaryData 打包好交过来(RunManager.OnRunSummaryReady)，这里只管显示和处理
/// Home/Play Again 两个按钮——不读任何游戏系统、不自己算分、不自己判断破紀錄。
///
/// 整个面板(背景/统计行/按钮)完全用代码在运行时搭建，不在 EndlessRunCanvas.prefab 里
/// 手动摆(参考 RunManager.SetupFeatList 的做法)：内容是数据驱动的可变结构(New High Score
/// 那一行显不显示要等结算那一刻才知道)，手改预制体 YAML 出错风险比这大得多，而且这边
/// 没法打开 Unity 预览效果，纯代码搭建至少能保证结构本身是对的。
///
/// 背景直接复用 NodePanel/MenuPanel/PausePanel 同一套全屏高斯模糊(ScreenBlurState +
/// Assets/Shaders/BlurredPanelBackground.mat)，不引入新的后处理系统。这个材质的 Shader
/// 只吃 _TintColor 这一个材质属性、完全不读 UI 顶点色，所以背景本身没法用 CanvasGroup/
/// Image.color 做淡入——跟其它几个弹窗一样直接瞬间出现；"淡入"效果做在背景之上的统计行/
/// Total/New High Score 这几块真正需要分先后出现的内容上(见 PlayRevealAnimation)。
/// </summary>
public class RunSummaryUI : MonoBehaviour
{
    RunManager runManager;

    GameObject panelRoot;
    Text distanceValueText;
    Text gearsValueText;
    Text landingQualityValueText;
    Text trickLabelText;
    Text trickValueText;
    Text nearMissValueText;
    Text totalValueText;
    Text gearsEarnedText;
    GameObject newHighScoreRow;

    CanvasGroup distanceRowGroup;
    CanvasGroup gearsRowGroup;
    CanvasGroup landingQualityRowGroup;
    CanvasGroup trickRowGroup;
    CanvasGroup nearMissRowGroup;
    CanvasGroup totalRowGroup;
    CanvasGroup highScoreRowGroup;

    public void Initialize(RunManager manager)
    {
        runManager = manager;
        runManager.OnRunSummaryReady += Show;
    }

    void Awake()
    {
        BuildUI();
    }

    void OnDestroy()
    {
        if (runManager != null) runManager.OnRunSummaryReady -= Show;
    }

    void Show(RunSummaryData data)
    {
        distanceValueText.text = $"{data.distance:N0} m";
        gearsValueText.text = $"{data.gearsCollected:N0}";

        landingQualityValueText.text = $"{data.landingQualityScore:N0}";

        trickLabelText.text = data.bestTrickScore > 0
            ? $"✎  Trick Score - best: {data.bestTrickScore:N0}"
            : "✎  Trick Score";
        trickValueText.text = $"{data.trickScore:N0}";

        nearMissValueText.text = $"{data.nearMissScore:N0}";

        totalValueText.text = $"{data.totalScore:N0}";
        gearsEarnedText.text = $"⚙ {data.gearsCollected:N0}";

        newHighScoreRow.SetActive(data.isNewHighScore);

        panelRoot.SetActive(true);
        PlayRevealAnimation();
    }

    /// <summary>成绩逐行出现 -> Total 最后出现 -> New High Score 最后出现，总时长控制在 1 秒
    /// 左右(十、的要求)。用一个 Sequence + Insert 手动排时间轴，比 for 循环叠 Append 更好控制
    /// 总时长，不需要额外写一套"动画系统"。</summary>
    void PlayRevealAnimation()
    {
        distanceRowGroup.alpha = 0f;
        gearsRowGroup.alpha = 0f;
        landingQualityRowGroup.alpha = 0f;
        trickRowGroup.alpha = 0f;
        nearMissRowGroup.alpha = 0f;
        totalRowGroup.alpha = 0f;
        highScoreRowGroup.alpha = 0f;

        const float rowFade = 0.22f;

        DOTween.Sequence()
            .SetUpdate(true) // 这时候 Time.timeScale 已经是 0(EnterRunSummary 里冻结的)，动画要走不受影响的 unscaled time
            .Insert(0.00f, distanceRowGroup.DOFade(1f, rowFade))
            .Insert(0.10f, gearsRowGroup.DOFade(1f, rowFade))
            .Insert(0.20f, landingQualityRowGroup.DOFade(1f, rowFade))
            .Insert(0.30f, trickRowGroup.DOFade(1f, rowFade))
            .Insert(0.40f, nearMissRowGroup.DOFade(1f, rowFade))
            .Insert(0.55f, totalRowGroup.DOFade(1f, rowFade))
            .Insert(0.75f, highScoreRowGroup.DOFade(1f, rowFade));
    }

    void HomeClicked() => ReloadScene();
    void PlayAgainClicked() => ReloadScene();

    /// <summary>项目没有单独的主菜单场景——"回主菜单"就是重新加载当前场景，自然会落回
    /// EnterStartGate 的 tap to start 画面，Home 和 Play Again 目前是同一个行为，跟
    /// PauseController.ReloadScene 是同一套做法(讨论方案时已经跟你确认过)。</summary>
    void ReloadScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void BuildUI()
    {
        panelRoot = new GameObject("RunSummaryPanel", typeof(RectTransform));
        panelRoot.transform.SetParent(transform, false);
        RectTransform panelRect = (RectTransform)panelRoot.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        BuildBackground(panelRect);

        CreateText(panelRect, "Run Complete", 52, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 70f));

        BuildRows(panelRect);
        BuildBottomBar(panelRect);

        panelRoot.SetActive(false);
    }

    void BuildBackground(Transform parent)
    {
        GameObject bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)bg.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = bg.AddComponent<Image>();
        Material blurMaterial = FindBlurMaterial();
        if (blurMaterial != null)
        {
            image.material = blurMaterial;
            image.color = new Color(0.12f, 0.08f, 0.1f, 0.96f); // 跟 NodePanel/MenuPanel/PausePanel 背景保持一致的 Tint，Shader 实际只读材质自己的 _TintColor，这里只是保持数值一致方便以后对照
        }
        else
        {
            // 找不到模糊材质的兜底：至少给一层纯色半透明背景，不至于结算面板背景直接透空。
            Debug.LogWarning("[RunSummaryUI] 找不到 BlurredPanelBackground 材质，结算面板背景退回纯色遮罩(没有模糊效果)。");
            image.color = new Color(0.06f, 0.05f, 0.06f, 0.9f);
        }
        image.raycastTarget = true; // 挡住结算画面后面的游戏世界/HUD，不让点击穿透下去
    }

    void BuildRows(Transform parent)
    {
        GameObject rows = new GameObject("RowsContainer", typeof(RectTransform));
        rows.transform.SetParent(parent, false);
        RectTransform rowsRect = (RectTransform)rows.transform;
        rowsRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowsRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowsRect.pivot = new Vector2(0.5f, 1f);
        rowsRect.anchoredPosition = new Vector2(0f, 90f);
        rowsRect.sizeDelta = new Vector2(680f, 0f);

        VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true; // 让每行按自己的 LayoutElement.preferredHeight 排布，不然行高会退回不可预测的默认 RectTransform 尺寸
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rows.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 上半段是纯展示的"本局数据"(不计分,Distance/Gears 目前没有换算成分数);下半段是
        // 真正累加进 Total 的三项(Landing Quality/Trick/Near Miss),紧挨着 Total 摆在一起，
        // 这样"Total 是哪几行加出来的"从面板上就能直接看出来，不用去猜——2026-09-23 根据
        // 实机反馈("370 是怎么算出来的")加的两行 + 这个分组，取代了之前"三行凑数、Total
        // 对不上"的版本。
        distanceRowGroup = CreateStatRow(rowsRect, "▲  Distance Travelled", out _, out distanceValueText, emphasized: false);
        gearsRowGroup = CreateStatRow(rowsRect, "⚙  Gears Collected", out _, out gearsValueText, emphasized: false);

        CreateSpacer(rowsRect, 14f);

        landingQualityRowGroup = CreateStatRow(rowsRect, "✓  Landing Quality", out _, out landingQualityValueText, emphasized: false);
        trickRowGroup = CreateStatRow(rowsRect, "✎  Trick Score", out trickLabelText, out trickValueText, emphasized: false);
        nearMissRowGroup = CreateStatRow(rowsRect, "!  Near Miss", out _, out nearMissValueText, emphasized: false);
        totalRowGroup = CreateStatRow(rowsRect, "Total", out _, out totalValueText, emphasized: true);

        // New High Score:单独一行、居中、只在破紀錄时显示——放进同一个 VerticalLayoutGroup 里,
        // 这样 SetActive(false) 的时候会自动不占位置，不用另外算它的位置。
        newHighScoreRow = new GameObject("NewHighScoreRow", typeof(RectTransform));
        newHighScoreRow.transform.SetParent(rowsRect, false);
        LayoutElement badgeLayout = newHighScoreRow.AddComponent<LayoutElement>();
        badgeLayout.preferredHeight = 44f;
        highScoreRowGroup = newHighScoreRow.AddComponent<CanvasGroup>();
        Text badgeText = CreateText(newHighScoreRow.transform, "★ New High Score", 28, FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        badgeText.color = new Color(1f, 0.85f, 0.3f, 1f);
        RectTransform badgeRt = (RectTransform)badgeText.transform;
        badgeRt.anchorMin = Vector2.zero;
        badgeRt.anchorMax = Vector2.one;
        badgeRt.offsetMin = Vector2.zero;
        badgeRt.offsetMax = Vector2.zero;
    }

    // 纯留白，把"展示用数据"和"真正计分的三项"这两组行隔开一点距离，不用画分割线。
    static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(parent, false);
        spacer.AddComponent<LayoutElement>().preferredHeight = height;
    }

    CanvasGroup CreateStatRow(Transform parent, string label, out Text labelText, out Text valueText, bool emphasized)
    {
        GameObject row = new GameObject(emphasized ? "TotalRow" : "StatRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = emphasized ? 64f : 46f;

        if (emphasized)
        {
            Image stripe = row.AddComponent<Image>();
            stripe.color = new Color(1f, 1f, 1f, 0.08f); // Total 行用一条淡淡的底色跟其它行区分开，不用额外的边框/贴图
        }

        CanvasGroup group = row.AddComponent<CanvasGroup>();

        int fontSize = emphasized ? 32 : 26;
        FontStyle style = emphasized ? FontStyle.Bold : FontStyle.Normal;

        labelText = CreateText(row.transform, label, fontSize, style, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(0.62f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform labelRt = (RectTransform)labelText.transform;
        labelRt.offsetMin = new Vector2(20f, 0f);
        labelRt.offsetMax = new Vector2(-8f, 0f);

        valueText = CreateText(row.transform, string.Empty, fontSize, style, TextAnchor.MiddleRight,
            new Vector2(0.62f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform valueRt = (RectTransform)valueText.transform;
        valueRt.offsetMin = new Vector2(8f, 0f);
        valueRt.offsetMax = new Vector2(-20f, 0f);

        return group;
    }

    void BuildBottomBar(Transform parent)
    {
        CreateTextButton(parent, "Home", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(40f, 40f), new Vector2(180f, 64f), TextAnchor.MiddleLeft).onClick.AddListener(HomeClicked);

        gearsEarnedText = CreateText(parent, "⚙ 0", 30, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(240f, 56f));
        gearsEarnedText.color = new Color(1f, 0.82f, 0.35f, 1f);

        CreateTextButton(parent, "Play Again", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(220f, 64f), TextAnchor.MiddleRight).onClick.AddListener(PlayAgainClicked);
    }

    Button CreateTextButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
    {
        Text text = CreateText(parent, label, 34, FontStyle.Normal, alignment, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        text.raycastTarget = true;

        Button button = text.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;
        return button;
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
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    // 跟 EndlessRunBootstrap.FindPrefab/FindSettings 是同一套思路:Editor 里按类型/文件名全项目搜,
    // 找不到(比如打包后的版本)就退回 Resources.Load。这个材质本身不在 Resources 目录下,
    // 现在只有 Editor 内搜索这条路能找到它——留着 Resources.Load 分支是为了以后万一挪进
    // Resources 目录也不用改这份代码。
    static Material FindBlurMaterial()
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("BlurredPanelBackground t:Material");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != "BlurredPanelBackground") continue;

            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
        }
#endif
        return Resources.Load<Material>("BlurredPanelBackground");
    }
}
