using System;
using UnityEngine;

/// <summary>
/// 摔车判定:不是"车歪了就摔"，而是"自行车已经失去可恢复的骑行状态才摔车"。
/// 四级状态机(Normal → Warning → Critical → Crashed)，只有 Critical 持续够久才真正摔车；
/// Critical 期间如果车身倾角/角速度显示"正在回正"，会逐级降回 Warning → Normal，形成救车机制。
/// 设计依据见 Desktop/RidingBike_Crash_Detection_Design.md。
///
/// 触地用真实物理接触(BikeController.FrontWheelContact/BackWheelContact)，不用距离射线——
/// 射线只代表"车身中心离地面够近"，滞空高度不够大时会在还没真正落地前就先报"触地"。
/// Critical/Crashed 都要求现有的每个轮子都触地(前轮被 BikeDamageSystem 卸掉之后自动只看剩下的轮子)；
/// Warning 不要求触地——空中姿态失控也应该能看到"开始危险"，但不会真的摔车，只有落地才会往下判。
///
/// 车身本身现在还没有独立的碰撞体("Body Contact")，也没有做基于真实碰撞冲量的撞击强度判定——
/// 这两项是文档里改动物理表现本身的部分，留到下一步单独验证，不在这一版里。
/// </summary>
public class CrashDetector : MonoBehaviour
{
    public enum CrashState { Normal, Warning, Critical, Crashed }

    [Header("References")]
    public Rigidbody2D bikeRigidbody;
    [Tooltip("用来读取前后轮真实触地状态、地面坡度，以及排除主动触发的空中 360 旋转，避免转体过程中被误判成摔车。")]
    public BikeController bikeController;

    [Header("Angle (相对当前地面坡度算；空中没有坡度参考时按水平算)")]
    public float warningAngle = 35f;
    public float criticalAngle = 50f;
    public float maxRecoverableAngle = 60f;

    [Header("Timing")]
    public float minimumDangerTime = 0.05f;
    public float crashConfirmTime = 0.20f;
    public float recoveryTime = 0.10f;

    [Header("Recovery (角速度回正判定)")]
    public float recoveringAngularSpeedThreshold = 30f;

    /// <summary>真正摔车(Crashed)那一刻触发，语义跟之前完全一样——外部系统不需要关心中间状态。</summary>
    public event Action OnCrash;
    /// <summary>每次状态变化时触发，供以后的 UI/镜头/音效按 Warning/Critical 做分级反馈用(这一版还没接)。</summary>
    public event Action<CrashState> OnStateChanged;

    public CrashState State { get; private set; } = CrashState.Normal;

    float dangerTime;
    float overTiltTime;
    float recoveryTimer;
    float invulnerableUntil;

    /// <summary>供 BikeDamageSystem 在"部分损毁"(掉零件但没真的结束这一局)之后调用:
    /// 复位状态机，并给一小段无敌时间，避免同一次摔倒的姿态在下一帧又立刻被判一次摔车。</summary>
    public void Recover(float invulnerableSeconds)
    {
        EnterState(CrashState.Normal);
        invulnerableUntil = Time.time + invulnerableSeconds;
    }

    public void ApplySettings(CrashDetectorSettings settings)
    {
        if (settings == null) return;

        warningAngle = settings.warningAngle;
        criticalAngle = settings.criticalAngle;
        maxRecoverableAngle = settings.maxRecoverableAngle;
        minimumDangerTime = settings.minimumDangerTime;
        crashConfirmTime = settings.crashConfirmTime;
        recoveryTime = settings.recoveryTime;
        recoveringAngularSpeedThreshold = settings.recoveringAngularSpeedThreshold;
    }

