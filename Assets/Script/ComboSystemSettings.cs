using UnityEngine;

/// <summary>
/// 连击系统的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Combo System Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "ComboSystemSettings", menuName = "RidingBike/Combo System Settings")]
public class ComboSystemSettings : ScriptableObject
{
    [Tooltip("超过这么久没有产生连击行为(落地/贴身险)，连击数清零。")]
    public float comboResetTime = 4f;
}
