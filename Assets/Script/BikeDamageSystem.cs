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
    [Tooltip("摔车扣血之后这段时间内不会再次扣血(CrashDetector 直接跳过判定)，车身贴图同步闪烁提示玩家。")]
    public float invulnerabilitySeconds = 5f;
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
        currentHp = Mathf.Max(0f, currentHp - damagePerCrash);

        if (currentHp <= 0f)
        {
            OnFinalCrash?.Invoke();
            return;
        }

        RecoverBikeUpright();
        crashDetector.Recover(invulnerabilitySeconds);
        bike.PlayInvulnerabilityFlash(invulnerabilitySeconds);

        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>纯粹扣血，不走 HandleCrash 那一整套(不回正车身、不给无敌时间、不闪烁)——
    /// 供掉进断层(GapFallHandler)这类没有实体碰撞、车身早就不在画面里的伤害来源使用。
    /// 返回 false 表示这一下正好把血扣没了(已经触发 OnFinalCrash)，调用方应该直接放弃
    /// 自己那一套后续流程，交给正常的摔车结算接管。</summary>
    public bool ApplyDamage(float amount)
    {
        currentHp = Mathf.Max(0f, currentHp - amount);

        if (currentHp <= 0f)
        {
            OnFinalCrash?.Invoke();
            return false;
        }

        OnHpChanged?.Invoke(currentHp, maxHp);
        return true;
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
