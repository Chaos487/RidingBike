using UnityEngine;

/// <summary>
/// 挂在物理轮子(Frontwheel/Backwheel)自己身上，用真正的碰撞事件记录触地状态，
/// 不额外发射线重复算一遍——物理引擎本来就要为悬挂/摩擦力算这些接触。
/// 供 LandingDetector 判定落地质量(比如前后轮接触顺序)使用。
/// </summary>
public class WheelContactSensor : MonoBehaviour
{
    public LayerMask groundLayer = ~0;

    int contactCount;

    /// <summary>当前是否触地(可能同时接触多个碰撞体)。</summary>
    public bool IsGrounded => contactCount > 0;

    /// <summary>最近一次从"离地"变成"触地"的时间(Time.time)。还没触地过时是负无穷。</summary>
    public float LastGroundedTime { get; private set; } = float.NegativeInfinity;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsGroundLayer(collision.gameObject.layer)) return;

        bool wasGrounded = IsGrounded;
        contactCount++;
        if (!wasGrounded) LastGroundedTime = Time.time;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!IsGroundLayer(collision.gameObject.layer)) return;
        contactCount = Mathf.Max(0, contactCount - 1);
    }

    bool IsGroundLayer(int layer) => (groundLayer.value & (1 << layer)) != 0;
}
