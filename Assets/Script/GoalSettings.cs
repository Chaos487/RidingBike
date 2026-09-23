using System.Collections.Generic;
using UnityEngine;

/// <summary>目标属于哪个大类——纯展示用的分组标签，不影响判定逻辑。</summary>
public enum GoalCategory
{
    Distance,
    Trick,
    Landing,
    NearMiss,
    Node,
    Score,
}

/// <summary>目标的判定口径，分两类(细节见 GoalManager 顶部注释)：
/// - BestXxxInRun：单局最佳成绩，直接复用已经在追踪的存档记录(RunManager 的
///   BestDistanceKey/HighScoreKey)，GoalManager 自己不用另存一份。
/// - XxxCountLifetime：跨局累计次数，GoalManager 自己开新的 PlayerPrefs 计数器，订阅对应
///   系统的事件持续累加，不随单局重开清零。</summary>
public enum GoalRequirementType
{
    BestDistanceInRun,
    BestScoreInRun,
    TrickCountLifetime,
    PerfectLandingCountLifetime,
    NearMissCountLifetime,
    NodeCountLifetime,
}

/// <summary>一条目标的静态定义——纯数据，不带任何运行时状态(完成与否由 GoalManager 拿
/// 对应的进度值现算，不在这里存)。</summary>
[System.Serializable]
public class GoalDefinition
{
    [Tooltip("唯一标识，Level 列表靠这个字符串引用具体是哪条目标。")]
    public string id;
    [Tooltip("显示在 Goals 面板上的文案，比如 \"Travel 1,000m\"。")]
    public string title;
    public GoalCategory category;
    public GoalRequirementType requirementType;
    [Tooltip("达到这个数值算完成——单位取决于 requirementType(米/分/次数)。")]
    public float targetValue;
    [Tooltip("完成后一次性发放多少 Gear。")]
    public int gearReward;
}

/// <summary>一个 Level 固定包含几个目标(参考 Alto's Odyssey 截图，3 个)——不是随机抽的，
/// 是策划直接排好的固定顺序，跟 Alto 实际的 Level 设计一致，也省掉"随机但不能同分类"这类
/// 生成器逻辑(目前目标池本来就很小，硬写顺序比写生成器更简单可靠)。</summary>
[System.Serializable]
public class GoalLevel
{
    [Tooltip("引用上面 goals 列表里的 id，固定 3 个。")]
    public List<string> goalIds = new List<string>();
}

/// <summary>
/// 全部 Goal 内容的唯一可配置资产——目标定义、Level 顺序都在这一份里，不写死在
/// GoalManager.cs 里。用法跟其它 XSettings 一样：Project 窗口右键
/// Create > RidingBike > Goal Settings 创建一份资产；不创建的话就用这里的默认值
/// (BuildDefaultGoals/BuildDefaultLevels)。
///
/// 第一版内容刻意做得很小(6 条目标、2 个 Level)——跟这个项目其它系统(地形/Node 选项池)
/// 现在都还是占位内容/最小可用版本的阶段一致，先把"生成→追踪→结算→发奖→存档"这条链路
/// 跑通，以后要扩内容直接往 goals/levels 这两个列表里加就行，不用改代码。
/// </summary>
[CreateAssetMenu(fileName = "GoalSettings", menuName = "RidingBike/Goal Settings")]
public class GoalSettings : ScriptableObject
{
    public List<GoalDefinition> goals = BuildDefaultGoals();
    public List<GoalLevel> levels = BuildDefaultLevels();

    static List<GoalDefinition> BuildDefaultGoals() => new List<GoalDefinition>
    {
        new GoalDefinition
        {
            id = "distance_1000",
            title = "Travel 1,000m in one run",
            category = GoalCategory.Distance,
            requirementType = GoalRequirementType.BestDistanceInRun,
            targetValue = 1000f,
            gearReward = 20,
        },
        new GoalDefinition
        {
            id = "score_1000",
            title = "Score 1,000 points in one run",
            category = GoalCategory.Score,
            requirementType = GoalRequirementType.BestScoreInRun,
            targetValue = 1000f,
            gearReward = 20,
        },
        new GoalDefinition
        {
            id = "trick_10",
            title = "Perform 10 Tricks",
            category = GoalCategory.Trick,
            requirementType = GoalRequirementType.TrickCountLifetime,
            targetValue = 10f,
            gearReward = 20,
        },
        new GoalDefinition
        {
            id = "landing_perfect_10",
            title = "Perform 10 Perfect Landings",
            category = GoalCategory.Landing,
            requirementType = GoalRequirementType.PerfectLandingCountLifetime,
            targetValue = 10f,
            gearReward = 20,
        },
        new GoalDefinition
        {
            id = "nearmiss_15",
            title = "Get 15 Near Misses",
            category = GoalCategory.NearMiss,
            requirementType = GoalRequirementType.NearMissCountLifetime,
            targetValue = 15f,
            gearReward = 20,
        },
        new GoalDefinition
        {
            id = "node_20",
            title = "Reach 20 Nodes",
            category = GoalCategory.Node,
            requirementType = GoalRequirementType.NodeCountLifetime,
            targetValue = 20f,
            gearReward = 20,
        },
    };

    static List<GoalLevel> BuildDefaultLevels() => new List<GoalLevel>
    {
        new GoalLevel { goalIds = { "distance_1000", "score_1000", "trick_10" } },
        new GoalLevel { goalIds = { "landing_perfect_10", "nearmiss_15", "node_20" } },
    };
}
