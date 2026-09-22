using System;
using UnityEngine;

/// <summary>
/// 正式版 Landing Quality:唯一判据是"前后轮有效接地的时间差"(Δt)——不看车身角度、
/// 坡度、角速度、垂直速度。一次腾空只产生一次判定:两轮都离地时(Airborne)任一轮首次
/// 触地记录 FirstContactWheel/FirstContactTime,进入 LandingPending;另一轮触地就算 Δt
/// 分档,或者等超过 landingTimeout 另一轮还没触地就直接判 NotBad;判定完立即广播,
/// 回到 Grounded 状态,直到两轮都离地才重新武装(Airborne),避免车身仍贴着地面时
/// 被误判成"又落地了一次"。
/// </summary>
public class LandingDetector : MonoBehaviour
{
    public enum Quality { Perfect, Good, NotBad }
    public enum ContactOrder { Simultaneous, FrontFirst, BackFirst }
    public enum Wheel { Front, Back }

    enum State { Airborne, Pending, Grounded }

    /// <summary>一次落地判定的完整结果。DeltaTime/FirstContactWheel 主要用于调试和以后的 ScoreSystem。</summary>
    public readonly struct LandingResult
    {
        public readonly Quality Quality;
        public readonly ContactOrder ContactOrder;
        public readonly float DeltaTime;
        public readonly Wheel FirstContactWheel;

        public LandingResult(Quality quality, ContactOrder contactOrder, float deltaTime, Wheel firstContactWheel)
        {
            Quality = quality;
            ContactOrder = contactOrder;
            DeltaTime = deltaTime;
            FirstContactWheel = firstContactWheel;
        }
    }

    float perfectThreshold = 0.02f;
    float goodThreshold = 0.05f;
    float landingTimeout = 0.15f;

    BikeController bike;

    State state = State.Airborne;
    Wheel firstContactWheel;
    float firstContactTime;

    /// <summary>每次判定出一次"落地"时触发,带质量分级和前后轮接触顺序。</summary>
    public event Action<Quality, ContactOrder> OnLanded;

    /// <summary>最近一次判定的完整结果(含 DeltaTime/FirstContactWheel),供调试/以后的 ScoreSystem 读取。</summary>
    public LandingResult LastLanding { get; private set; }

    public void Initialize(BikeController bikeController)
    {
        bike = bikeController;
        // 跟旧版一样:按当前实际接地状态初始化,不然如果一进场景车就是停在地上的,
        // 会被误判成"这一帧刚从空中落地"，凭空触发一次判定。
        state = AnyWheelGrounded() ? State.Grounded : State.Airborne;
    }

    public void ApplySettings(LandingDetectorSettings settings)
    {
        if (settings == null) return;

        perfectThreshold = settings.perfectThreshold;
        goodThreshold = settings.goodThreshold;
        landingTimeout = settings.landingTimeout;
    }

    void Update()
    {
        if (bike == null) return;

        bool frontGrounded = IsGrounded(bike.FrontWheelContact);
        bool backGrounded = IsGrounded(bike.BackWheelContact);

        switch (state)
        {
            case State.Airborne:
                if (frontGrounded || backGrounded) BeginPending(frontGrounded, backGrounded);
                break;

            case State.Pending:
                UpdatePending(frontGrounded, backGrounded);
                break;

            case State.Grounded:
                // 只有两轮都离地才重新武装,车身还有任意一轮贴着地面时不会重新开始一次新的落地判定。
                if (!frontGrounded && !backGrounded) state = State.Airborne;
                break;
        }
    }

    void BeginPending(bool frontGrounded, bool backGrounded)
    {
        // 两轮在同一帧一起首次触地:不用等待，直接用两个传感器各自记录的触地时间算 Δt。
        if (frontGrounded && backGrounded)
        {
            Resolve(bike.FrontWheelContact.LastGroundedTime, bike.BackWheelContact.LastGroundedTime, Wheel.Front);
            return;
        }

        firstContactWheel = frontGrounded ? Wheel.Front : Wheel.Back;
        firstContactTime = firstContactWheel == Wheel.Front ? bike.FrontWheelContact.LastGroundedTime : bike.BackWheelContact.LastGroundedTime;
        state = State.Pending;
    }

    void UpdatePending(bool frontGrounded, bool backGrounded)
    {
        bool secondGrounded = firstContactWheel == Wheel.Front ? backGrounded : frontGrounded;
        if (secondGrounded)
        {
            float secondContactTime = firstContactWheel == Wheel.Front
                ? bike.BackWheelContact.LastGroundedTime
                : bike.FrontWheelContact.LastGroundedTime;
            Resolve(firstContactTime, secondContactTime, firstContactWheel);
            return;
        }

        if (Time.time - firstContactTime >= landingTimeout)
        {
            ResolveTimeout();
        }
    }

    void Resolve(float firstTime, float secondTime, Wheel firstWheel)
    {
        float deltaTime = Mathf.Abs(secondTime - firstTime);

        Quality quality;
        if (deltaTime <= perfectThreshold) quality = Quality.Perfect;
        else if (deltaTime <= goodThreshold) quality = Quality.Good;
        else quality = Quality.NotBad;

        // ContactOrder 跟 Quality 是两个独立结果,但共用同一个"够不够接近"的边界(goodThreshold)。
        ContactOrder order = deltaTime <= goodThreshold
            ? ContactOrder.Simultaneous
            : (firstWheel == Wheel.Front ? ContactOrder.FrontFirst : ContactOrder.BackFirst);

        Finish(quality, order, deltaTime, firstWheel);
    }

    void ResolveTimeout()
    {
        // 等到超时都没等到第二只轮子,肯定早就超过 goodThreshold 了,不可能是 Simultaneous。
        ContactOrder order = firstContactWheel == Wheel.Front ? ContactOrder.FrontFirst : ContactOrder.BackFirst;
        Finish(Quality.NotBad, order, Time.time - firstContactTime, firstContactWheel);
    }

    void Finish(Quality quality, ContactOrder order, float deltaTime, Wheel firstWheel)
    {
        state = State.Grounded;
        LastLanding = new LandingResult(quality, order, deltaTime, firstWheel);
        OnLanded?.Invoke(quality, order);
    }

    bool AnyWheelGrounded() => IsGrounded(bike.FrontWheelContact) || IsGrounded(bike.BackWheelContact);

    static bool IsGrounded(WheelContactSensor sensor) => sensor != null && sensor.IsGrounded;
}
