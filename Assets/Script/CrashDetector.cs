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

    [Header("Crash Rule")]
    [Tooltip("车身倾角超过该值(度)且触地时视为失控。")]
    public float tiltThreshold = 65f;
    [Tooltip("触地检测距离(从车身中心向下)。")]
    public float groundCheckDistance = 0.6f;
    public LayerMask groundLayer = ~0;
    [Tooltip("倾角超限需要持续这么久才真正判定摔车,给玩家一点救车的余地。")]
    public float crashConfirmTime = 0.15f;

    public event Action OnCrash;

    float overTiltTime;
    bool crashed;

    void FixedUpdate()
    {
        if (crashed || bikeRigidbody == null) return;

        bool grounded = Physics2D.Raycast(bikeRigidbody.position, Vector2.down, groundCheckDistance, groundLayer).collider != null;
        float tilt = Mathf.Abs(Mathf.DeltaAngle(bikeRigidbody.rotation, 0f));

        if (grounded && tilt > tiltThreshold)
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
