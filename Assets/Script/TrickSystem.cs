using System;
using UnityEngine;

/// <summary>
/// 落地时把 BikeController 本次滞空累计的旋转角度换算成"完整转了几圈"——只看圈数,不看
/// 角度精确档位,不足一圈不触发。故意跟 Landing Quality 完全解耦:这里不读、不判断
/// LandingDetector.Quality,好落地/差落地转出同样的圈数算同样的圈数,好不好落地是另一个
/// 独立系统的事。这个类本身**不计分**——"转了几圈该给多少分"这个换算已经挪到
/// ScoreSystem.cs(读 ScoreSettings.scorePerLap),这里只负责判定"转满了几圈"这个事实。
/// </summary>
public class TrickSystem : MonoBehaviour
{
    BikeController bike;
    LandingDetector landingDetector;

    bool wasGrounded = true;
    int loggedLaps;

    /// <summary>特技结算成功,laps 是完整转了几圈——不带分数,分数由 ScoreSystem 订阅这个
    /// 事件之后自己查 ScoreSettings 换算。</summary>
    public event Action<int> OnTrickCompleted;

    public void Initialize(BikeController bikeController, LandingDetector detector)
    {
        bike = bikeController;
        landingDetector = detector;
        landingDetector.OnLanded += HandleLanded;
        wasGrounded = bike.IsConfirmedGrounded;
    }

    // 临时调试日志，排查"转了一圈但没判定"这类问题用——实时打出腾空开始/每转满一圈/落地结算
    // 三种时机，方便对照实际按键手感跟 SpinAccumulatedDegrees 记录的度数是不是一致。
    // 读 IsConfirmedGrounded(带 landingConfirmTime 防抖)而不是 IsWheelGrounded，这样日志里的
    // "开始判定"时机才跟 BikeController 实际用来重置旋转基准的时机保持一致，不会因为腾空途中
    // 蹭一下地面的单帧假触地凭空多打一轮日志。确认稳定之后可以整段删掉，不影响任何逻辑。
    void Update()
    {
        if (bike == null) return;

        bool grounded = bike.IsConfirmedGrounded;

        if (!wasGrounded && !grounded)
        {
            int currentLaps = Mathf.FloorToInt(Mathf.Abs(bike.SpinAccumulatedDegrees) / 360f);
            while (loggedLaps < currentLaps)
            {
                loggedLaps++;
                Debug.Log($"[TrickSystem] 第 {loggedLaps} 圈完成 (累计角度={bike.SpinAccumulatedDegrees:0.0}°)");
            }
        }
        else if (wasGrounded && !grounded)
        {
            loggedLaps = 0;
            Debug.Log("[TrickSystem] 开始判定 —— 进入滞空");
        }

        wasGrounded = grounded;
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        float degrees = bike.SpinAccumulatedDegrees;
        int laps = Mathf.FloorToInt(Mathf.Abs(degrees) / 360f);
        Debug.Log($"[TrickSystem] 结束判定 —— 落地,累计角度={degrees:0.0}°, 圈数={laps}, Quality={quality}, ContactOrder={order}");

        if (laps <= 0) return; // 没转满一圈，压根没触发过特技

        OnTrickCompleted?.Invoke(laps);
    }

    void OnDestroy()
    {
        if (landingDetector != null) landingDetector.OnLanded -= HandleLanded;
    }
}
