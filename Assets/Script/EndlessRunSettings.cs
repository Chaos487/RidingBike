using UnityEngine;

/// <summary>
/// 无限地形/障碍物的可调参数,做成资产而不是硬编码在脚本默认值里,
/// 这样可以在编辑器里创建一份、直接拖数值调,不用改代码、不用等场景手动接线。
/// 用法:Project 窗口右键 Create > RidingBike > Endless Run Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "EndlessRunSettings", menuName = "RidingBike/Endless Run Settings")]
public class EndlessRunSettings : ScriptableObject
{
    [Header("Generation Range")]
    [Tooltip("目标前方保持多远的已生成地形。")]
    public float generateAheadDistance = 50f;
    [Tooltip("目标身后超过这个距离的地形会被回收。")]
    public float despawnBehindDistance = 25f;
    [Tooltip("地形采样点间距,越小曲线越平滑,但点数越多。")]
    public float sampleSpacing = 0.4f;

    [Header("Start")]
    [Tooltip("起点前方的安全平地长度。")]
    public float startFlatLength = 20f;
    [Tooltip("地面视觉网格的厚度。")]
    public float groundThickness = 3f;

    [Header("平地段长度 (米)")]
    public float minFlatLength = 4f;
    public float maxFlatLength = 12f;

    [Header("上坡段长度 (米)")]
    public float minUphillLength = 15f;
    public float maxUphillLength = 35f;

    [Header("下坡段长度 (米)")]
    public float minDownhillLength = 15f;
    public float maxDownhillLength = 35f;

    [Header("坡的高度差 (米)")]
    [Tooltip("每个上坡爬升多高,下坡再落回同样的高度,不会有累计漂移。")]
    public float minHillHeight = 2f;
    public float maxHillHeight = 5f;

    [Header("Obstacle Hook")]
    [Tooltip("大约每隔多远对地形采样一次,供障碍物生成使用。")]
    public float obstacleCheckIntervalMin = 6f;
    public float obstacleCheckIntervalMax = 12f;
    [Tooltip("坡度角小于这个值(度)视为平地。")]
    public float flatAngleThreshold = 5f;

    [Header("地面碰撞体")]
    public float edgeRadius = 0.1f;
    public Color groundColor = new Color(0.35f, 0.6f, 0.25f);

    [Header("障碍物")]
    [Range(0f, 1f)]
    public float obstacleSpawnChance = 0.5f;
    public float obstacleMinGap = 6f;
    [Tooltip("上坡不放障碍物,留作低速缓冲/救车区间。")]
    public bool skipObstaclesOnUphill = true;
    public Vector2 obstacleSize = new Vector2(0.6f, 0.4f);
    public Color obstacleColor = new Color(0.5f, 0.35f, 0.2f);
    public float obstacleEdgeRadius = 0.08f;
}
