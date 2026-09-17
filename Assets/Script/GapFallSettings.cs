using UnityEngine;

/// <summary>
/// GapFallHandler 的可调参数,做成资产而不是硬编码,方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Gap Fall Settings 创建一份,不创建就用脚本默认值。
/// </summary>
[CreateAssetMenu(fileName = "GapFallSettings", menuName = "RidingBike/Gap Fall Settings")]
public class GapFallSettings : ScriptableObject
{
    [Tooltip("车身比断层记录的地面高度低多少米,判定为\"掉进虚空\"。")]
    public float fallThreshold = 8f;
    [Tooltip("判定掉进虚空扣多少血量——走跟正常摔车一样的 HP 血条,不是单独一条命。")]
    public float damage = 25f;
    [Tooltip("按空格重新出现时,传送到断层终点前方多远(米)。")]
    public float respawnAheadDistance = 1.5f;
    [Tooltip("重新出现时车身中心比地面高多少米,避免正好卡进地形碰撞体里。")]
    public float respawnHeightOffset = 1.5f;
}
