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

    [Tooltip("扣血之后这段时间内不会再次扣血(秒)，车身贴图同步闪烁提示玩家。")]
    public float invulnerabilitySeconds = 5f;

    [Tooltip("扣血时把车身角度朝目标角度(触地时是坡度，空中是水平)拉回的比例，0 = 完全不干预，1 = 直接摆正。")]
    [Range(0f, 1f)]
    public float recoveryUprightBlend = 0.6f;

    [Header("断层扣血 (GapFallHandler)")]
    [Tooltip("车身比断层记录的地面高度低多少米,判定为\"掉进虚空\"。")]
    public float gapFallThreshold = 2.5f;
    [Tooltip("判定掉进虚空扣多少血量——走这条同一个 HP 血条，不是单独一条命。")]
    public float gapFallDamage = 25f;
    [Tooltip("按空格重新出现时,传送到断层终点前方多远(米)。")]
    public float gapRespawnAheadDistance = 1.5f;
    [Tooltip("重新出现时车身中心比地面高多少米,避免正好卡进地形碰撞体里。")]
    public float gapRespawnHeightOffset = 1.5f;
}
