using UnityEngine;

/// <summary>
/// 落地质量判定的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Landing Detector Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "LandingDetectorSettings", menuName = "RidingBike/Landing Detector Settings")]
public class LandingDetectorSettings : ScriptableObject
{
    [Header("Perfect 判定(三项都不超过才算)")]
    [Tooltip("车身角度和当地坡度的偏差上限(度)。")]
    public float perfectMaxAngleError = 12f;
    [Tooltip("落地瞬间车身角速度上限(deg/s)。")]
    public float perfectMaxAngularSpeed = 90f;
    [Tooltip("落地瞬间垂直速度上限(m/s)。")]
    public float perfectMaxVerticalSpeed = 4f;

    [Header("Good 判定(超过 Perfect、不超过这里算 Good，再往上算 Bad)")]
    public float goodMaxAngleError = 30f;
    public float goodMaxAngularSpeed = 200f;
    public float goodMaxVerticalSpeed = 8f;

    [Header("前后轮接触顺序")]
    [Tooltip("两轮触地时间差小于这个值(秒)视为同时着地。")]
    public float simultaneousContactWindow = 0.05f;
}
