using UnityEngine;

/// <summary>
/// 跨局持久的玩家数据统计(参考 Alto's Odyssey 的 Stats 面板)——纯只读展示用的数字集合,
/// 不影响任何 gameplay/判定逻辑。跟 GoalManager/ScoreSystem 同一个套路:只订阅其它系统
/// 已有的事件来累加,不控制它们。
///
/// 这里是所有"跨局累计计数器"的唯一归属地。Trick/Perfect 落地/贴身险/Node 这 4 个计数器
/// 原来长在 GoalManager 里(因为 Goals 系统最先需要它们),现在 Stats 面板也要用同一批数字——
/// 为了不让两个系统分别存一份、迟早对不上,统一搬到这里管,GoalManager 改成读这里的公开
/// 属性(见 GoalManager.ApplyPlayerStats)。PlayerPrefs key 沿用 GoalManager 原来的字符串
/// (没有改名),这样已经在玩家设备上攒了的进度不会因为这次重构清零。
///
/// 单局最佳(最远距离/最高分)不在这里重复存一份——StatsTabUI 直接读 RunManager 已经在
/// 维护的 PlayerPrefs 记录(BestDistanceKey/HighScoreKey)。
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    // 沿用 GoalManager 原来的 key 名(见上面类注释),新增的几项另起 Stats 前缀。
    const string TrickCountKey = "RidingBike_Goal_TrickCount";
    const string PerfectLandingCountKey = "RidingBike_Goal_PerfectLandingCount";
    const string NearMissCountKey = "RidingBike_Goal_NearMissCount";
    const string NodeCountKey = "RidingBike_Goal_NodeCount";

    const string GoodLandingCountKey = "RidingBike_Stats_GoodLandingCount";
    const string NotBadLandingCountKey = "RidingBike_Stats_NotBadLandingCount";
    const string TotalDistanceKey = "RidingBike_Stats_TotalDistance";
    const string TotalRunsKey = "RidingBike_Stats_TotalRuns";
    const string BestTrickScoreKey = "RidingBike_Stats_BestTrickScore";
    const string TotalGearEarnedKey = "RidingBike_Stats_TotalGearEarned";

    public int TrickCount { get; private set; }
    public int PerfectLandingCount { get; private set; }
    public int GoodLandingCount { get; private set; }
    public int NotBadLandingCount { get; private set; }
    public int NearMissCount { get; private set; }
    public int NodeCount { get; private set; }
    public float TotalDistance { get; private set; }
    public int TotalRuns { get; private set; }
    public int BestTrickScoreEver { get; private set; }
    public int TotalGearEarned { get; private set; }

    public void Initialize()
    {
        TrickCount = PlayerPrefs.GetInt(TrickCountKey, 0);
        PerfectLandingCount = PlayerPrefs.GetInt(PerfectLandingCountKey, 0);
        GoodLandingCount = PlayerPrefs.GetInt(GoodLandingCountKey, 0);
        NotBadLandingCount = PlayerPrefs.GetInt(NotBadLandingCountKey, 0);
        NearMissCount = PlayerPrefs.GetInt(NearMissCountKey, 0);
        NodeCount = PlayerPrefs.GetInt(NodeCountKey, 0);
        TotalDistance = PlayerPrefs.GetFloat(TotalDistanceKey, 0f);
        TotalRuns = PlayerPrefs.GetInt(TotalRunsKey, 0);
        BestTrickScoreEver = PlayerPrefs.GetInt(BestTrickScoreKey, 0);
        TotalGearEarned = PlayerPrefs.GetInt(TotalGearEarnedKey, 0);
    }

    /// <summary>跟 ScoreSystem.Initialize/GoalManager 原来的 SubscribeGameplayEvents 同一个
    /// 套路——这几个系统都创建好才能订阅,调用方放在 Bootstrap 最后。runManager 用来读每局的
    /// RunSummaryData(总里程/历史最佳 Trick 分/总局数从这里累加);gearManager 用来读每次
    /// 拾取的 Gear 数量(累计获得量,跟 GearManager.CurrentGearCount 这个"当前余额"是两个数,
    /// 花掉之后余额会变但累计获得量不变)。</summary>
    public void SubscribeGameplayEvents(RunManager runManager, LandingDetector landing, TrickSystem trick,
        ObstacleSpawner obstacles, NodeManager nodeManager, GearManager gearManager)
    {
        runManager.OnRunSummaryReady += HandleRunSummaryReady;
        landing.OnLanded += HandleLanded;
        trick.OnTrickCompleted += HandleTrickCompleted;
        obstacles.OnNearMiss += HandleNearMiss;
        if (nodeManager != null) nodeManager.OnNodeReached += HandleNodeReached;
        gearManager.OnGearEarned += HandleGearEarned;
    }

    void HandleRunSummaryReady(RunSummaryData data)
    {
        TotalDistance += data.distance;
        PlayerPrefs.SetFloat(TotalDistanceKey, TotalDistance);

        TotalRuns++;
        PlayerPrefs.SetInt(TotalRunsKey, TotalRuns);

        if (data.bestTrickScore > BestTrickScoreEver)
        {
            BestTrickScoreEver = data.bestTrickScore;
            PlayerPrefs.SetInt(BestTrickScoreKey, BestTrickScoreEver);
        }

        PlayerPrefs.Save();
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        switch (quality)
        {
            case LandingDetector.Quality.Perfect:
                PerfectLandingCount++;
                PlayerPrefs.SetInt(PerfectLandingCountKey, PerfectLandingCount);
                break;
            case LandingDetector.Quality.Good:
                GoodLandingCount++;
                PlayerPrefs.SetInt(GoodLandingCountKey, GoodLandingCount);
                break;
            case LandingDetector.Quality.NotBad:
                NotBadLandingCount++;
                PlayerPrefs.SetInt(NotBadLandingCountKey, NotBadLandingCount);
                break;
        }

        PlayerPrefs.Save();
    }

    void HandleTrickCompleted(int laps)
    {
        TrickCount++;
        PlayerPrefs.SetInt(TrickCountKey, TrickCount);
        PlayerPrefs.Save();
    }

    void HandleNearMiss()
    {
        NearMissCount++;
        PlayerPrefs.SetInt(NearMissCountKey, NearMissCount);
        PlayerPrefs.Save();
    }

    void HandleNodeReached()
    {
        NodeCount++;
        PlayerPrefs.SetInt(NodeCountKey, NodeCount);
        PlayerPrefs.Save();
    }

    void HandleGearEarned(int amount)
    {
        TotalGearEarned += amount;
        PlayerPrefs.SetInt(TotalGearEarnedKey, TotalGearEarned);
        PlayerPrefs.Save();
    }
}
