using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 结算面板(Run Complete):不自己订阅 RunManager.OnRunSummaryReady——摔车之后会先弹
/// GoalsRecapUI(本局目标进度)，玩家点 Next 才由它调这里公开的 Show(data)。这里只管
/// 显示和处理 Home/Play Again 两个按钮——不读任何游戏系统、不自己算分、不自己判断破紀錄。
///
/// 整个面板(背景/统计行/按钮)完全用代码在运行时搭建，不在 EndlessRunCanvas.prefab 里
/// 手动摆(参考 RunManager.SetupFeatList 的做法)：内容是数据驱动的可变结构(New High
/// Score/New Distance Record 这两行显不显示要等结算那一刻才知道)，手改预制体 YAML 出错
/// 风险比这大得多，而且这边没法打开 Unity 预览效果，纯代码搭建至少能保证结构本身是对的。
///
/// 每一行右侧显示的都是这一项算出来的分数(不是原始数值)——全部加起来正好等于 Total，
/// 原始数值(比如 793m、103 个齿轮)折进行标签文字里显示，参考 Trick Score 那行早就有的
/// "- best: X" 副标题写法。这是 2026-09-23 根据实机反馈("Total 数字跟显示的行对不上")
/// 调整过一版之后，这次接入 ScoreSystem 时又进一步统一成的最终形态：不再分"展示组/计分组"
/// 两组，回到一张单一列表，因为现在每一项都真的在计分了。
///
/// 背景直接复用 NodePanel/MenuPanel/PausePanel 同一套全屏高斯模糊(ScreenBlurState +
/// Assets/Shaders/BlurredPanelBackground.mat)，不引入新的后处理系统。这个材质的 Shader
/// 只吃 _TintColor 这一个材质属性、完全不读 UI 顶点色，所以背景本身没法用 CanvasGroup/
/// Image.color 做淡入——跟其它几个弹窗一样直接瞬间出现；"淡入"效果做在背景之上的统计行/
/// Total/New High Score 这几块真正需要分先后出现的内容上(见 PlayRevealAnimation)。
/// </summary>
public class RunSummaryUI : MonoBehaviour
{
    GameObject panelRoot;

    Text distanceValueText;
    Text gearsValueText;
    Text nodeValueText;
    Text maxHpValueText;
    Text landingQualityValueText;
    Text trickLabelText;
    Text trickValueText;
    Text nearMissValueText;
    Text totalValueText;
    Text gearsEarnedText;

    GameObject distanceRecordRow;
    Text distanceRecordValueText;
    GameObject newHighScoreRow;

    // 动画按这个顺序依次淡入——只包含固定存在的 7 行 + Total，New Distance Record/New High
    // Score 这两个条件行不放进来统一排(它们各自的时机在 PlayRevealAnimation 里单独处理，
    // 因为"是否出现"要等 Show() 那一刻才知道，混进循环里反而不好读)。
    readonly List<CanvasGroup> orderedRowGroups = new List<CanvasGroup>();
    CanvasGroup distanceRecordRowGroup;
    CanvasGroup totalRowGroup;
    CanvasGroup highScoreRowGroup;

    void Awake()
    {
        BuildUI();
    }

    public void Show(RunSummaryData data)
    {
        distanceValueText.text = $"{data.distanceScore:N0}";
        gearsValueText.text = $"{data.gearScore:N0}";
        nodeValueText.text = $"{data.nodeScore:N0}";
        maxHpValueText.text = $"{data.maxHpScore:N0}";
        landingQualityValueText.text = $"{data.landingQualityScore:N0}";

        trickLabelText.text = data.bestTrickScore > 0
            ? $"✎  Trick Score - best: {data.bestTrickScore:N0}"
            : "✎  Trick Score";
        trickValueText.text = $"{data.trickScore:N0}";

        nearMissValueText.text = $"{data.nearMissScore:N0}";
        totalValueText.text = $"{data.totalScore:N0}";
        gearsEarnedText.text = $"⚙ {data.gearsCollected:N0}";

        distanceRecordValueText.text = $"+{data.distanceRecordBonus:N0}";
        distanceRecordRow.SetActive(data.isNewDistanceRecord);
        newHighScoreRow.SetActive(data.isNewHighScore);

        // 原始数值(793m / 103 个齿轮…)只在这里更新标签文字，不参与上面几行的"分数"显示——
        // 每次 Show() 都要重新拼一遍，因为标签是运行时字符串，不能在 BuildUI() 里写死。
        SetRowLabel(distanceValueText, "▲", $"Distance Travelled ({data.distance:N0}m)");
        SetRowLabel(gearsValueText, "⚙", $"Gears Collected ({data.gearsCollected:N0})");
        SetRowLabel(nodeValueText, "◆", $"Nodes Passed ({data.nodeCount:N0})");
        SetRowLabel(maxHpValueText, "♥", $"Max HP ({data.finalMaxHp:0})");

        // 背景模糊材质要等 ScreenBlurFeature 真的跑了这趟渲染 Pass 才有内容可采样——
        // 之前这里漏调了这一句，面板背景实际上一直没有真的模糊过。这个面板打开之后不会再关
        // (Home/Play Again 直接重载场景)，不需要对应的 EndBlur()，ReloadScene() 里会用
        // ScreenBlurState.Reset() 统一清零，不依赖这里配对调用。
        ScreenBlurState.BeginBlur();

        panelRoot.SetActive(true);
        PlayRevealAnimation();
    }

