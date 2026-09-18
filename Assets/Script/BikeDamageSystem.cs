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

    /// <summary>因为摔车扣血时触发(不包括 Node 系统改满血值)，参数是当前血量和满血值。
    /// RunManager/CameraDirector 订阅这个来做"摔车了"的提示/震动反馈——如果 Node 系统的
    /// ModifyMaxHp 也复用这个事件，会被误判成又摔了一次车，所以两者分开成不同事件。</summary>
    public event Action<float, float> OnHpChanged;
    /// <summary>满血值被 Node 系统这类"非摔车"来源修改时触发，参数是当前血量和满血值。
    /// 只用来静默刷新血条 UI，不应该触发摔车提示/镜头震动。</summary>
    public event Action<float, float> OnMaxHpChanged;
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

    /// <summary>不管当前还剩多少血，直接判定为致命的最终摔车——供掉进断层(GapFallHandler)
    /// 这类"一律直接判死"的伤害来源使用，不走 HandleCrash 那一整套(回正车身/无敌时间/闪烁
    /// 对已经结束的一局没有意义)。</summary>
    public void ForceFinalCrash()
    {
        currentHp = 0f;
        OnFinalCrash?.Invoke();
    }

    /// <summary>供 Node 系统的 MaxHpFlat 效果使用:同时调整满血值和当前血量(而不是只调满血值、
    /// 让当前血量凭空"多"出一截或者超过新的满血值)，正值当场回一部分血,负值当场扣一部分血。</summary>
    public void ModifyMaxHp(float delta)
    {
        maxHp = Mathf.Max(1f, maxHp + delta);
        currentHp = Mathf.Clamp(currentHp + delta, 0f, maxHp);
        OnMaxHpChanged?.Invoke(currentHp, maxHp);
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
