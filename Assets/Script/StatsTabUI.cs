using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 参考 Alto's Odyssey 的 Stats 面板——跨局累计数据的只读展示，接进菜单/暂停面板里现成的
/// StatsPanel 占位节点(预制体/TabGroupController 早就留好了这个 Tab，只是内容一直没接，
/// 见 EndlessRunCanvas.prefab 的 StatsPanel/StatsTabButton)，跟 GoalsTabUI 接 GoalsPanel
/// 是同一套路。数据来源是 PlayerStatsManager(跨局累计计数)+ 直接读 RunManager 已有的两个
/// 存档 key(单局最佳距离/分数)。
///
/// 内容比 Goals Tab 多(12 行)，固定高度装不下，用 StatsUIUtil.BuildScrollView 搭一个可
/// 滚动容器——这是这两个 Tab 面板唯一的结构性区别。
/// </summary>
public class StatsTabUI : MonoBehaviour
{
    PlayerStatsManager stats;
    Transform[] rowsContainers;

    public void Initialize(PlayerStatsManager statsManager)
    {
        stats = statsManager;

        Transform menuStatsPanel = transform.Find("MenuPanel/ContentArea/StatsPanel");
        Transform pauseStatsPanel = transform.Find("PausePanel/TabArea/ContentArea/StatsPanel");

        rowsContainers = new[]
        {
            menuStatsPanel != null ? BuildInto(menuStatsPanel) : null,
            pauseStatsPanel != null ? BuildInto(pauseStatsPanel) : null,
        };

        Refresh();

        // Refresh() 每次都整块重建行标签，重新调一遍就自然带上新语言。
        LocalizationManager.OnLocaleChanged += Refresh;
    }

    void OnDestroy()
    {
        LocalizationManager.OnLocaleChanged -= Refresh;
    }

    /// <summary>跟 GoalsTabUI.Refresh 不同——这些数字在骑行过程中随时会变(不是只在摔车结算
    /// 那一刻才变)，暂停期间打开这个 Tab 应该看到最新值。由 PauseController.HandlePauseStateChanged
    /// 在每次面板打开时调用，不能像 Goals 那样只在 Initialize() 时画一次。</summary>
    public void Refresh()
    {
        if (stats == null || rowsContainers == null) return;

        foreach (Transform rows in rowsContainers)
        {
            if (rows == null) continue;
            Populate(rows);
        }
    }

    Transform BuildInto(Transform panel)
    {
        // 占位文字节点本身(StatsPanel 上直接挂的那个 Text，内容原来是"Stats")清空——Tab
        // 按钮本身已经写着"Stats"，面板里没必要再重复一次标题。
        Text placeholderText = panel.GetComponent<Text>();
        if (placeholderText != null) placeholderText.text = string.Empty;

        return StatsUIUtil.BuildScrollView(panel, 20f, 20f);
    }

    void Populate(Transform rows)
    {
        for (int i = rows.childCount - 1; i >= 0; i--)
        {
            Destroy(rows.GetChild(i).gameObject);
        }

        int index = 0;
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.bestDistance"), $"{PlayerPrefs.GetFloat(RunManager.BestDistanceKey, 0f):N0}m");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.bestScore"), $"{PlayerPrefs.GetInt(RunManager.HighScoreKey, 0):N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.bestTrickScore"), $"{stats.BestTrickScoreEver:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.totalDistance"), $"{stats.TotalDistance:N0}m");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.totalRuns"), $"{stats.TotalRuns:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.tricksPerformed"), $"{stats.TrickCount:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.perfectLandings"), $"{stats.PerfectLandingCount:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.goodLandings"), $"{stats.GoodLandingCount:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.notBadLandings"), $"{stats.NotBadLandingCount:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.nearMisses"), $"{stats.NearMissCount:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.nodesReached"), $"{stats.NodeCount:N0}");
        StatsUIUtil.BuildStatRow(rows, index++, LocalizationManager.Get("stats.gearsCollected"), $"{stats.TotalGearEarned:N0}");
    }
}
