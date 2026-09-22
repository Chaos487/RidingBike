using UnityEngine;

/// <summary>
/// 挂在物理轮子(Frontwheel/Backwheel)自己身上，用真正的碰撞事件记录触地状态，
/// 不额外发射线重复算一遍——物理引擎本来就要为悬挂/摩擦力算这些接触。
/// 供 LandingDetector 判定落地质量(比如前后轮接触顺序)、CrashDetector 判定摔车、
/// GapFallHandler 判定是否真的"贴地"使用。
///
/// 不用 Enter/Exit 配对计数——地形是一条随玩家前进不断在 Update 里重建 points 的
/// EdgeCollider2D(EndlessTerrainGenerator.RebuildCollider),形状被频繁重建时
/// Enter/Exit 不一定严格配对触发,计数器一旦多加一次就会永远卡在"触地"，
/// 导致跳跃全程都读不到"离地"，空中旋转怎么按都触发不了。
/// 改成每个物理步只看这一步有没有 Stay/Enter，不依赖上一步的计数状态,不可能卡死。
///
/// 只看碰撞层不够——断层(EndlessTerrainGenerator 3.11 节)两侧是接近垂直的陡坡，
/// 轮子撞上峭壁侧面在物理上也是一次跟"地面"层的碰撞，会被误判成"贴地"，导致车能
/// 顺着峭壁一路"爬"上去(ApplyBalance 以为在正常骑行、按碰到的"坡度"回正姿态，
/// 悬挂+摩擦力再加一把，就变成贴着峭壁往上蹭)。改成额外检查接触点法线跟正上方的
/// 夹角，只有明显朝上的接触(真正踩在地面/坡面上)才算"贴地"，法线偏向水平的
/// (撞墙/撞峭壁侧面)一律不算，跟正常骑行会用到的最陡坡度相比留了足够余量。
/// </summary>
public class WheelContactSensor : MonoBehaviour
{
    public LayerMask groundLayer = ~0;
    [Tooltip("接触点法线跟正上方的夹角超过这个角度(度)就不算\"贴地\"，只当作撞到了墙/悬崖峭壁——" +
             "地形最陡的正常坡大约 50°多，断层两侧的峭壁接近 90°，中间留了余量。")]
    public float maxGroundAngle = 70f;
    [Tooltip("是否要求接触点法线朝上(在 maxGroundAngle 以内)才算\"触地\"。轮子需要这个来区分" +
             "真正踩在地面上、还是贴着断层峭壁/墙面蹭；车架(BikeController.BodyContact)不需要——" +
             "车架撞到障碍物大多是正面/侧面撞、法线接近水平，这种情况也应该算数(判摔车)，" +
             "所以给车架用的那个实例要把这个开关关掉。")]
    public bool requireUpwardContact = true;

    bool groundedThisStep;
    bool groundedLastStep;

    /// <summary>当前是否触地(可能同时接触多个碰撞体)。滞后一个物理步,可忽略不计。</summary>
    public bool IsGrounded => groundedLastStep;

    /// <summary>最近一次从"离地"变成"触地"的时间(Time.time)。还没触地过时是负无穷。</summary>
    public float LastGroundedTime { get; private set; } = float.NegativeInfinity;

    void FixedUpdate()
    {
        if (groundedThisStep && !groundedLastStep) LastGroundedTime = Time.time;
        groundedLastStep = groundedThisStep;
        groundedThisStep = false; // 每步重新判定,下面的碰撞回调会在本步物理模拟后立刻把它设回 true
    }

    void OnCollisionEnter2D(Collision2D collision) => MarkGroundedIfGroundLayer(collision);
    void OnCollisionStay2D(Collision2D collision) => MarkGroundedIfGroundLayer(collision);

    void MarkGroundedIfGroundLayer(Collision2D collision)
    {
        if (!IsGroundLayer(collision.gameObject.layer)) return;
        if (requireUpwardContact && !HasUpwardContact(collision)) return;
        groundedThisStep = true;
    }

    bool HasUpwardContact(Collision2D collision)
    {
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            if (Vector2.Angle(Vector2.up, collision.GetContact(i).normal) <= maxGroundAngle) return true;
        }
        return false;
    }

    bool IsGroundLayer(int layer) => (groundLayer.value & (1 << layer)) != 0;
}
