using System;
using UnityEngine;

/// <summary>
/// 处理"掉进断层"(见 3.11 节)这个专门的失败情形:车身比断层记录的地面高度低过
/// fallThreshold 时判定为掉进虚空——镜头脱离跟随定在原地不动、扣一次血(跟正常摔车
/// 共用同一条 HP 血条)、冻结 BikeController(输入/驱动/自动回正全部停摆，车身交给
/// 纯物理自由落体继续往下掉，反正镜头已经看不到，不需要真的等它掉到底)。
///
/// 判定不用 Collider2D/触发区——这个项目已经在 WheelContactSensor 上踩过一次坑:
/// 地形是持续重建的 EdgeCollider2D，跨越断层范围的触发区在地形频繁重建时 Enter/Exit
/// 不保证严格配对触发。改成纯数据查表:EndlessTerrainGenerator 生成断层的那一刻就精确
/// 知道每段断层的 [起点X, 终点X] 和地面高度(TryGetGapAt)，这里每帧拿车身当前 X 去查。
///
/// 玩家按 Space 之后，直接把车身传送到断层终点前方、贴着地面，跟正常跳过了这个断层
/// 一样继续往前骑；镜头重新接上 Follow，不额外判断车身是否已经回到画面内——Cinemachine
/// 自己的 Damping 会把镜头平滑地"追"回去。
/// </summary>
public class GapFallHandler : MonoBehaviour
{
    float fallThreshold = 8f;
    float damage = 25f;
    float respawnAheadDistance = 1.5f;
    float respawnHeightOffset = 1.5f;

    BikeController bike;
    EndlessTerrainGenerator terrain;
    BikeDamageSystem damageSystem;
    CameraDirector cameraDirector;
    Transform rackTransform;

    bool isLost;
    float pendingRespawnX;
    float pendingRespawnY;

    /// <summary>判定掉进虚空、进入"按 Space 继续"等待状态时触发,UI(RunManager)订阅来显示提示。</summary>
    public event Action OnEnteredVoid;
    /// <summary>玩家按 Space、车身已经传送回地面时触发,UI 订阅来隐藏提示。</summary>
    public event Action OnExitedVoid;

    public void Initialize(BikeController bikeController, EndlessTerrainGenerator terrainGenerator,
        BikeDamageSystem bikeDamageSystem, CameraDirector camera)
    {
        bike = bikeController;
        terrain = terrainGenerator;
        damageSystem = bikeDamageSystem;
        cameraDirector = camera;
        rackTransform = bike.transform.Find("rack");
    }

    public void ApplySettings(GapFallSettings settings)
    {
        if (settings == null) return;

        fallThreshold = settings.fallThreshold;
        damage = settings.damage;
        respawnAheadDistance = settings.respawnAheadDistance;
        respawnHeightOffset = settings.respawnHeightOffset;
    }

    void Update()
    {
        if (isLost)
        {
            if (Input.GetKeyDown(KeyCode.Space)) Respawn();
            return;
        }

        if (bike.IsWheelGrounded) return; // 贴地就不可能掉进断层，省一次查表

        Vector2 pos = bike.bikeRigidbody.position;
        if (terrain.TryGetGapAt(pos.x, out float groundY, out float gapEndX) && groundY - pos.y > fallThreshold)
        {
            EnterVoid(gapEndX, groundY);
        }
    }

    void EnterVoid(float gapEndX, float groundY)
    {
        bike.enabled = false;

        bool stillAlive = damageSystem.ApplyDamage(damage);
        if (!stillAlive)
        {
            // 这一下正好把血扣没了，交给 BikeDamageSystem.OnFinalCrash 走正常的摔车结算——
            // 镜头/输入不接管"掉进虚空"这套流程，镜头保持跟随，让结算时的聚焦对着车身本身。
            return;
        }

        isLost = true;
        pendingRespawnX = gapEndX + respawnAheadDistance;
        pendingRespawnY = groundY + respawnHeightOffset;

        cameraDirector.DetachFollow();
        OnEnteredVoid?.Invoke();
    }

    void Respawn()
    {
        isLost = false;

        Rigidbody2D rb = bike.bikeRigidbody;
        rb.position = new Vector2(pendingRespawnX, pendingRespawnY);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.rotation = 0f;

        bike.enabled = true;
        cameraDirector.ReattachFollow(rackTransform);

        OnExitedVoid?.Invoke();
    }
}
