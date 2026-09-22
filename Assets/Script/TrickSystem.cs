using System;
using UnityEngine;

/// <summary>
/// 落地时把 BikeController 本次滞空累计的旋转角度换算成"完整转了几圈",按圈数查表给
/// Trick Score——只看圈数,不看角度精确档位,不足一圈不计分。故意跟 Landing Quality
/// 完全解耦:这里不读、不判断 LandingDetector.Quality,好落地/差落地转出同样的圈数拿
/// 一样的分,好不好落地是另一个独立系统的事。
/// </summary>
public class TrickSystem : MonoBehaviour
{
    int[] scorePerLap = { 50, 150, 300, 500, 750 };

    BikeController bike;
    LandingDetector landingDetector;

    /// <summary>特技结算成功,score 是这次的得分,laps 是完整转了几圈(仅供 UI 展示用，比如 "Backflip x{laps}")。</summary>
    public event Action<int, int> OnTrickScored;

    public void Initialize(BikeController bikeController, LandingDetector detector)
    {
        bike = bikeController;
        landingDetector = detector;
        landingDetector.OnLanded += HandleLanded;
    }

    public void ApplySettings(TrickSystemSettings settings)
    {
        if (settings == null || settings.scorePerLap == null || settings.scorePerLap.Length == 0) return;
        scorePerLap = settings.scorePerLap;
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        float degrees = bike.SpinAccumulatedDegrees;
        int laps = Mathf.FloorToInt(Mathf.Abs(degrees) / 360f);
        if (laps <= 0) return; // 没转满一圈，压根没触发过特技

        OnTrickScored?.Invoke(ScoreForLaps(laps), laps);
    }

    int ScoreForLaps(int laps)
    {
        // 超过表里配置的最高圈数，直接沿用最后一档（最高分），不会数组越界。
        int index = Mathf.Clamp(laps, 1, scorePerLap.Length) - 1;
        return scorePerLap[index];
    }

    void OnDestroy()
    {
        if (landingDetector != null) landingDetector.OnLanded -= HandleLanded;
    }
}
