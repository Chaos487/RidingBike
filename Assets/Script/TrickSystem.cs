using System;
using UnityEngine;

/// <summary>
/// 落地时把 BikeController 本次滞空累计的旋转角度换算成特技分数。
/// 落地质量是 Bad(对应设计文档里的"落地失败")就把已经转出来的分数清零，
/// 呼应"转得越多风险越高"——落地质量判定本身已经在 LandingDetector 里做好，这里只负责计分。
/// </summary>
public class TrickSystem : MonoBehaviour
{
    TrickSystemSettings.Tier[] tiers =
    {
        new TrickSystemSettings.Tier { minDegrees = 90f, score = 50 },
        new TrickSystemSettings.Tier { minDegrees = 180f, score = 100 },
        new TrickSystemSettings.Tier { minDegrees = 360f, score = 250 },
        new TrickSystemSettings.Tier { minDegrees = 540f, score = 500 },
        new TrickSystemSettings.Tier { minDegrees = 720f, score = 1000 },
    };

    BikeController bike;
    LandingDetector landingDetector;

    /// <summary>特技结算成功，score 是这次的得分，degrees 是实际转了多少度。</summary>
    public event Action<int, float> OnTrickScored;
    /// <summary>转出了够计分的角度，但落地质量太差，奖励归零；degrees 是本来能拿到的角度。</summary>
    public event Action<float> OnTrickFailed;

    public void Initialize(BikeController bikeController, LandingDetector detector)
    {
        bike = bikeController;
        landingDetector = detector;
        landingDetector.OnLanded += HandleLanded;
    }

    public void ApplySettings(TrickSystemSettings settings)
    {
        if (settings == null || settings.tiers == null || settings.tiers.Length == 0) return;
        tiers = settings.tiers;
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        float degrees = bike.SpinAccumulatedDegrees;
        int score = ScoreForDegrees(degrees);
        if (score <= 0) return; // 没转够最低档，压根没触发过特技

        if (quality == LandingDetector.Quality.NotBad)
        {
            OnTrickFailed?.Invoke(degrees);
            return;
        }

        OnTrickScored?.Invoke(score, degrees);
    }

    int ScoreForDegrees(float degrees)
    {
        int best = 0;
        foreach (TrickSystemSettings.Tier tier in tiers)
        {
            if (degrees >= tier.minDegrees && tier.score > best)
            {
                best = tier.score;
            }
        }
        return best;
    }

    void OnDestroy()
    {
        if (landingDetector != null) landingDetector.OnLanded -= HandleLanded;
    }
}
