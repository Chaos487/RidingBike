using System;
using UnityEngine;

/// <summary>
/// HP 损毁系统:插在 CrashDetector 和 RunManager/CameraDirector 之间。
/// CrashDetector 判定一次失控只代表"这次姿态/角度失控了"，不直接等于这一局结束——
/// 每次摔车扣一定比例的血量，给车身一点回正、给 CrashDetector 一段无敌时间，
/// 然后放行让玩家继续骑；血量归零那次才是真的摔车，交给 OnFinalCrash 走现在这套结算流程。
/// </summary>
public class BikeDamageSystem : MonoBehaviour
{
    [Header("HP")]
    public float maxHp = 100f;
    public float damagePerCrash = 35f;

    [Header("Recovery")]
    public float invulnerabilitySeconds = 1.5f;
    [Range(0f, 1f)]
    public float recoveryUprightBlend = 0.6f;

    BikeController bike;
    CrashDetector crashDetector;

    float currentHp;

    /// <summary>血量变化时触发(包括扣血、以后如果加回血)，参数是当前血量和满血值，供 UI 更新血条。</summary>
    public event Action<float, float> OnHpChanged;
    /// <summary>血量耗尽，这次才是真的摔车结算。</summary>
    public event Action OnFinalCrash;

    public void Initialize(BikeController bikeController, CrashDetector detector)
    {
        bike = bikeController;
        crashDetector = detector;
        currentHp = maxHp;
        crashDetector.OnCrash += HandleCrash;
    }

    public void ApplySettings(BikeDamageSettings settings)
    {
        if (settings == null) return;

        maxHp = settings.maxHp;
        damagePerCrash = settings.damagePerCrash;
        invulnerabilitySeconds = settings.invulnerabilitySeconds;
        recoveryUprightBlend = settings.recoveryUprightBlend;

        currentHp = maxHp;
    }

    void HandleCrash()
    {
        float hpBefore = currentHp;
        currentHp = Mathf.Max(0f, currentHp - damagePerCrash);

        // 临时验证用:排查"血条不掉血"的问题，确认扣血数值本身是不是符合预期。
        // 确认没问题之后可以删掉。
        Debug.Log($"[BikeDamageSystem] 摔车扣血 maxHp={maxHp} damagePerCrash={damagePerCrash} hpBefore={hpBefore} hpAfter={currentHp}");

        if (currentHp <= 0f)
        {
            OnFinalCrash?.Invoke();
            return;
        }

        RecoverBikeUpright();
        crashDetector.Recover(invulnerabilitySeconds);

        OnHpChanged?.Invoke(currentHp, maxHp);
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
