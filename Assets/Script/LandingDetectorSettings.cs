using UnityEngine;

/// <summary>
/// 落地质量判定的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Landing Detector Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "LandingDetectorSettings", menuName = "RidingBike/Landing Detector Settings")]
public class LandingDetectorSettings : ScriptableObject
{
    [Header("前后轮有效接地时间差 Δt 分档阈值(秒)")]
    [Tooltip("Δt 不超过这个值判 Perfect。")]
    public float perfectThreshold = 0.02f;

    [Tooltip("Δt 超过 Perfect 阈值、但不超过这个值判 Good；再往上判 Not Bad。" +
             "这个值同时也是 ContactOrder 判 Simultaneous 的边界。")]
    public float goodThreshold = 0.05f;

    [Header("等待第二只轮子触地的超时")]
    [Tooltip("第一只轮子触地后，超过这么久第二只轮子还没触地，直接判 Not Bad，不再等待。")]
    public float landingTimeout = 0.15f;
}
