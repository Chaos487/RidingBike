using System;
using UnityEngine;

/// <summary>
/// 唯一持有 Total(Score)和每个分类累计值的地方——从 RunManager 里拆出来的,原因是
/// RunManager 已经身兼 HUD/开始 gate/暂停/摔车结算好几摊事,现在又要接 Distance/Gears/
/// Node/Max HP 这几个新的计分来源,继续堆在 RunManager 上会让它更难看懂。
///
/// 分两类事件源:
/// - Landing Quality / Trick / Near Miss 是跑动过程中的离散事件,发生的瞬间只广播"这次值
///   多少分"(OnLandingScored/OnTrickScored/OnNearMissScored),**不立刻**计入 Total——
///   真正落进 Total 的时机交给 RunManager 的右侧 Feat 列表(气泡淡出那一刻才调
///   CommitScore),这个"淡出才计分"的反馈节奏完全没变,只是"这次该给多少分"这个判断
///   从 RunManager 挪到了这里,统一查 ScoreSettings。
/// - Distance / Gears / Node / Max HP 是结算那一刻才算的"连续数值",没有对应的 Feat 弹幕,
///   BuildSummary() 里直接算完就通过 CommitScore 计入 Total。
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    static readonly int[] DefaultScorePerLap = { 50, 150, 300, 500, 750 };

    ScoreSettings settings;

    LandingDetector landingDetector;
    TrickSystem trickSystem;
    ObstacleSpawner obstacleSpawner;

    int score;
    int landingQualityTotalScore;
    int trickTotalScore;
    int bestTrickScore;
    int nearMissTotalScore;

    public int CurrentScore => score;

    /// <summary>Total 变化后触发,参数是变化后的新值——RunManager 用来刷新右上角 ScoreText。</summary>
    public event Action<int> OnScoreChanged;
    /// <summary>Landing Quality 事件发生那一刻广播(展示用的文案, 这次值多少分),还没计入 Total。</summary>
    public event Action<string, int> OnLandingScored;
    /// <summary>Trick 事件发生那一刻广播(这次值多少分, 转了几圈),还没计入 Total。</summary>
    public event Action<int, int> OnTrickScored;
    /// <summary>Near Miss 事件发生那一刻广播(这次值多少分),还没计入 Total。</summary>
    public event Action<int> OnNearMissScored;

    public void ApplySettings(ScoreSettings scoreSettings)
    {
        if (scoreSettings != null) settings = scoreSettings;
    }

    public void Initialize(LandingDetector landing, TrickSystem trick, ObstacleSpawner obstacles)
    {
        landingDetector = landing;
        trickSystem = trick;
        obstacleSpawner = obstacles;

        landingDetector.OnLanded += HandleLanded;
        trickSystem.OnTrickCompleted += HandleTrickCompleted;
        obstacleSpawner.OnNearMiss += HandleNearMiss;
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        int points = LandingQualityScore(quality);
        landingQualityTotalScore += points;
        OnLandingScored?.Invoke(LandingQualityLabel(quality), points);
    }

    static string LandingQualityLabel(LandingDetector.Quality quality) => quality switch
    {
        LandingDetector.Quality.Perfect => "PERFECT!",
        LandingDetector.Quality.Good => "GOOD",
        _ => "NOT BAD",
    };

    int LandingQualityScore(LandingDetector.Quality quality) => quality switch
    {
        LandingDetector.Quality.Perfect => settings != null ? settings.perfectLandingScore : 20,
        LandingDetector.Quality.Good => settings != null ? settings.goodLandingScore : 10,
        _ => settings != null ? settings.notBadLandingScore : 0,
    };

    void HandleTrickCompleted(int laps)
    {
        int points = ScoreForLaps(laps);
        trickTotalScore += points;
        if (points > bestTrickScore) bestTrickScore = points;
        OnTrickScored?.Invoke(points, laps);
    }

    int ScoreForLaps(int laps)
    {
        int[] table = settings != null && settings.scorePerLap != null && settings.scorePerLap.Length > 0
            ? settings.scorePerLap
            : DefaultScorePerLap;
        // 超过表里配置的最高圈数，直接沿用最后一档（最高分），不会数组越界。
        int index = Mathf.Clamp(laps, 1, table.Length) - 1;
        return table[index];
    }

    void HandleNearMiss()
    {
        int points = settings != null ? settings.nearMissScore : 15;
        nearMissTotalScore += points;
        OnNearMissScored?.Invoke(points);
    }

    /// <summary>真正把一笔分数计入 Total——Landing/Trick/Near Miss 由 RunManager 的 Feat
    /// 列表在气泡淡出那一刻调用；BuildSummary() 里的几项自己直接调用，不经过 Feat 列表。</summary>
    public void CommitScore(int amount)
    {
        score += amount;
        OnScoreChanged?.Invoke(score);
    }

    /// <summary>结算那一刻一次性算完 Distance/Gears/Node/Max HP 这几项换算出来的分数并计入
    /// Total，打包成完整的 RunSummaryData 交还给 RunManager。isNewHighScore 不在这里判断——
    /// 要等这里全部加完才知道最终 Total 是多少，RunManager 拿到返回值之后自己再补上。</summary>
    public RunSummaryData BuildSummary(float distance, int gearsCollected, int nodeCount, float finalMaxHp, bool isNewDistanceRecord)
    {
        int distanceScore = Mathf.RoundToInt(distance * (settings != null ? settings.scorePerMeter : 1f));
        int gearScore = gearsCollected * (settings != null ? settings.scorePerGear : 5);
        int nodeScore = nodeCount * (settings != null ? settings.scorePerNode : 50);
        int maxHpScore = Mathf.RoundToInt(finalMaxHp * (settings != null ? settings.scorePerMaxHpPoint : 2f));
        int distanceRecordBonus = isNewDistanceRecord ? (settings != null ? settings.newDistanceRecordBonus : 500) : 0;

        CommitScore(distanceScore + gearScore + nodeScore + maxHpScore + distanceRecordBonus);

        return new RunSummaryData
        {
            distance = distance,
            distanceScore = distanceScore,
            isNewDistanceRecord = isNewDistanceRecord,
            distanceRecordBonus = distanceRecordBonus,
            trickScore = trickTotalScore,
            bestTrickScore = bestTrickScore,
            landingQualityScore = landingQualityTotalScore,
            nearMissScore = nearMissTotalScore,
            gearsCollected = gearsCollected,
            gearScore = gearScore,
            nodeCount = nodeCount,
            nodeScore = nodeScore,
            finalMaxHp = finalMaxHp,
            maxHpScore = maxHpScore,
            totalScore = score,
        };
    }

    void OnDestroy()
    {
        if (landingDetector != null) landingDetector.OnLanded -= HandleLanded;
        if (trickSystem != null) trickSystem.OnTrickCompleted -= HandleTrickCompleted;
        if (obstacleSpawner != null) obstacleSpawner.OnNearMiss -= HandleNearMiss;
    }
}
