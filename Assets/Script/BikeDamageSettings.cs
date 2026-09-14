using UnityEngine;

/// <summary>
/// "多条命"损毁系统的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Bike Damage Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "BikeDamageSettings", menuName = "RidingBike/Bike Damage Settings")]
public class BikeDamageSettings : ScriptableObject
{
    [Tooltip("总共能扛几次失控判定。前 (maxLives - 1) 次只掉零件继续骑，第 maxLives 次才是真的摔车结算。")]
    public int maxLives = 3;

    [Tooltip("掉零件之后给一小段无敌时间(秒)，避免同一次摔倒的姿态在下一帧又立刻消耗掉下一条命。")]
    public float invulnerabilitySeconds = 1.5f;

    [Tooltip("掉零件时把车身角度朝目标角度(触地时是坡度，空中是水平)拉回的比例，0 = 完全不干预，1 = 直接摆正。")]
    [Range(0f, 1f)]
    public float recoveryUprightBlend = 0.6f;

    [Tooltip("前轮飞出去的冲量随机范围(下限)。")]
    public float ejectImpulseMin = 3f;
    [Tooltip("前轮飞出去的冲量随机范围(上限)。")]
    public float ejectImpulseMax = 6f;
    [Tooltip("前轮飞出去时附加的随机扭矩范围(deg/s 量级的冲量)，让它飞出去的时候会打转，更有\"被甩脱\"的感觉。")]
    public float ejectTorqueMax = 720f;
    [Tooltip("前轮飞出去之后几秒被清理掉(Destroy)，避免长期滞留在场景里。")]
    public float wheelCleanupDelay = 5f;
}
