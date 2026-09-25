using System;
using UnityEngine;

/// <summary>
/// 正式版 Landing Quality:唯一判据是"前后轮有效接地的时间差"(Δt)——不看车身角度、
/// 坡度、角速度、垂直速度。一次腾空只产生一次判定:两轮都离地时(Airborne)任一轮首次
/// 触地记录 FirstContactWheel/FirstContactTime,进入 LandingPending;另一轮触地就算 Δt
/// 分档,或者等超过 landingTimeout 另一轮还没触地就直接判 NotBad;判定完立即广播,
/// 先等待 bike.IsConfirmedGrounded 变 true，确认本次接地已完成防抖；然后才允许在它
/// 再次变 false、且两轮均已离地时重新武装。不能把结算后尚未更新的旧 false 当作新起跳。
/// 从 Grounded 等待下一次确认腾空(经过
/// BikeController.airborneConfirmTime 双向防抖确认过的"真的腾空了")才重新武装
/// (Airborne),避免车身仍贴着地面、或者只是被地形 Collider 重建/悬挂噪声/boost
/// 瞬间顶了一下这类几毫秒的假离地，被误判成"又落地了一次"。
/// </summary>
public class LandingDetector : MonoBehaviour
{
    public enum Quality { Perfect, Good, NotBad }
    public enum ContactOrder { Simultaneous, FrontFirst, BackFirst }
    public enum Wheel { Front, Back }

    enum State { Airborne, Pending, AwaitingGroundedConfirmation, Grounded }

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
        state = bike.IsConfirmedGrounded ? State.Grounded
            : AnyWheelGrounded() ? State.AwaitingGroundedConfirmation : State.Airborne;
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

            case State.AwaitingGroundedConfirmation:
                // Raw wheel contact can settle a landing before BikeController's 0.05s
                // debounce confirms it. Consume that contact once and wait for true;
                // the old false is still the SAME airtime, not a new takeoff.
                if (bike.IsConfirmedGrounded) state = State.Grounded;
                break;

            case State.Grounded:
                // 重新武装的条件改成读 bike.IsConfirmedGrounded(双向防抖过的接地状态)，不再是
                // 原始的"两轮都离地"——地形 Collider 重建/悬挂噪声/boost 瞬间顶一下这类几毫秒的
                // 假离地，现在会被 BikeController.airborneConfirmTime 直接挡掉，不会走到这里，
                // 从源头上减少凭空触发一次新落地判定的次数(而不是靠事后加锁/加冷却掩盖)。
                if (!frontGrounded && !backGrounded && !bike.IsConfirmedGrounded)
                {
                    // 临时调试日志，排查"偶尔一弹一弹"导致的 landing/trick 误判用——加了双向防抖
                    // 之后这条应该只在真的腾空时才打印。排查完可以整段删掉，不影响任何逻辑。
                    Debug.Log($"[LandingDetector] 重新武装(确认腾空) @ t={Time.time:0.0000}");
                    state = State.Airborne;
                }
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

        Debug.Log($"[LandingDetector] 进入 Pending，先触地={firstContactWheel} @ t={firstContactTime:0.0000}");
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

        // 触发 Pending 的那只轮子自己先弹开了(腾空途中蹭了一下地面/小坡坎，没有真正落地)，
        // 另一只轮子也没跟上——这次不算数，取消判定，回到 Airborne 重新等下一次两只轮子都
        // 实打实触地。不这样处理的话，蹭一下地面就会卡在 Pending 里干等 landingTimeout，
        // 到时间就拿蹭地那一刻的旧数据强行结算一次，把明明还在继续的同一次转体腰斩成两段
        // (症状：同一次滞空里连续出现两次"进入滞空"、中间夹着一次不该有的"落地结算")。
        bool firstStillGrounded = firstContactWheel == Wheel.Front ? frontGrounded : backGrounded;
        if (!firstStillGrounded)
        {
            state = State.Airborne;
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
        state = bike.IsConfirmedGrounded ? State.Grounded : State.AwaitingGroundedConfirmation;
        LastLanding = new LandingResult(quality, order, deltaTime, firstWheel);

        // 临时调试日志，排查"偶尔一弹一弹"导致的 landing/trick 误判用——重点看 deltaTime 是不是
        // 小得离谱(几毫秒级)、又没有对应的玩家跳跃输入，那基本可以确认是检测侧的假判定，
        // 不是真的有过一次腾空。排查完可以整段删掉，不影响任何逻辑。
        Debug.Log($"[LandingDetector] 判定完成 Quality={quality} Order={order} " +
                  $"DeltaTime={deltaTime:0.0000} FirstWheel={firstWheel} @ t={Time.time:0.0000}");

        OnLanded?.Invoke(quality, order);
    }

    bool AnyWheelGrounded() => IsGrounded(bike.FrontWheelContact) || IsGrounded(bike.BackWheelContact);

    static bool IsGrounded(WheelContactSensor sensor) => sensor != null && sensor.IsGrounded;
}
