using System;
using UnityEngine;

/// <summary>
/// 摔车判定:车身现有的每个轮子都真实触地、且车身倾角超过阈值、并持续一小段时间后判定为摔车。
/// 触地用真实物理接触(WheelContactSensor),不用距离射线——射线只代表"车身中心离地面够近",
/// 滞空高度不够大时会在还没真正落地前就先报"触地",从而在飞行中被误判成摔车。
/// 必须现有的轮子都触地才开始判定,只有一个轮子(比如起跳瞬间后轮还没离地、或落地时前轮先/后轮先着地)
/// 是正常的过渡姿态,车身倾角本来就会比较大,不代表摔车。前轮被 BikeDamageSystem 卸掉之后
/// FrontWheelContact 会变成 null,这里跟着自动只看剩下的轮子,不会因为少了一个轮子就永远判不出摔车。
/// </summary>
public class CrashDetector : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D bikeRigidbody;
    [Tooltip("用来读取前后轮真实触地状态,以及排除主动触发的空中 360 旋转,避免转体过程中被误判成摔车。")]
    public BikeController bikeController;

    [Header("Crash Rule")]
    [Tooltip("车身倾角超过该值(度)且现有轮子都触地时视为失控。")]
    public float tiltThreshold = 65f;
    [Tooltip("现有轮子都触地的状态需要连续保持这么久才开始看倾角,防止接触碰撞体的单帧抖动被误判。")]
    public float groundSettleTime = 0.05f;
    [Tooltip("落地之后，倾角超限需要再持续这么久才真正判定摔车,给玩家一点救车的余地。")]
    public float crashConfirmTime = 0.15f;

    public event Action OnCrash;

    float groundedTime;
    float overTiltTime;
    float invulnerableUntil;
    bool crashed;

    /// <summary>供 BikeDamageSystem 在"部分损毁"(掉零件但没真的结束这一局)之后调用:
    /// 复位摔车判定状态,并给一小段无敌时间,避免同一次摔倒的姿态在下一帧又立刻被判一次摔车。</summary>
    public void Recover(float invulnerableSeconds)
    {
        crashed = false;
        groundedTime = 0f;
        overTiltTime = 0f;
        invulnerableUntil = Time.time + invulnerableSeconds;
    }

    void FixedUpdate()
    {
        if (crashed || bikeRigidbody == null) return;
        if (Time.time < invulnerableUntil) return;

        if (bikeController != null && bikeController.IsSpinning)
        {
            groundedTime = 0f;
            overTiltTime = 0f;
            return;
        }

        if (!AllExistingWheelsGrounded())
        {
            // 只要有一个还装着的轮子没触地——空中,或者起跳/落地的单轮过渡瞬间——都不判定摔车。
            groundedTime = 0f;
            overTiltTime = 0f;
            return;
        }

        groundedTime += Time.fixedDeltaTime;
        bool settled = groundedTime >= groundSettleTime;

        float tilt = Mathf.Abs(Mathf.DeltaAngle(bikeRigidbody.rotation, 0f));

        if (settled && tilt > tiltThreshold)
        {
            overTiltTime += Time.fixedDeltaTime;
            if (overTiltTime >= crashConfirmTime)
            {
                crashed = true;
                LogCrashDiagnostics(tilt);
                OnCrash?.Invoke();
            }
        }
        else
        {
            overTiltTime = 0f;
        }
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

    /// <summary>摔车瞬间把判定用到的全部状态打成一条独立的 log,方便复现/排查误判(比如空中被判摔车)。</summary>
    void LogCrashDiagnostics(float tilt)
    {
        bool frontContact = bikeController != null && bikeController.FrontWheelContact != null && bikeController.FrontWheelContact.IsGrounded;
        bool backContact = bikeController != null && bikeController.BackWheelContact != null && bikeController.BackWheelContact.IsGrounded;

        Debug.LogWarning(
            $"[CrashDetector] 摔车 pos={bikeRigidbody.position} rot={bikeRigidbody.rotation:0.0} tilt={tilt:0.0} " +
            $"vel={bikeRigidbody.linearVelocity} angVel={bikeRigidbody.angularVelocity:0.0} " +
            $"frontContact={frontContact} backContact={backContact} " +
            $"isSpinning={bikeController != null && bikeController.IsSpinning} spinDeg={(bikeController != null ? bikeController.SpinAccumulatedDegrees : 0f):0.0} " +
            $"groundedTime={groundedTime:0.00} overTiltTime={overTiltTime:0.00}");
    }
}
