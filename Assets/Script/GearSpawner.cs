using System;
using UnityEngine;

/// <summary>
/// 沿赛道生成齿轮拾取物——跟 StationMarkerSpawner 同一个"轮询地形生成到目标 X 才摆放"的手法,
/// 但用自己独立的一套间隔/概率,不跟 ObstacleSpawner 共用 OnGroundSampled 那个采样点,
/// 避免两者偶尔挤在同一个位置。每个生成点不是放单个齿轮,而是放一组(数量在
/// minGroupSize~maxGroupSize 之间随机,组内相邻齿轮间距 intraGroupSpacing),
/// minSpawnInterval/maxSpawnInterval/spawnChance 管的是"这一组"整体的间隔/出现概率,
/// 不是组内单个齿轮的。
///
/// 断层(Gap)、Station 安全区(减速进站/加速出站那一段)、地形还没生成到的位置都不生成——
/// 前者是因为地面在深坑底部摆一个齿轮画面很怪，安全区是想让那一段路保持视觉干净。这三种情况
/// 只跳过组里命中的那几个齿轮,不影响同一组里其他位置正常生成,也不会卡住整条生成链。
/// </summary>
public class GearSpawner : MonoBehaviour
{
    float minSpawnInterval = 15f;
    float maxSpawnInterval = 30f;
    float spawnChance = 1f;
    int minGroupSize = 3;
    int maxGroupSize = 5;
    float intraGroupSpacing = 1.5f;
    float heightAboveGround = 1.2f;
    float spinSpeed = 3f;
    bool darkenBackFace = true;
    float backFaceBrightness = 0.6f;
    float bobAmplitude = 0.15f;
    float bobSpeed = 2f;

    Transform trackTarget;
    EndlessTerrainGenerator terrain;
    GameObject gearPrefab;
    GearManager gearManager;

    /// <summary>Station 安全区反向查询(NodeManager.IsInSafeZone)。为空(没有 Node 系统接线)时
    /// 视为永远不在安全区内,不影响齿轮照常生成。</summary>
    public Func<float, bool> isInSafeZone;

    float pendingX;
    int pendingGroupSize = -1; // -1 = 还没为下一组预先掷出组内数量

    public void ApplySettings(GearSettings settings)
    {
        if (settings == null) return;

        minSpawnInterval = settings.minSpawnInterval;
        maxSpawnInterval = settings.maxSpawnInterval;
        spawnChance = settings.spawnChance;
        minGroupSize = settings.minGroupSize;
        maxGroupSize = settings.maxGroupSize;
        intraGroupSpacing = settings.intraGroupSpacing;
        heightAboveGround = settings.heightAboveGround;
        spinSpeed = settings.spinSpeed;
        darkenBackFace = settings.darkenBackFace;
        backFaceBrightness = settings.backFaceBrightness;
        bobAmplitude = settings.bobAmplitude;
        bobSpeed = settings.bobSpeed;
    }

    public void Initialize(Transform bikeTransform, EndlessTerrainGenerator terrainGenerator, GameObject prefab, GearManager manager)
    {
        trackTarget = bikeTransform;
        terrain = terrainGenerator;
        gearPrefab = prefab;
        gearManager = manager;

        ScheduleNext(trackTarget.position.x);
    }

    void Update()
    {
        if (trackTarget == null || terrain == null || gearPrefab == null) return;

        // 组内数量提前掷好、缓存住,不要每帧重新掷——不然下面算"组的最远端在哪"会跟着每帧变,
        // 永远等不到一个稳定的目标点。
        if (pendingGroupSize < 0) pendingGroupSize = UnityEngine.Random.Range(minGroupSize, maxGroupSize + 1); // Range(int,int) 右开区间,+1 让 maxGroupSize 也能选到

        // 必须等整组(包括最靠后那一个)都在地形已生成范围内才能开始摆放，只等组的起点是不够的——
        // 组跨度可能有好几米(maxGroupSize * intraGroupSpacing),起点刚好够到的那一帧，
        // 地形前沿往往还没推进到组尾那么远，之前就是因为只查起点，组尾那几个单帧查询失败后
        // 直接被跳过且不会重试，出现"配置了至少 3 个、实际只生成 1 个"的问题。
        float groupEndX = pendingX + (pendingGroupSize - 1) * intraGroupSpacing;
        if (!terrain.TryGetHeightAt(groupEndX, out _)) return;

        if (UnityEngine.Random.value <= spawnChance) SpawnGroup(pendingX, pendingGroupSize);

        pendingGroupSize = -1;
        ScheduleNext(pendingX);
    }

    void ScheduleNext(float fromX)
    {
        pendingX = fromX + UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    void SpawnGroup(float groupStartX, int count)
    {
        for (int i = 0; i < count; i++)
        {
            TrySpawnAt(groupStartX + i * intraGroupSpacing);
        }
    }

    void TrySpawnAt(float x)
    {
        if (!terrain.TryGetHeightAt(x, out float groundY)) return;
        if (terrain.TryGetGapAt(x, out _, out _)) return;
        if (isInSafeZone != null && isInSafeZone(x)) return;

        GameObject instance = UnityEngine.Object.Instantiate(gearPrefab, new Vector3(x, groundY + heightAboveGround, 0f), Quaternion.identity);
        GearPickup pickup = instance.GetComponent<GearPickup>();
        if (pickup == null) pickup = instance.AddComponent<GearPickup>();
        pickup.Initialize(gearManager, spinSpeed, darkenBackFace, backFaceBrightness, bobAmplitude, bobSpeed);
    }
}
