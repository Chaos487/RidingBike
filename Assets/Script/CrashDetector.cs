using System;
using UnityEngine;

/// <summary>
/// 摔车判定:车身触地且倾角超过阈值、并持续一小段时间后判定为摔车。
/// 只在触地时判定,空中翻转(比如主动出的 360)不会误判。
/// </summary>
public class CrashDetector : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D bikeRigidbody;
    [Tooltip("用来排除主动触发的空中 360 旋转,避免转体过程中被误判成摔车。")]
    public BikeController bikeController;

    [Header("Crash Rule")]
    [Tooltip("车身倾角超过该值(度)且触地时视为失控。")]
    public float tiltThreshold = 65f;
    [Tooltip("触地检测距离(从车身中心向下)。跳跃弧线下降段离地面够近时这条射线就会瞬间报一次触地，不代表真的落地了。")]
    public float groundCheckDistance = 0.6f;
    public LayerMask groundLayer = ~0;
    [Tooltip("触地状态需要连续保持这么久才算真的落地，而不是滞空途中蹭到判定距离的一瞬间——只有真正落地之后才开始看倾角，从根上保证空中不会被判定摔车。")]
    public float groundSettleTime = 0.1f;
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

        bool grounded = Physics2D.Raycast(bikeRigidbody.position, Vector2.down, groundCheckDistance, groundLayer).collider != null;

        if (!grounded)
        {
            // 空中完全不判定摔车——只要射线没碰到东西，不管倾角多大都放行(比如主动出的空翻)。
            groundedTime = 0f;
            overTiltTime = 0f;
            return;
        }

        groundedTime += Time.fixedDeltaTime;
        bool settled = groundedTime >= groundSettleTime; // 真的落地了，不是滞空途中蹭了一下

        float tilt = Mathf.Abs(Mathf.DeltaAngle(bikeRigidbody.rotation, 0f));

        if (settled && tilt > tiltThreshold)
        {
            overTiltTime += Time.fixedDeltaTime;
            if (overTiltTime >= crashConfirmTime)
            {
                crashed = true;
                OnCrash?.Invoke();
            }
        }
        else
        {
            overTiltTime = 0f;
        }
    }
}
