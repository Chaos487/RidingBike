using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 参考 Alto's Odyssey：菜单/暂停面板里现成的 Goals Tab(TabGroupController 已经搭好 Tab
/// 切换逻辑，见该类注释)之前只是占位文字("Goals")。这里在运行时把清单内容画进已有的
/// GoalsPanel 节点，不改 EndlessRunCanvas.prefab——跟 RunSummaryUI 一样，内容是数据驱动的
/// (星星数量、勾选状态都要等 GoalManager 给数据)，不在预制体里手摆。
///
/// MenuPanel 和 PausePanel 各有一份自己的 GoalsPanel(两边的 TabGroupController 是分开
/// 实例化的)，两边显示同一份数据，这里各画一遍。
///
/// 纯只读：调 GoalManager.GetDisplaySnapshot()，不发奖励、不推进 Level(那是 GoalsRecapUI
/// 结算那一刻才做的事)。Back 按钮是 MenuPanel/PausePanel 本来就有的共享按钮，这里不用管。
/// </summary>
public class GoalsTabUI : MonoBehaviour
{
    GoalManager goalManager;
    Transform[] goalsPanels;

    public void Initialize(GoalManager goals)
    {
        goalManager = goals;

        Transform menuGoalsPanel = transform.Find("MenuPanel/ContentArea/GoalsPanel");
        Transform pauseGoalsPanel = transform.Find("PausePanel/TabArea/ContentArea/GoalsPanel");
        goalsPanels = new[] { menuGoalsPanel, pauseGoalsPanel };

        foreach (Transform panel in goalsPanels)
        {
            if (panel != null) BuildInto(panel);
        }

        Refresh();

        // Level 行标签("Level N")要跟着语言走——Refresh() 每次都整块重建，重新调一遍就
        // 自然带上新语言，不用另外维护一份"只改文字"的路径。
        LocalizationManager.OnLocaleChanged += Refresh;
    }

    void OnDestroy()
    {
        LocalizationManager.OnLocaleChanged -= Refresh;
    }

    /// <summary>公开方法，三处会调:Initialize() 建面板后画一次；`PauseController` 每次打开
    /// 暂停面板时调一次(跨局计数器在骑行过程中随时会变，见 PauseController.HandlePauseStateChanged)；
    /// `LocalizationManager.OnLocaleChanged` 触发时调一次(语言可能是在同一次打开菜单期间
    /// 被切换的)。每次都整块重建行内容，这个面板行数少，重建开销不是问题。</summary>
    public void Refresh()
    {
        if (goalManager == null) return;
        GoalsSnapshot snapshot = goalManager.GetDisplaySnapshot();

        foreach (Transform panel in goalsPanels)
        {
            if (panel == null) continue;
            Transform rows = panel.Find("Rows");
            if (rows == null) continue;
            Populate(rows, snapshot);
        }
    }

    void BuildInto(Transform panel)
    {
        // 占位文字节点本身(GoalsPanel 上直接挂的那个 Text，内容原来是"Goals")清空——
        // Tab 按钮本身已经写着"Goals"，面板里没必要再重复一次标题，只留下面真正测试用的
        // 目标清单内容。真正的清单内容挂一个新的子物体 "Rows"，运行时动态填充。
        Text placeholderText = panel.GetComponent<Text>();
        if (placeholderText != null) placeholderText.text = string.Empty;

        GameObject rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(panel, false);
        RectTransform rowsRect = (RectTransform)rows.transform;
        rowsRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowsRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowsRect.pivot = new Vector2(0.5f, 0.5f);
        rowsRect.anchoredPosition = new Vector2(0f, -30f);
        rowsRect.sizeDelta = new Vector2(700f, 0f);

        VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rows.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void Populate(Transform rows, GoalsSnapshot snapshot)
    {
        for (int i = rows.childCount - 1; i >= 0; i--)
        {
            Destroy(rows.GetChild(i).gameObject);
        }

        foreach (GoalRow row in snapshot.goals)
        {
            GoalsUIUtil.BuildGoalRow(rows, 40f, row.title, row.completed);
        }

        GoalsUIUtil.BuildStarRow(rows, 46f, snapshot.completedCount);
        GoalsUIUtil.BuildLevelLabel(rows, 40f, snapshot.levelNumber);
    }
}
