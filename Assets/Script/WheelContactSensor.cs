using UnityEngine;

/// <summary>
/// 挂在物理轮子(Frontwheel/Backwheel)自己身上，用真正的碰撞事件记录触地状态，
/// 不额外发射线重复算一遍——物理引擎本来就要为悬挂/摩擦力算这些接触。
/// 供 LandingDetector 判定落地质量(比如前后轮接触顺序)、CrashDetector 判定摔车使用。
///
/// 不用 Enter/Exit 配对计数——地形是一条随玩家前进不断在 Update 里重建 points 的
/// EdgeCollider2D(EndlessTerrainGenerator.RebuildCollider),形状被频繁重建时
/// Enter/Exit 不一定严格配对触发,计数器一旦多加一次就会永远卡在"触地"，
/// 导致跳跃全程都读不到"离地"，空中旋转怎么按都触发不了。
/// 改成每个物理步只看这一步有没有 Stay/Enter，不依赖上一步的计数状态,不可能卡死。
/// </summary>
public class WheelContactSensor : MonoBehaviour
{
    public LayerMask groundLayer = ~0;

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
        if (IsGroundLayer(collision.gameObject.layer)) groundedThisStep = true;
    }

    bool IsGroundLayer(int layer) => (groundLayer.value & (1 << layer)) != 0;
}
