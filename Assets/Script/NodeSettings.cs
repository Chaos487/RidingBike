using System.Collections.Generic;
using UnityEngine;

/// <summary>Decision Curve 的分档——按第几个 Node 计数换算出来,不是独立的类型分类。</summary>
public enum NodeTier { Early, Mid, Late }

/// <summary>Choice 的风险等级,用于三选一时保证呈现的选项覆盖低/中/高风险(见 NodeChoicePool),
/// 不直接决定数值大小——数值大小完全由 Effects 自己的 Value 决定。</summary>
public enum RiskLevel { Safe, Low, Medium, High }

/// <summary>第一版收窄成白名单:效果只能是这几种,直接改 BikeController/BikeDamageSystem 的
/// 现有数值字段,不做通用效果引擎。</summary>
public enum EffectType
{
    /// <summary>车速上限 (BikeController.maxSpeedKmh),按百分比加成/减益。</summary>
    MaxSpeedPercent,
    /// <summary>起跳基础冲量 (BikeController.jumpForce),直接加/减一个固定值。</summary>
    JumpForceFlat,
    /// <summary>满血值 (BikeDamageSystem.maxHp),直接加/减一个固定值,当前血量跟着同步变化。</summary>
    MaxHpFlat,
    /// <summary>巡航加速手感 (BikeController.cruiseMotorAcceleration/cruiseTorque),按百分比加成/减益。</summary>
    AccelerationPercent,
    /// <summary>氮气充能所需距离 (BikeController.boostRechargeDistance),按百分比缩短/拉长
    /// (正值=缩短=回能更快,负值=拉长=回能更慢)。</summary>
    BoostRechargePercent,
}

[System.Serializable]
public class EffectEntry
{
    public EffectType type;
    [Tooltip("正值=增益,负值=代价。具体含义按 EffectType 而定(百分比 or 固定值)。")]
    public float value;
}

[System.Serializable]
public class ChoicePreset
{
    public string title;
    [TextArea(2, 4)]
    public string description;
    [Tooltip("最早在第几档可用——从这档开始一直到后面所有档都可能被抽到,不是只在这一档出现。" +
             "比如 Min Stage = Early 的 Choice,在 Mid/Late 档也依然可能出现。")]
    public NodeTier minStage;
    [Tooltip("风险等级。NodeChoicePool 生成三选一时会尽量让三个选项分别落在低/中/高风险," +
             "不是纯随机抽奖——这个字段只影响\"会不会被凑进这三个里\",不影响数值大小。")]
    public RiskLevel riskLevel;
    [Tooltip("同一风险分档内的相对权重,越大越容易被抽到。")]
    public float weight = 10f;
    public List<EffectEntry> effects = new List<EffectEntry>();
}

/// <summary>
/// Roguelike Node 系统的可调参数,做成资产方便在编辑器里手调(参考 EndlessRunSettings 的模式)。
/// 不创建资产的话,NodeManager 就用这里的默认值 + BuildDefaultChoices() 内置的默认 Choice 池,
/// 开箱即用不依赖任何手动配置。
/// 用法:Project 窗口右键 Create > RidingBike > Node Settings 创建一份资产。
/// </summary>
[CreateAssetMenu(fileName = "NodeSettings", menuName = "RidingBike/Node Settings")]
public class NodeSettings : ScriptableObject
{
    [Header("触发间隔 (米)")]
    [Tooltip("间隔必须明显大于地形的 generateAheadDistance(默认 50m)+安全区前段长度(约等于" +
             "进站减速的安全区长度),否则安全区注册的时候,那段地形可能已经被提前生成过了," +
             "来不及避开断层/障碍物。")]
    public float minNodeInterval = 110f;
    public float maxNodeInterval = 140f;

    [Header("安全区 (米)")]
    [Tooltip("Station 前方的安全区长度——同时也是玩家开始看到\"即将进站\"提示、车速开始平滑" +
             "下降的距离(Approaching 状态从这里开始)。")]
    public float safeZoneBefore = 30f;
    [Tooltip("Station 后方的安全区长度——车速从站内低速平滑加速回正常水平(Exiting 状态)" +
             "需要跑完这段距离，跑完之前地形不会生成断层/致命障碍物。")]
    public float safeZoneAfter = 30f;

