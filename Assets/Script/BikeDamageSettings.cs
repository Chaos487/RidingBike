using UnityEngine;

/// <summary>
/// HP 损毁系统的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Bike Damage Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "BikeDamageSettings", menuName = "RidingBike/Bike Damage Settings")]
public class BikeDamageSettings : ScriptableObject
{
    [Tooltip("满血值。")]
    public float maxHp = 100f;
    [Tooltip("每次摔车扣多少血。血量归零那次判定为真的摔车结算，不归零就给一段无敌时间继续骑。")]
    public float damagePerCrash = 35f;

    [Tooltip("扣血之后给一小段无敌时间(秒)，避免同一次摔倒的姿态在下一帧又立刻扣第二次血。")]
    public float invulnerabilitySeconds = 1.5f;

    [Tooltip("扣血时把车身角度朝目标角度(触地时是坡度，空中是水平)拉回的比例，0 = 完全不干预，1 = 直接摆正。")]
    [Range(0f, 1f)]
    public float recoveryUprightBlend = 0.6f;
}
