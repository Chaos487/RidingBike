using UnityEngine;

/// <summary>
/// GroundForegroundLayer(屏幕底部前景剪影层)的可调参数,做成资产而不是硬编码在脚本默认值里,
/// 这样可以在编辑器里创建一份、直接拖数值调,Play 模式下改了也不会随着退出 Play 被重置
/// (跟场景状态不一样,资产改动是真的落盘的)。
/// 用法:Project 窗口右键 Create > RidingBike > Ground Foreground Layer Settings 创建一份资产,
/// 放在 Assets/Resources/ 下(文件名必须叫 GroundForegroundLayerSettings,设备打包版本靠这个
/// 名字通过 Resources.Load 找到它)启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "GroundForegroundLayerSettings", menuName = "RidingBike/Ground Foreground Layer Settings")]
public class GroundForegroundLayerSettings : ScriptableObject
{
    [Header("覆盖范围")]
    [Tooltip("以车身为中心，左右各铺多宽(米)。必须明显小于地形的 generateAheadDistance(默认 50m)" +
             "和 despawnBehindDistance(默认 25m)，否则采样点会落在地形还没生成/已经回收的区间。")]
    public float halfWidth = 20f;
    [Tooltip("采样间距(米)，越小曲线越平滑，但顶点数越多。")]
    public float sampleSpacing = 1f;

    [Header("形状(独立于真地形起伏的低频噪声,叠加在真实高度之上)")]
    public float noiseScale = 0.03f;
    [Tooltip("起伏幅度(米)，波峰到基准线的最大高度。必须小于 Sink Depth，否则波峰可能反而" +
             "高过真地形在同一位置的实际高度。")]
    public float hillHeight = 2f;
    [Tooltip("网格往下延伸多深(米)，保证镜头怎么缩放都看不到底边穿帮。")]
    public float groundThickness = 60f;

    [Header("位置/外观")]
    [Tooltip("基准线比对应位置的真实地形低多少米——需要大于 Hill Height，保证这层任何时候都不会" +
             "高过真地形。")]
    public float sinkDepth = 3f;
    public Color silhouetteColor = new Color(0.22f, 0.14f, 0.12f);
    [Tooltip("渲染排序，必须比车/障碍物(默认 0)和真地形(-1)都高，才能真的盖在最前面。")]
    public int sortingOrder = 10;
}
