using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 速度反应式镜头:低速/摔车时聚焦放大,加速/Shift 时丝滑拉远,空中略微拉远,
/// 落地一个小回弹,车速越快镜头目标点越往前偏移。全部用 DOTween 驱动 Cinemachine 的
/// Lens.OrthographicSize 和 CinemachinePositionComposer.TargetOffset,不直接碰
/// 相机的 Transform——那部分仍然完全交给 Cinemachine 自己的跟随/阻尼逻辑。
///
/// 优先级(从高到低,互相排斥):摔车聚焦(接管后不再退出,直到重开) > 落地回弹(短暂,期间暂停连续缩放)
/// > 连续的"速度/空中状态"缩放。
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CameraDirector : MonoBehaviour
{
    CinemachineCamera cmCamera;
    CinemachinePositionComposer composer;
    CinemachineImpulseSource impulseSource;
    BikeController bike;
    BikeDamageSystem damageSystem;
    LandingDetector landingDetector;

    float minOrthoSize = 2.4f;
    float maxOrthoSize = 4.2f;
    float crashOrthoSize = 1.8f;
    float airborneZoomOutBonus = 0.6f;

    float zoomInDuration = 0.18f;
    Ease zoomInEase = Ease.OutQuad;
    float zoomOutDuration = 1.1f;
    Ease zoomOutEase = Ease.OutSine;

    float crashZoomDuration = 0.12f;
    Ease crashZoomEase = Ease.OutBack;
    float crashShakeAmplitude = 0.6f;
    float partialDamageShakeAmplitude = 0.25f;

    float landingPunchAmount = 0.12f;
    float landingPunchInDuration = 0.08f;
    float landingPunchOutDuration = 0.22f;
    float perfectLandingPunchScale = 0.2f;
    float goodLandingPunchScale = 1f;
    float badLandingPunchScale = 1.8f;

    float maxLookaheadOffset = 1.6f;
    float lookaheadDuration = 0.6f;
    Ease lookaheadEase = Ease.OutSine;

    float retargetThreshold = 0.03f;

    Tweener zoomTweener;
    Tweener lookaheadTweener;
    Sequence landingPunchSequence;

    bool isCrashed;
    bool punchInProgress;
    float lastZoomTarget;
    float lastLookaheadTarget;
    float lookaheadDirection = 1f;

    void Awake()
    {
        cmCamera = GetComponent<CinemachineCamera>();
        composer = GetComponent<CinemachinePositionComposer>();
        lastZoomTarget = cmCamera.Lens.OrthographicSize;
    }

    public void Initialize(BikeController bikeController, BikeDamageSystem bikeDamageSystem, CinemachineImpulseSource impulse, LandingDetector landing)
    {
        bike = bikeController;
        damageSystem = bikeDamageSystem;
        impulseSource = impulse;
        landingDetector = landing;
        damageSystem.OnFinalCrash += HandleFinalCrash;
        damageSystem.OnPartialDamage += HandlePartialDamage;
        landingDetector.OnLanded += HandleLanded;
    }

    public void ApplySettings(CameraDirectorSettings settings)
    {
        if (settings == null) return;

        minOrthoSize = settings.minOrthoSize;
        maxOrthoSize = settings.maxOrthoSize;
        crashOrthoSize = settings.crashOrthoSize;
        airborneZoomOutBonus = settings.airborneZoomOutBonus;
        zoomInDuration = settings.zoomInDuration;
        zoomInEase = settings.zoomInEase;
        zoomOutDuration = settings.zoomOutDuration;
        zoomOutEase = settings.zoomOutEase;
        crashZoomDuration = settings.crashZoomDuration;
        crashZoomEase = settings.crashZoomEase;
        crashShakeAmplitude = settings.crashShakeAmplitude;
        partialDamageShakeAmplitude = settings.partialDamageShakeAmplitude;
        landingPunchAmount = settings.landingPunchAmount;
        landingPunchInDuration = settings.landingPunchInDuration;
        landingPunchOutDuration = settings.landingPunchOutDuration;
        perfectLandingPunchScale = settings.perfectLandingPunchScale;
        goodLandingPunchScale = settings.goodLandingPunchScale;
        badLandingPunchScale = settings.badLandingPunchScale;
        maxLookaheadOffset = settings.maxLookaheadOffset;
        lookaheadDuration = settings.lookaheadDuration;
        lookaheadEase = settings.lookaheadEase;
        retargetThreshold = settings.retargetThreshold;
    }

    void Update()
    {
        if (bike == null || isCrashed) return;

        // 落地那一刻的回弹改成订阅 LandingDetector 抛出的事件，不再自己单独判一遍
        // "上一帧空中、这一帧触地"——避免两套系统各判一次、逻辑重复。
        if (!punchInProgress)
        {
            UpdateZoom(bike.IsGrounded());
        }
        UpdateLookahead();
    }

    void UpdateZoom(bool grounded)
    {
        float speed = Mathf.Abs(bike.bikeRigidbody.linearVelocity.x);
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(bike.MaxLinearSpeed, 0.01f));

        // Boost 直接把目标拉满,不等物理车速真的追上去——按下加速键那一下就该有反应。
        float target = bike.IsBoosting ? maxOrthoSize : Mathf.Lerp(minOrthoSize, maxOrthoSize, speedRatio);
        if (!grounded) target += airborneZoomOutBonus;
        target = Mathf.Clamp(target, minOrthoSize, maxOrthoSize + airborneZoomOutBonus);

        if (Mathf.Abs(target - lastZoomTarget) < retargetThreshold) return;

        bool zoomingIn = target < lastZoomTarget;
        RetargetZoom(target, zoomingIn ? zoomInDuration : zoomOutDuration, zoomingIn ? zoomInEase : zoomOutEase);
    }

    void RetargetZoom(float target, float duration, Ease ease)
    {
        lastZoomTarget = target;

        if (zoomTweener != null && zoomTweener.IsActive())
        {
            zoomTweener.ChangeEndValue(target, duration, true).SetEase(ease);
        }
        else
        {
            zoomTweener = DOTween.To(GetOrthoSize, SetOrthoSize, target, duration).SetEase(ease);
        }
    }

    float GetOrthoSize() => cmCamera.Lens.OrthographicSize;

    void SetOrthoSize(float value)
    {
        LensSettings lens = cmCamera.Lens;
        lens.OrthographicSize = value;
        cmCamera.Lens = lens;
    }

    void UpdateLookahead()
    {
        if (composer == null) return;

        float velocityX = bike.bikeRigidbody.linearVelocity.x;
        float speedRatio = Mathf.Clamp01(Mathf.Abs(velocityX) / Mathf.Max(bike.MaxLinearSpeed, 0.01f));
        if (Mathf.Abs(velocityX) > 0.3f) lookaheadDirection = Mathf.Sign(velocityX);

        float target = maxLookaheadOffset * speedRatio * lookaheadDirection;
        if (Mathf.Abs(target - lastLookaheadTarget) < retargetThreshold) return;

        lastLookaheadTarget = target;

        if (lookaheadTweener != null && lookaheadTweener.IsActive())
        {
            lookaheadTweener.ChangeEndValue(target, lookaheadDuration, true).SetEase(lookaheadEase);
        }
        else
        {
            lookaheadTweener = DOTween.To(GetLookaheadX, SetLookaheadX, target, lookaheadDuration).SetEase(lookaheadEase);
        }
    }

    float GetLookaheadX() => composer.TargetOffset.x;

    void SetLookaheadX(float x)
    {
        Vector3 offset = composer.TargetOffset;
        offset.x = x;
        composer.TargetOffset = offset;
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        if (isCrashed) return;

        // 落地质量越差,回弹越明显;Perfect 落地几乎感觉不到回弹,突出"稳"。
        float scale = quality switch
        {
            LandingDetector.Quality.Perfect => perfectLandingPunchScale,
            LandingDetector.Quality.Good => goodLandingPunchScale,
            _ => badLandingPunchScale,
        };
        PlayLandingPunch(landingPunchAmount * scale);
    }

    void PlayLandingPunch(float punchAmount)
    {
        punchInProgress = true;
        zoomTweener?.Kill();

        float baseSize = GetOrthoSize();
        float punchTarget = Mathf.Max(minOrthoSize, baseSize - punchAmount);

        landingPunchSequence?.Kill();
        landingPunchSequence = DOTween.Sequence();
        landingPunchSequence.Append(DOTween.To(GetOrthoSize, SetOrthoSize, punchTarget, landingPunchInDuration).SetEase(Ease.OutQuad));
        landingPunchSequence.Append(DOTween.To(GetOrthoSize, SetOrthoSize, baseSize, landingPunchOutDuration).SetEase(Ease.OutSine));
        landingPunchSequence.OnComplete(() =>
        {
            punchInProgress = false;
            lastZoomTarget = baseSize;
        });
    }

    void HandleFinalCrash()
    {
        isCrashed = true;

        zoomTweener?.Kill();
        lookaheadTweener?.Kill();
        landingPunchSequence?.Kill();
        punchInProgress = false;

        lastZoomTarget = crashOrthoSize;
        zoomTweener = DOTween.To(GetOrthoSize, SetOrthoSize, crashOrthoSize, crashZoomDuration).SetEase(crashZoomEase);

        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(crashShakeAmplitude);
        }
    }

    // 部分损毁(掉零件但没结束这一局)只给一次轻微震动，镜头照常跟随/缩放，
    // 不做上面那套"接管定格"——玩家需要立刻感觉到自己还能继续骑。
    void HandlePartialDamage(BikeDamageSystem.DamagedPart part, int livesRemaining)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(partialDamageShakeAmplitude);
        }
    }

    void OnDestroy()
    {
        zoomTweener?.Kill();
        lookaheadTweener?.Kill();
        landingPunchSequence?.Kill();
        if (damageSystem != null)
        {
            damageSystem.OnFinalCrash -= HandleFinalCrash;
            damageSystem.OnPartialDamage -= HandlePartialDamage;
        }
        if (landingDetector != null) landingDetector.OnLanded -= HandleLanded;
    }
}
