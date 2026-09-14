using System;
using UnityEngine;

/// <summary>
/// "多条命"损毁系统:插在 CrashDetector 和 RunManager/CameraDirector 之间。
/// CrashDetector 判定一次失控只代表"这次姿态/角度失控了"，不直接等于这一局结束——
/// 前 (maxLives - 1) 次算"部分损毁"：卸掉一个零件(第一次卸前轮、第二次卸行李架)，
/// 给车身一点回正、给 CrashDetector 一段无敌时间，然后放行让玩家继续骑；
/// 第 maxLives 次才是真的摔车，交给 OnFinalCrash 走现在这套结算流程。
///
/// 前轮/行李架都不参与真正的驱动物理(后轮才是电机驱动轮)，卸掉之后车会更难控制
/// (前轮飞走后少了一个支撑点)，但不会破坏"能不能骑"这件事本身。
/// </summary>
public class BikeDamageSystem : MonoBehaviour
{
    public enum DamagedPart { FrontWheel, Rack }

    [Header("Lives")]
    public int maxLives = 3;
    public float invulnerabilitySeconds = 1.5f;
    [Range(0f, 1f)]
    public float recoveryUprightBlend = 0.6f;

    [Header("Front Wheel Eject")]
    public float ejectImpulseMin = 3f;
    public float ejectImpulseMax = 6f;
    public float ejectTorqueMax = 720f;
    public float wheelCleanupDelay = 5f;

    BikeController bike;
    CrashDetector crashDetector;

    int hitsTaken;

    /// <summary>部分损毁(掉零件但没结束这一局)时触发，参数是掉了哪个零件、损毁后还剩几条命。</summary>
    public event Action<DamagedPart, int> OnPartialDamage;
    /// <summary>命耗尽，这次才是真的摔车结算。</summary>
    public event Action OnFinalCrash;

    public void Initialize(BikeController bikeController, CrashDetector detector)
    {
        bike = bikeController;
        crashDetector = detector;
        crashDetector.OnCrash += HandleCrash;
    }

    public void ApplySettings(BikeDamageSettings settings)
    {
        if (settings == null) return;

        maxLives = settings.maxLives;
        invulnerabilitySeconds = settings.invulnerabilitySeconds;
        recoveryUprightBlend = settings.recoveryUprightBlend;
        ejectImpulseMin = settings.ejectImpulseMin;
        ejectImpulseMax = settings.ejectImpulseMax;
        ejectTorqueMax = settings.ejectTorqueMax;
        wheelCleanupDelay = settings.wheelCleanupDelay;
    }

    void HandleCrash()
    {
        hitsTaken++;

        if (hitsTaken >= maxLives)
        {
            OnFinalCrash?.Invoke();
            return;
        }

        DamagedPart part;
        switch (hitsTaken)
        {
            case 1:
                EjectFrontWheel();
                part = DamagedPart.FrontWheel;
                break;
            default:
                bike.DetachRack();
                part = DamagedPart.Rack;
                break;
        }

        RecoverBikeUpright();
        crashDetector.Recover(invulnerabilitySeconds);

        OnPartialDamage?.Invoke(part, maxLives - hitsTaken);
    }

    void EjectFrontWheel()
    {
        Rigidbody2D wheel = bike.DetachFrontWheel();
        if (wheel == null) return;

        // 往后上方甩出去(跟车身前进方向相反 + 向上)，比单纯往上弹更像"被甩脱"。
        Vector2 kickDir = new Vector2(-bike.driveDirection, 1f).normalized;
        float impulse = UnityEngine.Random.Range(ejectImpulseMin, ejectImpulseMax);
        wheel.AddForce(kickDir * impulse, ForceMode2D.Impulse);
        wheel.AddTorque(UnityEngine.Random.Range(-ejectTorqueMax, ejectTorqueMax), ForceMode2D.Impulse);

        Destroy(wheel.gameObject, wheelCleanupDelay);
    }

    void RecoverBikeUpright()
    {
        Rigidbody2D rb = bike.bikeRigidbody;
        rb.angularVelocity = 0f;

        float targetAngle = bike.IsGrounded() ? bike.GetGroundSlopeAngle() : 0f;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, recoveryUprightBlend));
    }

    void OnDestroy()
    {
        if (crashDetector != null) crashDetector.OnCrash -= HandleCrash;
    }
}
