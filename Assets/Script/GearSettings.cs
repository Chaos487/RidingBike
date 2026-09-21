using UnityEngine;

/// <summary>
/// 齿轮生成/视觉的可调参数,做成资产方便在编辑器里手调(参考 EndlessRunSettings 的模式)。
/// 用法:Project 窗口右键 Create > RidingBike > Gear Settings 创建一份资产。
/// </summary>
[CreateAssetMenu(fileName = "GearSettings", menuName = "RidingBike/Gear Settings")]
public class GearSettings : ScriptableObject
{
    [Header("组与组之间的间隔 (米)")]
    [Tooltip("这一组的起点到下一组起点之间的距离。")]
    public float minSpawnInterval = 15f;
    public float maxSpawnInterval = 30f;
    [Range(0f, 1f)]
    [Tooltip("每个排到的生成点实际生成一组齿轮的概率,小于 1 代表偶尔会跳过一组,间隔更参差不齐。")]
    public float spawnChance = 1f;

    [Header("组内(一次连续生成几个齿轮)")]
    [Tooltip("每组最少生成几个齿轮。")]
    public int minGroupSize = 3;
    [Tooltip("每组最多生成几个齿轮。")]
    public int maxGroupSize = 5;
    [Tooltip("同一组里相邻两个齿轮之间的间距(米)。")]
    public float intraGroupSpacing = 1.5f;

    [Header("位置")]
    [Tooltip("齿轮悬浮在地面上方多高(米)。")]
    public float heightAboveGround = 1.2f;
    [Tooltip("跟障碍物的水平距离小于这个值就跳过,避免齿轮生成在障碍物身上或紧贴着障碍物。")]
    public float obstacleAvoidMargin = 1f;

    [Header("视觉(单张图的假 3D 旋转,cos 缩放挤压)")]
    [Tooltip("旋转速度,数值越大转得越快。")]
    public float spinSpeed = 3f;
    [Tooltip("转到\"背面\"(缩放为负)时要不要把颜色调暗一点,模拟光照角度变化,让旋转看起来更立体。")]
    public bool darkenBackFace = true;
    [Range(0f, 1f)]
    public float backFaceBrightness = 0.6f;
    [Tooltip("上下浮动的幅度(米),0 = 不浮动。")]
    public float bobAmplitude = 0.15f;
    public float bobSpeed = 2f;
}
