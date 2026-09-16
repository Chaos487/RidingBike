using System;
using UnityEngine;

/// <summary>
/// 摔车判定:车架本身的碰撞体(BikeController.BodyContact)一旦真的碰到东西，直接判定摔车。
/// 不再用倾角阈值/计时器/角速度回正这套状态机——那套东西调起来太绕，价值也大半被
/// BodyContact 这个碰撞体本身取代了:它的位置和大小本来就是刻意摆在车身正常骑行/跳跃/
/// 落地都碰不到地面的地方，只有车身真的歪倒到相当程度才会接触到——"碰到了"本身
/// 就已经等价于"倾角已经很危险了"，不需要再额外算一遍角度。
/// </summary>
public class CrashDetector : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D bikeRigidbody;
    [Tooltip("用来读取车架碰撞体(BodyContact)的真实触地状态。")]
    public BikeController bikeController;

    public event Action OnCrash;

    bool crashed;
    float invulnerableUntil;

    /// <summary>供 BikeDamageSystem 在"部分损毁"(掉零件但没真的结束这一局)之后调用:
    /// 复位判定，并给一小段无敌时间，避免同一次摔倒的姿态在下一帧又立刻被判一次摔车。</summary>
    public void Recover(float invulnerableSeconds)
    {
        crashed = false;
        invulnerableUntil = Time.time + invulnerableSeconds;
    }

    void FixedUpdate()
    {
        if (crashed || bikeController == null) return;
        if (Time.time < invulnerableUntil) return;

        if (bikeController.BodyContact == null || !bikeController.BodyContact.IsGrounded) return;

        crashed = true;
        LogCrashDiagnostics();
        OnCrash?.Invoke();
    }

    void LogCrashDiagnostics()
    {
        if (bikeRigidbody == null) return;

        Debug.LogWarning(
            $"[CrashDetector] 摔车(车架触地) pos={bikeRigidbody.position} rot={bikeRigidbody.rotation:0.0} " +
            $"vel={bikeRigidbody.linearVelocity} angVel={bikeRigidbody.angularVelocity:0.0}");
    }
}