    [Header("Station 进站/出站节奏")]
    [Tooltip("站内低速值 (km/h)——比正常保底速度还慢很多,让\"停下来\"这件事有实感。")]
    public float stationSpeedKmh = 12f;
    [Tooltip("进站减速动画时长(秒)——车速从正常封顶平滑降到站内低速用多久。")]
    public float approachSlowdownDuration = 2.5f;
    [Tooltip("出站加速动画时长(秒)——车速从站内低速平滑升回正常封顶用多久。")]
    public float exitAccelerationDuration = 2f;
    [Tooltip("车身真正停稳之后,再等这么久(秒)才弹出三选一面板——给玩家一个\"车停下来了\"的" +
             "缓冲感,不是一到站就硬切出菜单。")]
    public float stationUiDelay = 0.15f;

    [Header("Decision Curve (按第几个 Node 计数,从 1 开始)")]
    [Tooltip("第几个 Node 开始进入中期档(正面+负面混合)。")]
    public int midTierStartIndex = 3;
    [Tooltip("第几个 Node 开始进入后期档(更强增益+更明显代价)。")]
    public int lateTierStartIndex = 6;

    [Header("Choice 池")]
    public List<ChoicePreset> choices = BuildDefaultChoices();

    static List<ChoicePreset> BuildDefaultChoices()
    {
        return new List<ChoicePreset>
        {
            new ChoicePreset
            {
                title = "轻装上阵",
                description = "跳跃力度 +3",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.JumpForceFlat, value = 3f } },
            },
            new ChoicePreset
            {
                title = "强化引擎",
                description = "车速上限 +8%",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.MaxSpeedPercent, value = 8f } },
            },
            new ChoicePreset
            {
                title = "快速补给",
                description = "氮气回能距离 -20%",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.BoostRechargePercent, value = 20f } },
            },
            new ChoicePreset
            {
                title = "越野改装",
                description = "跳跃力度 +6\n车速上限 -5%",
                minStage = NodeTier.Mid,
                riskLevel = RiskLevel.Low,
                weight = 8f,
                effects =
                {
                    new EffectEntry { type = EffectType.JumpForceFlat, value = 6f },
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = -5f },
                },
            },
            new ChoicePreset
            {
                title = "极限调校",
                description = "车速上限 +15%\n最大 HP -15",
                minStage = NodeTier.Mid,
                riskLevel = RiskLevel.Medium,
                weight = 7f,
                effects =
                {
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = 15f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -15f },
                },
            },
            new ChoicePreset
            {
                title = "轻量车架",
                description = "加速度 +20%\n最大 HP -10",
                minStage = NodeTier.Mid,
                riskLevel = RiskLevel.Medium,
                weight = 7f,
                effects =
                {
                    new EffectEntry { type = EffectType.AccelerationPercent, value = 20f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -10f },
                },
            },
            new ChoicePreset
            {
                title = "疯狂氮气",
                description = "氮气回能距离 -50%\n车速上限 -10%",
                minStage = NodeTier.Late,
                riskLevel = RiskLevel.Medium,
                weight = 6f,
                effects =
                {
                    new EffectEntry { type = EffectType.BoostRechargePercent, value = 50f },
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = -10f },
                },
            },
            new ChoicePreset
            {
                title = "破风涡轮",
                description = "车速上限 +30%\n最大 HP -30",
                minStage = NodeTier.Late,
                riskLevel = RiskLevel.High,
                weight = 5f,
                effects =
                {
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = 30f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -30f },
                },
            },
            new ChoicePreset
            {
                title = "孤注一掷",
                description = "跳跃力度 +15\n最大 HP -40",
                minStage = NodeTier.Late,
                riskLevel = RiskLevel.High,
                weight = 4f,
                effects =
                {
                    new EffectEntry { type = EffectType.JumpForceFlat, value = 15f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -40f },
                },
            },
        };
    }
}
