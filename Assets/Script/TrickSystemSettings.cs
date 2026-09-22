using UnityEngine;

/// <summary>
/// 特技计分的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Trick System Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "TrickSystemSettings", menuName = "RidingBike/Trick System Settings")]
public class TrickSystemSettings : ScriptableObject
{
    [Tooltip("按完整旋转圈数给分：Element 0 = 转满 1 圈的分数，Element 1 = 2 圈，以此类推。" +
             "转的圈数超过这个列表长度时，直接沿用最后一档（最高分），不会越界。")]
    public int[] scorePerLap = { 50, 150, 300, 500, 750 };
}
