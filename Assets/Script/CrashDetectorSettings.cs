using UnityEngine;

/// <summary>
/// 摔车状态机的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Crash Detector Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// 数值是设计文档给的建议初始值，没有经过实机调过手感，大概率需要重新试。
/// </summary>
[CreateAssetMenu(fileName = "CrashDetectorSettings", menuName = "RidingBike/Crash Detector Settings")]
public class CrashDetectorSettings : ScriptableObject
{
    [Header("Angle (相对当前地面坡度算；空中没有坡度参考时按水平算)")]
    [Tooltip("倾角超过这个值，从 Normal 进入 Warning。")]
    public float warningAngle = 35f;
    [Tooltip("触地 + 倾角超过这个值 + 还在继续倒，从 Warning 进入 Critical。")]
    public float criticalAngle = 50f;
    [Tooltip("倾角超过这个值时，就算角速度在回正也不认——已经倒得太狠，基本没救。")]
    public float maxRecoverableAngle = 60f;

    [Header("Timing")]
    [Tooltip("Warning 状态下满足 Critical 的条件要连续保持这么久才真的升级，防止单帧抖动误判。")]
    public float minimumDangerTime = 0.05f;
    [Tooltip("Critical 状态持续这么久没能救回来，才真正判定摔车。")]
    public float crashConfirmTime = 0.20f;
    [Tooltip("倾角/角速度回到安全范围后，要连续保持这么久才降一级状态(Critical→Warning→Normal)，" +
             "不是一降回全部清零，也是防止抖动。")]
    public float recoveryTime = 0.10f;

    [Header("Recovery (角速度回正判定)")]
    [Tooltip("角速度绝对值超过这个值(deg/s)才算\"正在主动回正/继续倒\"，太小的角速度当噪声忽略，" +
             "不然车身静止不动时的微小抖动会被误判成\"正在回正\"。")]
    public float recoveringAngularSpeedThreshold = 30f;
}
