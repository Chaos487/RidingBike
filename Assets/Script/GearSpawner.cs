using System;
using UnityEngine;

/// <summary>
/// 沿赛道生成齿轮拾取物——跟 StationMarkerSpawner 同一个"轮询地形生成到目标 X 才摆放"的手法,
/// 但用自己独立的一套间隔/概率,不跟 ObstacleSpawner 共用 OnGroundSampled 那个采样点,
/// 避免两者偶尔挤在同一个位置。
///
/// 断层(Gap)和 Station 安全区(减速进站/加速出站那一段)都不生成——前者是因为地面在深坑
/// 底部摆一个齿轮画面很怪，后者是想让那一段路保持视觉干净。跳过的点不会卡住整条生成链,
/// 照样正常排下一个目标 X。
/// </summary>
public class GearSpawner : MonoBehaviour
{
    float minSpawnInterval = 15f;
    float maxSpawnInterval = 30f;
    float spawnChance = 1f;
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

    public void ApplySettings(GearSettings settings)
    {
        if (settings == null) return;

        minSpawnInterval = settings.minSpawnInterval;
        maxSpawnInterval = settings.maxSpawnInterval;
        spawnChance = settings.spawnChance;
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
        if (!terrain.TryGetHeightAt(pendingX, out float groundY)) return;

        TrySpawn(pendingX, groundY);
        ScheduleNext(pendingX);
    }

    void ScheduleNext(float fromX)
    {
        pendingX = fromX + UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    void TrySpawn(float x, float groundY)
    {
        if (terrain.TryGetGapAt(x, out _, out _)) return;
        if (isInSafeZone != null && isInSafeZone(x)) return;
        if (UnityEngine.Random.value > spawnChance) return;

        GameObject instance = Object.Instantiate(gearPrefab, new Vector3(x, groundY + heightAboveGround, 0f), Quaternion.identity);
        GearPickup pickup = instance.GetComponent<GearPickup>();
        if (pickup == null) pickup = instance.AddComponent<GearPickup>();
        pickup.Initialize(gearManager, spinSpeed, darkenBackFace, backFaceBrightness, bobAmplitude, bobSpeed);
    }
}