    static void SetRowLabel(Text valueText, string icon, string text)
    {
        // 每一行的 Text 层级是 row -> [Label, Value]，valueText 的父物体就是 row，
        // 第一个子物体固定是 Label——用 GetChild(0) 比再存一堆 Text 字段省事。
        Text labelText = valueText.transform.parent.GetChild(0).GetComponent<Text>();
        labelText.text = $"{icon}  {text}";
    }

    /// <summary>成绩逐行出现 -> Total 最后出现 -> New High Score 最后出现，总时长控制在 1 秒
    /// 左右(十、的要求)。行数比最初设计多了不少(接入 ScoreSystem 之后从 3 行涨到 7 行 +
    /// 最多 2 个条件行)，stagger 间隔跟着缩短，不然总时长会明显超过 1 秒。顺序严格按阅读
    /// 顺序来:7 个固定行 -> New Distance Record(如果有) -> Total -> New High Score(如果有)，
    /// 不能把 Total 提前混进固定行的循环里，不然会出现"Total 比 New Distance Record 先出现"
    /// 这种跟"往上数几行正好加总"的阅读顺序矛盾的观感。</summary>
    void PlayRevealAnimation()
    {
        const float rowStagger = 0.07f;
        const float rowFade = 0.18f;

        Sequence seq = DOTween.Sequence().SetUpdate(true); // 这时候 Time.timeScale 已经是 0(EnterRunSummary 里冻结的)，动画要走不受影响的 unscaled time

        float t = 0f;
        foreach (CanvasGroup group in orderedRowGroups)
        {
            group.alpha = 0f;
            seq.Insert(t, group.DOFade(1f, rowFade));
            t += rowStagger;
        }

        // 不显示的时候 SetActive(false) 已经让它不占布局空间，这里空跑一次淡入动画没有
        // 副作用(反正看不见)，不用为了"是否显示"另外分支处理时间轴。
        distanceRecordRowGroup.alpha = 0f;
        seq.Insert(t, distanceRecordRowGroup.DOFade(1f, rowFade));
        t += rowStagger + 0.05f;

        totalRowGroup.alpha = 0f;
        seq.Insert(t, totalRowGroup.DOFade(1f, rowFade));
        t += rowFade + 0.05f;

        highScoreRowGroup.alpha = 0f;
        seq.Insert(t, highScoreRowGroup.DOFade(1f, rowFade));
    }

    void HomeClicked() => ReloadScene();
    void PlayAgainClicked() => ReloadScene();

    /// <summary>项目没有单独的主菜单场景——"回主菜单"就是重新加载当前场景，自然会落回
    /// EnterStartGate 的 tap to start 画面，Home 和 Play Again 目前是同一个行为，跟
    /// PauseController.ReloadScene 是同一套做法(讨论方案时已经跟你确认过)。</summary>
    void ReloadScene()
    {
        Time.timeScale = 1f;
        // ScreenBlurState 是 static,不会跟着场景重开自动清零——这个面板打开的时候调过
        // BeginBlur() 却不会走到配对的 EndBlur(),这里统一清一次，见 ScreenBlurState.Reset() 注释。
        ScreenBlurState.Reset();
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

        CreateText(panelRect, "Run Complete", 46, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 60f));

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
        rowsRect.anchoredPosition = new Vector2(0f, 130f);
        rowsRect.sizeDelta = new Vector2(680f, 0f);

        VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true; // 让每行按自己的 LayoutElement.preferredHeight 排布，不然行高会退回不可预测的默认 RectTransform 尺寸
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rows.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 统一列表——每一行右侧都是分数，全部加起来正好等于 Total(见类顶部注释)。
        // Distance/Gears/Node/Max HP 这四行先建空标签，Show() 时用 SetRowLabel 把原始数值
        // 拼进去(标签要嵌运行时数据，不能在这里写死)。
        orderedRowGroups.Add(CreateStatRow(rowsRect, string.Empty, out distanceValueText, false, out _, out _));
        orderedRowGroups.Add(CreateStatRow(rowsRect, string.Empty, out gearsValueText, false, out _, out _));
        orderedRowGroups.Add(CreateStatRow(rowsRect, string.Empty, out nodeValueText, false, out _, out _));
        orderedRowGroups.Add(CreateStatRow(rowsRect, string.Empty, out maxHpValueText, false, out _, out _));
        orderedRowGroups.Add(CreateStatRow(rowsRect, "✓  Landing Quality", out landingQualityValueText, false, out _, out _));
        orderedRowGroups.Add(CreateStatRow(rowsRect, "✎  Trick Score", out trickValueText, false, out trickLabelText, out _));
        orderedRowGroups.Add(CreateStatRow(rowsRect, "!  Near Miss", out nearMissValueText, false, out _, out _));

        // New Distance Record：只在破紀錄时显示，紧跟在固定行之后、Total 之前——它的分数
        // 也计入 Total，摆在这里最符合"往上数几行正好加总"的阅读顺序。
        distanceRecordRowGroup = CreateStatRow(rowsRect, "▲  New Distance Record", out distanceRecordValueText, false, out _, out distanceRecordRow);
        distanceRecordValueText.color = new Color(1f, 0.85f, 0.3f, 1f);

        totalRowGroup = CreateStatRow(rowsRect, "Total", out totalValueText, true, out _, out _);

        // New High Score：只在破紀錄时显示，放在 Total 之后——纯粹的破紀錄提示，不像
        // New Distance Record 那样带具体分数(它已经算在 Total 里了，这里不用重复显示)。
        newHighScoreRow = new GameObject("NewHighScoreRow", typeof(RectTransform));
        newHighScoreRow.transform.SetParent(rowsRect, false);
        LayoutElement badgeLayout = newHighScoreRow.AddComponent<LayoutElement>();
        badgeLayout.preferredHeight = 40f;
        highScoreRowGroup = newHighScoreRow.AddComponent<CanvasGroup>();
        Text badgeText = CreateText(newHighScoreRow.transform, "★ New High Score", 26, FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        badgeText.color = new Color(1f, 0.85f, 0.3f, 1f);
        RectTransform badgeRt = (RectTransform)badgeText.transform;
        badgeRt.anchorMin = Vector2.zero;
        badgeRt.anchorMax = Vector2.one;
        badgeRt.offsetMin = Vector2.zero;
        badgeRt.offsetMax = Vector2.zero;
    }

    // label 传空字符串的话，行会先建一个空标签，留给 Show() 用 SetRowLabel 动态拼(Distance/
    // Gears/Node/Max HP 这几行的标签要嵌运行时数值，不能在这里写死)；labelText/rowObject
    // 两个 out 参数不需要的调用点一律传 out _ 丢弃——New Distance Record 是唯一需要
    // rowObject(拿去在 Show() 里 SetActive)的行，Trick Score 是唯一需要 labelText(拿去在
    // Show() 里拼 "- best: X" 副标题)的行。
    CanvasGroup CreateStatRow(Transform parent, string label, out Text valueText, bool emphasized, out Text labelText, out GameObject rowObject)
    {
        GameObject row = new GameObject(emphasized ? "TotalRow" : "StatRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        rowObject = row;

        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = emphasized ? 56f : 40f;

        if (emphasized)
        {
            Image stripe = row.AddComponent<Image>();
            stripe.color = new Color(1f, 1f, 1f, 0.08f); // Total 行用一条淡淡的底色跟其它行区分开，不用额外的边框/贴图
        }

        CanvasGroup group = row.AddComponent<CanvasGroup>();

        int fontSize = emphasized ? 30 : 24;
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
