using UnityEngine;

/// <summary>
/// 处理"掉进断层"(见 3.11 节)这个专门的失败情形:车身比断层记录的地面高度低过
/// fallThreshold 时直接判定为致命的摔车——不管当前还剩多少血，一律走
/// BikeDamageSystem.ForceFinalCrash() 结算，交给已有的摔车结算流程(锁输入、
/// 镜头接管、显示结算画面、玩家按 R 重开)，这里不需要另外管镜头/UI。
///
/// 判定不用 Collider2D/触发区——这个项目已经在 WheelContactSensor 上踩过一次坑:
/// 地形是持续重建的 EdgeCollider2D，跨越断层范围的触发区在地形频繁重建时 Enter/Exit
/// 不保证严格配对触发。改成纯数据查表:EndlessTerrainGenerator 生成断层的那一刻就精确
/// 知道每段断层的 [起点X, 终点X] 和地面高度(TryGetGapAt)，这里每帧拿车身当前 X 去查，
/// 不看 IsWheelGrounded——轮子贴着断层峭壁侧面蹭的时候也可能被判定成"贴地"，这里只认
/// 深度，掉得够深就无条件判定，不受"贴地"状态影响。
/// </summary>
public class GapFallHandler : MonoBehaviour
{
    float fallThreshold = 2.5f;

    BikeController bike;
    EndlessTerrainGenerator terrain;
    BikeDamageSystem damageSystem;

    bool triggered;

    public void Initialize(BikeController bikeController, EndlessTerrainGenerator terrainGenerator, BikeDamageSystem bikeDamageSystem)
    {
        bike = bikeController;
        terrain = terrainGenerator;
        damageSystem = bikeDamageSystem;
    }

    // 沿用 BikeDamageSettings，不单开一份资产——掉进断层本质上也是摔车判定的一种，
    // 跟正常摔车的配置放在一起，不用为了一个数字多维护一份独立的 Settings 资产。
    public void ApplySettings(BikeDamageSettings settings)
    {
        if (settings == null) return;
        fallThreshold = settings.gapFallThreshold;
    }

    void Update()
    {
        if (triggered) return;

        Vector2 pos = bike.bikeRigidbody.position;
        if (terrain.TryGetGapAt(pos.x, out float groundY, out _) && groundY - pos.y > fallThreshold)
        {
            triggered = true;
            bike.enabled = false;
            damageSystem.ForceFinalCrash();
        }
    }
}
