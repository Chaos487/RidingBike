using System.Collections.Generic;
using UnityEngine;

/// <summary>Node 处在风险曲线的哪一档——不是独立的类型分类，直接等于 Choice 池抽取认的那一个维度。</summary>
public enum NodeTier { Early, Mid, Late }

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
    public NodeTier tier;
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
    [Tooltip("间隔必须明显大于地形的 generateAheadDistance(默认 50m)+安全区前段长度,否则安全区" +
             "注册的时候,那段地形可能已经被提前生成过了,来不及避开断层/障碍物。")]
    public float minNodeInterval = 80f;
    public float maxNodeInterval = 100f;

    [Header("安全区 (米)")]
    public float safeZoneBefore = 15f;
    public float safeZoneAfter = 15f;

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
                tier = NodeTier.Early,
                effects = { new EffectEntry { type = EffectType.JumpForceFlat, value = 3f } },
            },
            new ChoicePreset
            {
                title = "强化引擎",
                description = "车速上限 +8%",
                tier = NodeTier.Early,
                effects = { new EffectEntry { type = EffectType.MaxSpeedPercent, value = 8f } },
            },
            new ChoicePreset
            {
                title = "快速补给",
                description = "氮气回能距离 -20%",
                tier = NodeTier.Early,
                effects = { new EffectEntry { type = EffectType.BoostRechargePercent, value = 20f } },
            },
            new ChoicePreset
            {
                title = "极限调校",
                description = "车速上限 +15%\n最大 HP -15",
                tier = NodeTier.Mid,
                effects =
                {
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = 15f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -15f },
                },
            },
            new ChoicePreset
            {
                title = "越野改装",
                description = "跳跃力度 +6\n车速上限 -5%",
                tier = NodeTier.Mid,
                effects =
                {
                    new EffectEntry { type = EffectType.JumpForceFlat, value = 6f },
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = -5f },
                },
            },
            new ChoicePreset
            {
                title = "轻量车架",
                description = "加速度 +20%\n最大 HP -10",
                tier = NodeTier.Mid,
                effects =
                {
                    new EffectEntry { type = EffectType.AccelerationPercent, value = 20f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -10f },
                },
            },
            new ChoicePreset
            {
                title = "破风涡轮",
                description = "车速上限 +30%\n最大 HP -30",
                tier = NodeTier.Late,
                effects =
                {
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = 30f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -30f },
                },
            },
            new ChoicePreset
            {
                title = "疯狂氮气",
                description = "氮气回能距离 -50%\n车速上限 -10%",
                tier = NodeTier.Late,
                effects =
                {
                    new EffectEntry { type = EffectType.BoostRechargePercent, value = 50f },
                    new EffectEntry { type = EffectType.MaxSpeedPercent, value = -10f },
                },
            },
            new ChoicePreset
            {
                title = "孤注一掷",
                description = "跳跃力度 +15\n最大 HP -40",
                tier = NodeTier.Late,
                effects =
                {
                    new EffectEntry { type = EffectType.JumpForceFlat, value = 15f },
                    new EffectEntry { type = EffectType.MaxHpFlat, value = -40f },
                },
            },
        };
    }
}