    void FixedUpdate()
    {
        if (State == CrashState.Crashed || bikeRigidbody == null) return;
        if (Time.time < invulnerableUntil) return;

        if (bikeController != null && bikeController.IsSpinning)
        {
            // 主动触发的空中旋转：不管转到多大角度都不计入危险状态。
            if (State != CrashState.Normal) EnterState(CrashState.Normal);
            return;
        }

        bool grounded = AllExistingWheelsGrounded();
        float targetAngle = (bikeController != null && grounded) ? bikeController.GetGroundSlopeAngle() : 0f;
        float angleError = Mathf.DeltaAngle(targetAngle, bikeRigidbody.rotation);
        float tilt = Mathf.Abs(angleError);
        float angularVelocity = bikeRigidbody.angularVelocity;

        // 正在回正:角速度的方向跟"倾角超出目标的方向"相反，且幅度不是噪声。
        bool recovering = angleError * angularVelocity < 0f && Mathf.Abs(angularVelocity) > recoveringAngularSpeedThreshold;
        // 倾角已经大到基本没救了的话，就算角速度显示在回正也不认。
        bool stillFalling = tilt > maxRecoverableAngle || !recovering;
        bool criticalConditionMet = grounded && tilt > criticalAngle && stillFalling;

        switch (State)
        {
            case CrashState.Normal:
                if (tilt > warningAngle) EnterState(CrashState.Warning);
                break;

            case CrashState.Warning:
                if (tilt <= warningAngle)
                {
                    recoveryTimer += Time.fixedDeltaTime;
                    if (recoveryTimer >= recoveryTime) EnterState(CrashState.Normal);
                }
                else
                {
                    recoveryTimer = 0f;
                    if (criticalConditionMet)
                    {
                        dangerTime += Time.fixedDeltaTime;
                        if (dangerTime >= minimumDangerTime) EnterState(CrashState.Critical);
                    }
                    else
                    {
                        dangerTime = 0f;
                    }
                }
                break;

            case CrashState.Critical:
                if (criticalConditionMet)
                {
                    recoveryTimer = 0f;
                    overTiltTime += Time.fixedDeltaTime;
                    if (overTiltTime >= crashConfirmTime)
                    {
                        ConfirmCrash(tilt);
                    }
                }
                else
                {
                    overTiltTime = 0f;
                    recoveryTimer += Time.fixedDeltaTime;
                    if (recoveryTimer >= recoveryTime) EnterState(CrashState.Warning);
                }
                break;
        }
    }

    void EnterState(CrashState next)
    {
        State = next;
        dangerTime = 0f;
        overTiltTime = 0f;
        recoveryTimer = 0f;
        OnStateChanged?.Invoke(next);
    }

    void ConfirmCrash(float tilt)
    {
        State = CrashState.Crashed;
        LogCrashDiagnostics(tilt);
        OnStateChanged?.Invoke(CrashState.Crashed);
        OnCrash?.Invoke();
    }

    /// <summary>车身现在实际装着的每一个轮子(FrontWheelContact/BackWheelContact 非 null 的那些)
    /// 是不是都真的触地了。轮子被卸掉之后对应引用会是 null,直接跳过,不参与判定——
    /// 不然前轮飞走之后永远凑不齐"两轮都触地"，反而变成了摔不了车的无敌状态。
    /// 至少要还剩一个轮子,不然(理论上不会发生,后轮不会被卸)直接不判定。</summary>
    bool AllExistingWheelsGrounded()
    {
        if (bikeController == null) return false;

        WheelContactSensor front = bikeController.FrontWheelContact;
        WheelContactSensor back = bikeController.BackWheelContact;

        bool frontOk = front == null || front.IsGrounded;
        bool backOk = back == null || back.IsGrounded;
        bool anyWheelLeft = front != null || back != null;

        return anyWheelLeft && frontOk && backOk;
    }

    /// <summary>摔车瞬间把判定用到的全部状态打成一条独立的 log,方便复现/排查误判。</summary>
    void LogCrashDiagnostics(float tilt)
    {
        bool frontContact = bikeController != null && bikeController.FrontWheelContact != null && bikeController.FrontWheelContact.IsGrounded;
        bool backContact = bikeController != null && bikeController.BackWheelContact != null && bikeController.BackWheelContact.IsGrounded;

        Debug.LogWarning(
            $"[CrashDetector] 摔车 pos={bikeRigidbody.position} rot={bikeRigidbody.rotation:0.0} tilt={tilt:0.0} " +
            $"vel={bikeRigidbody.linearVelocity} angVel={bikeRigidbody.angularVelocity:0.0} " +
            $"frontContact={frontContact} backContact={backContact} " +
            $"isSpinning={bikeController != null && bikeController.IsSpinning} spinDeg={(bikeController != null ? bikeController.SpinAccumulatedDegrees : 0f):0.0} " +
            $"overTiltTime={overTiltTime:0.00}");
    }
}
