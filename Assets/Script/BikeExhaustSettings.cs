using UnityEngine;

/// <summary>
/// 尾气/扬尘粒子效果的可调参数,做成资产方便在编辑器里手调(参考 EndlessRunSettings 的模式)。
/// 粒子本身的形状/颜色/生命周期这些视觉参数不在这里——那些由美术直接在 ExhaustTrail 预制体
/// 的 ParticleSystem 组件上调;这份资产只管"什么时候喷、喷多猛、挂在车身哪个位置"。
/// 不创建资产的话,BikeExhaust 就用这里的默认值。
/// 用法:Project 窗口右键 Create > RidingBike > Bike Exhaust Settings 创建一份资产,
/// 建议放在 Assets/Resources/ 下(原因见 BikeExhaust.cs 顶部注释)。
/// </summary>
[CreateAssetMenu(fileName = "BikeExhaustSettings", menuName = "RidingBike/Bike Exhaust Settings")]
public class BikeExhaustSettings : ScriptableObject
{
    [Header("触发阈值")]
    [Tooltip("车速低于这个值(km/h)就不喷了,避免静止/低速时还在冒烟显得奇怪。")]
    public float minSpeedKmhForEmission = 3f;

    [Header("喷发强度 (乘在粒子系统自己 Emission 模块的 Rate over Time 上)")]
    [Tooltip("刚超过阈值、低速时的强度倍率。")]
    public float minEmissionMultiplier = 0.3f;
    [Tooltip("满速时的强度倍率。")]
    public float maxEmissionMultiplier = 1.5f;
    [Tooltip("按 Shift 加速(Boost)时的强度倍率——直接覆盖按车速插值的结果，按下那一下就该有反应，不用等车速真的追上去。")]
    public float boostEmissionMultiplier = 2.2f;

    [Header("挂点")]
    [Tooltip("粒子效果相对车身根节点(Bike)的局部偏移——大概在后轮/排气管的位置。挂在车身根节点上" +
             "是故意的：会跟着车身姿态(上下坡倾斜)一起转，但不会跟着轮子转(轮子转速比车身姿态" +
             "变化快得多，挂在轮子上喷口方向会跟着乱转)。")]
    public Vector3 localOffset = new Vector3(-0.5f, 0.05f, 0f);
}
