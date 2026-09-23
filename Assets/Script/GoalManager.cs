using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>结算面板/Menu Goals Tab 用的一份只读快照——GoalManager 不直接把内部状态暴露给
/// UI，UI 只管把这几个字段画出来。</summary>
public struct GoalRow
{
    public string title;
    public bool completed;
}

public struct GoalsSnapshot
{
    public int levelNumber;
    public GoalRow[] goals;
    public int completedCount;
    /// <summary>只有 SettleCurrentLevel() 返回的快照这个值才有意义——GetDisplaySnapshot()
    /// 永远是 0(它不发奖励)。</summary>
    public int gearJustEarned;
}

/// <summary>
/// 参考 Alto's Odyssey 的持久 Level 目标系统——固定 3 个目标一组(Level)，跨很多局游戏持续
/// 追踪，不是每局重新抽一遍。跟这个项目其它系统一样，只监听事件，不控制 gameplay
/// (`ScoreSystem`是同样的做法，订阅 LandingDetector/TrickSystem/ObstacleSpawner 的事件，
/// 不改它们的任何逻辑)。
///
/// 两类判定口径(见 GoalRequirementType)：
/// - "单局最佳成绩"(距离/分数)：直接读 RunManager 已经在维护的存档记录
///   (BestDistanceKey/HighScoreKey)，GoalManager 自己不重复存一份。
/// - "跨局累计次数"(Trick/Perfect 落地/贴身险/Node)：GoalManager 自己开 PlayerPrefs 计数器，
///   订阅对应系统的事件持续累加，不随单局重开清零。
///
/// 有两个查询入口，一读一写：
/// - GetDisplaySnapshot()：纯只读，给 Menu/暂停面板里的 Goals Tab 用，不发奖励、不推进 Level。
/// - SettleCurrentLevel()：有副作用，只在摔车结算前的 GoalsRecapUI 弹出时调用一次——本局
///   新完成的目标在这一刻才真正发 Gear、写盘；3 个全部完成的话顺带把 Level 推进到下一组，
///   但这次返回的快照仍然是"刚结算完的这个 Level"(所有目标都打钩的状态)，下一次调用才会
///   看到新 Level 的空目标——不然玩家会在还没看到自己刚打满的画面时就已经被换掉。
/// </summary>
public class GoalManager : MonoBehaviour
{
    const string TrickCountKey = "RidingBike_Goal_TrickCount";
    const string PerfectLandingCountKey = "RidingBike_Goal_PerfectLandingCount";
    const string NearMissCountKey = "RidingBike_Goal_NearMissCount";
    const string NodeCountKey = "RidingBike_Goal_NodeCount";
    const string CurrentLevelKey = "RidingBike_Goal_CurrentLevel";
    // 已经发过奖励的目标 id，逗号拼接存成一个字符串——目标一旦完成过一次奖励就不会再发第二次，
    // 哪怕跨局累计的计数器之后继续往上涨(比如 10 次 Trick 达成拿过奖励后，第 11、12 次不会重复给)。
    const string RewardedIdsKey = "RidingBike_Goal_RewardedIds";

    GoalSettings settings;
    GearManager gearManager;

    int trickLifetimeCount;
    int perfectLandingLifetimeCount;
    int nearMissLifetimeCount;
    int nodeLifetimeCount;
    int currentLevelIndex;
    HashSet<string> rewardedGoalIds = new HashSet<string>();

    public void ApplySettings(GoalSettings goalSettings)
    {
        if (goalSettings != null) settings = goalSettings;
    }

