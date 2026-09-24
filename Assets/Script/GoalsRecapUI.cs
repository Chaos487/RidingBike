using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 摔车结算最先弹出的一屏(参考 Alto's Odyssey 的 Goals 截图)——展示当前 Level 的 3 个目标、
/// 星星、Level 号，点 Next 才继续显示 RunSummaryUI。`RunManager.OnRunSummaryReady` 现在
/// 唯一的订阅方就是这里，`RunSummaryUI` 不再自己订阅(见 RunSummaryUI.Show 顶部注释)——
/// 两个面板轮流独占屏幕，不能都直接订阅同一个事件同时弹出来。
///
/// 目标的完成判定/发奖励/推进 Level 全部交给 `GoalManager.SettleCurrentLevel()`——这里只管
/// 拿到快照(`GoalsSnapshot`)之后怎么画，不自己算任何东西，也不碰 gameplay。
///
/// 面板本身完全用代码在运行时搭建，做法跟 `RunSummaryUI` 一样(背景模糊复用
/// `BlurredPanelBackground.mat`，行/星星复用 `GoalsUIUtil`)。
/// </summary>
public class GoalsRecapUI : MonoBehaviour
{
    RunManager runManager;
    GoalManager goalManager;
    RunSummaryUI runSummaryUI;

    GameObject panelRoot;
    Transform rowsContainer;

    RunSummaryData pendingSummaryData;

    public void Initialize(RunManager manager, GoalManager goals, RunSummaryUI summaryUI)
    {
        runManager = manager;
        goalManager = goals;
        runSummaryUI = summaryUI;
        runManager.OnRunSummaryReady += HandleRunSummaryReady;
    }

    void Awake()
    {
        BuildUI();
    }

    void OnDestroy()
    {
        if (runManager != null) runManager.OnRunSummaryReady -= HandleRunSummaryReady;
    }

    void HandleRunSummaryReady(RunSummaryData data)
    {
        pendingSummaryData = data;

        GoalsSnapshot snapshot = goalManager != null ? goalManager.SettleCurrentLevel() : default;
        Populate(snapshot);

        // 背景模糊要真的跑过 ScreenBlurFeature 这趟渲染 Pass 才有内容可采样，见
        // RunSummaryUI.Show 里同样的注释——这个面板打开之后也不会再关，交给 ReloadScene()
        // 里的 ScreenBlurState.Reset() 统一清零。
        ScreenBlurState.BeginBlur();
        panelRoot.SetActive(true);
    }

    void Populate(GoalsSnapshot snapshot)
    {
        // 每次重新生成整块内容——比维护一堆可变数量的 Text 引用简单，这个面板一局只弹一次，
        // 重建开销完全不是问题。
        for (int i = rowsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(rowsContainer.GetChild(i).gameObject);
        }

        foreach (GoalRow row in snapshot.goals)
        {
            GoalsUIUtil.BuildGoalRow(rowsContainer, 44f, row.title, row.completed);
        }

        GoalsUIUtil.BuildStarRow(rowsContainer, 50f, snapshot.completedCount);
        GoalsUIUtil.BuildLevelLabel(rowsContainer, 44f, snapshot.levelNumber);
    }

    void NextClicked()
    {
        panelRoot.SetActive(false);
        runSummaryUI.Show(pendingSummaryData);
    }

    void BuildUI()
    {
        panelRoot = new GameObject("GoalsRecapPanel", typeof(RectTransform));
        panelRoot.transform.SetParent(transform, false);
        RectTransform panelRect = (RectTransform)panelRoot.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        BuildBackground(panelRect);

        GoalsUIUtil.CreateText(panelRect, LocalizationManager.Get("goalsrecap.title"), 40, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 60f));

        GameObject rows = new GameObject("RowsContainer", typeof(RectTransform));
        rows.transform.SetParent(panelRect, false);
        RectTransform rowsRect = (RectTransform)rows.transform;
        rowsRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowsRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowsRect.pivot = new Vector2(0.5f, 0.5f);
        rowsRect.anchoredPosition = new Vector2(0f, 20f);
        rowsRect.sizeDelta = new Vector2(700f, 0f);

        VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rows.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        rowsContainer = rows.transform;

        CreateTextButton(panelRect, LocalizationManager.Get("goalsrecap.next"), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(180f, 64f), TextAnchor.MiddleRight).onClick.AddListener(NextClicked);

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
            image.color = new Color(0.12f, 0.08f, 0.1f, 0.96f);
        }
        else
        {
            Debug.LogWarning("[GoalsRecapUI] 找不到 BlurredPanelBackground 材质，背景退回纯色遮罩(没有模糊效果)。");
            image.color = new Color(0.06f, 0.05f, 0.06f, 0.9f);
        }
        image.raycastTarget = true;
    }

    Button CreateTextButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
    {
        Text text = GoalsUIUtil.CreateText(parent, label, 34, FontStyle.Normal, alignment, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        text.raycastTarget = true;

        Button button = text.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;
        return button;
    }

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
