using System;
using UnityEngine;

/// <summary>
/// 摔车判定:车身触地且倾角超过阈值、并持续一小段时间后判定为摔车。
/// 触地用 BikeController.IsWheelGrounded(前后轮真实物理接触),不用距离射线——
/// 射线只代表"车身中心离地面够近",滞空高度不够大时会在还没真正落地前就先报"触地",
/// 从而在飞行中被误判成摔车;真实轮胎接触则不存在这个问题,没碰到就是没碰到。
/// </summary>
public class CrashDetector : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D bikeRigidbody;
    [Tooltip("用来读取真实轮胎接触状态(IsWheelGrounded),以及排除主动触发的空中 360 旋转,避免转体过程中被误判成摔车。")]
    public BikeController bikeController;

    [Header("Crash Rule")]
    [Tooltip("车身倾角超过该值(度)且触地时视为失控。")]
    public float tiltThreshold = 65f;
    [Tooltip("触地状态需要连续保持这么久才开始看倾角,防止落地瞬间轮胎接触碰撞体的单帧抖动被误判。")]
    public float groundSettleTime = 0.05f;
    [Tooltip("落地之后，倾角超限需要再持续这么久才真正判定摔车,给玩家一点救车的余地。")]
    public float crashConfirmTime = 0.15f;

    public event Action OnCrash;

    float groundedTime;
    float overTiltTime;
    bool crashed;

    void FixedUpdate()
    {
        if (crashed || bikeRigidbody == null) return;

        if (bikeController != null && bikeController.IsSpinning)
        {
            groundedTime = 0f;
            overTiltTime = 0f;
            return;
        }

        bool grounded = bikeController != null && bikeController.IsWheelGrounded;

        if (!grounded)
        {
            // 空中完全不判定摔车——轮胎没真的碰到地面,不管倾角多大都放行(比如主动出的空翻)。
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