    public void Initialize(GearManager gearManagerRef)
    {
        gearManager = gearManagerRef;

        trickLifetimeCount = PlayerPrefs.GetInt(TrickCountKey, 0);
        perfectLandingLifetimeCount = PlayerPrefs.GetInt(PerfectLandingCountKey, 0);
        nearMissLifetimeCount = PlayerPrefs.GetInt(NearMissCountKey, 0);
        nodeLifetimeCount = PlayerPrefs.GetInt(NodeCountKey, 0);
        currentLevelIndex = PlayerPrefs.GetInt(CurrentLevelKey, 0);

        string rewarded = PlayerPrefs.GetString(RewardedIdsKey, string.Empty);
        rewardedGoalIds = new HashSet<string>(rewarded.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>跟 ScoreSystem.Initialize 是同一个套路——订阅这几个系统已有的事件，不改它们
    /// 任何逻辑。nodeManager 可能为空(比如场景里没接 Node 系统)，为空就跳过对应订阅。</summary>
    public void SubscribeGameplayEvents(LandingDetector landing, TrickSystem trick, ObstacleSpawner obstacles, NodeManager nodeManager)
    {
        landing.OnLanded += HandleLanded;
        trick.OnTrickCompleted += HandleTrickCompleted;
        obstacles.OnNearMiss += HandleNearMiss;
        if (nodeManager != null) nodeManager.OnNodeReached += HandleNodeReached;
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        if (quality != LandingDetector.Quality.Perfect) return;
        perfectLandingLifetimeCount++;
        PlayerPrefs.SetInt(PerfectLandingCountKey, perfectLandingLifetimeCount);
        PlayerPrefs.Save();
    }

    void HandleTrickCompleted(int laps)
    {
        trickLifetimeCount++;
        PlayerPrefs.SetInt(TrickCountKey, trickLifetimeCount);
        PlayerPrefs.Save();
    }

    void HandleNearMiss()
    {
        nearMissLifetimeCount++;
        PlayerPrefs.SetInt(NearMissCountKey, nearMissLifetimeCount);
        PlayerPrefs.Save();
    }

    void HandleNodeReached()
    {
        nodeLifetimeCount++;
        PlayerPrefs.SetInt(NodeCountKey, nodeLifetimeCount);
        PlayerPrefs.Save();
    }

    float GetProgress(GoalDefinition goal) => goal.requirementType switch
    {
        GoalRequirementType.BestDistanceInRun => PlayerPrefs.GetFloat(RunManager.BestDistanceKey, 0f),
        GoalRequirementType.BestScoreInRun => PlayerPrefs.GetInt(RunManager.HighScoreKey, 0),
        GoalRequirementType.TrickCountLifetime => trickLifetimeCount,
        GoalRequirementType.PerfectLandingCountLifetime => perfectLandingLifetimeCount,
        GoalRequirementType.NearMissCountLifetime => nearMissLifetimeCount,
        GoalRequirementType.NodeCountLifetime => nodeLifetimeCount,
        _ => 0f,
    };

    bool IsComplete(GoalDefinition goal) => GetProgress(goal) >= goal.targetValue;

    GoalDefinition FindGoal(string id) => settings?.goals.Find(g => g.id == id);

    GoalLevel CurrentLevel => settings != null && settings.levels.Count > 0
        ? settings.levels[Mathf.Clamp(currentLevelIndex, 0, settings.levels.Count - 1)]
        : null;

    public GoalsSnapshot GetDisplaySnapshot() => BuildSnapshot(settle: false);

    public GoalsSnapshot SettleCurrentLevel() => BuildSnapshot(settle: true);

    GoalsSnapshot BuildSnapshot(bool settle)
    {
        int displayedLevelIndex = currentLevelIndex;
        GoalLevel level = CurrentLevel;
        if (level == null || settings == null)
        {
            return new GoalsSnapshot { levelNumber = displayedLevelIndex + 1, goals = new GoalRow[0] };
        }

        List<GoalRow> rows = new List<GoalRow>();
        int completedCount = 0;
        int gearEarned = 0;

        foreach (string id in level.goalIds)
        {
            GoalDefinition goal = FindGoal(id);
            if (goal == null) continue;

            bool completed = IsComplete(goal);
            if (completed) completedCount++;

            if (settle && completed && !rewardedGoalIds.Contains(goal.id))
            {
                rewardedGoalIds.Add(goal.id);
                gearEarned += goal.gearReward;
                gearManager?.AddGear(goal.gearReward);
            }

            rows.Add(new GoalRow { title = goal.title, completed = completed });
        }

        if (settle)
        {
            PlayerPrefs.SetString(RewardedIdsKey, string.Join(",", rewardedGoalIds));

            if (completedCount >= level.goalIds.Count && currentLevelIndex < settings.levels.Count - 1)
            {
                currentLevelIndex++;
                PlayerPrefs.SetInt(CurrentLevelKey, currentLevelIndex);
            }

            PlayerPrefs.Save();
        }

        return new GoalsSnapshot
        {
            levelNumber = displayedLevelIndex + 1,
            goals = rows.ToArray(),
            completedCount = completedCount,
            gearJustEarned = gearEarned,
        };
    }
}
