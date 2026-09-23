using UnityEngine;

/// <summary>
/// 全部计分规则的唯一可调配置——玩家每完成一件事该给多少分,都收在这一份资产里,
/// 不再散在 RunManager/TrickSystemSettings 这几个不同的地方各管一摊。
/// 用法:Project 窗口右键 Create > RidingBike > Score Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// 真正"事件发生时该查哪个字段、怎么把原始数值换算成分数"这部分逻辑在 ScoreSystem.cs,
/// 这份资产本身只是纯数据。
/// </summary>
[CreateAssetMenu(fileName = "ScoreSettings", menuName = "RidingBike/Score Settings")]
public class ScoreSettings : ScriptableObject
{
    [Header("Landing Quality (Perfect / Good / Not Bad)")]
    public int perfectLandingScore = 20;
    public int goodLandingScore = 10;
    public int notBadLandingScore = 0;

    [Header("Trick (按完整旋转圈数)")]
    [Tooltip("Element 0 = 转满 1 圈的分数，Element 1 = 2 圈，以此类推。转的圈数超过这个列表" +
             "长度时，直接沿用最后一档（最高分），不会越界。")]
    public int[] scorePerLap = { 50, 150, 300, 500, 750 };

    [Header("Near Miss")]
    public int nearMissScore = 15;

    [Header("Distance")]
    [Tooltip("本局跑了多远(米)乘以这个系数，四舍五入成整数分。")]
    public float scorePerMeter = 1f;
    [Tooltip("本局距离超过之前的最远距离纪录时，额外给这么多分(跟 Total Score 历史最高分" +
             "是两套独立的破紀錄提示，可能同时触发也可能只触发一个)。")]
    public int newDistanceRecordBonus = 500;

    [Header("Gears Collected")]
    public int scorePerGear = 5;

    [Header("Nodes Passed (Roguelike Station)")]
    public int scorePerNode = 50;

    [Header("Max HP (结算那一刻的血量上限，不是剩余血量)")]
    [Tooltip("摔车判定就是血量归零那一刻触发的，剩余血量永远是 0，没法拿来加分；" +
             "这里用的是结算那一刻的 maxHp 上限——会被 Node 选项加成/削弱，相当于奖励" +
             "这局 Build 往生命值方向堆得多深。")]
    public float scorePerMaxHpPoint = 2f;
}
