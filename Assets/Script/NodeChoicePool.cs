using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按 Min Stage 过滤 + Weight 加权 + 风险光谱,从总 Choice 池里生成呈现给玩家的三选一。
/// 不依赖 MonoBehaviour——纯数据进、纯数据出,方便单独复用/测试。
/// </summary>
public class NodeChoicePool
{
    // 三档风险光谱:低(Safe/Low 合并成一档) / 中 / 高——生成三选一时尽量让三个选项分别落在
    // 这三档里,不是纯随机抽奖,保证玩家每次面对的是一个有意义的风险决策。某一档在当前
    // eligible 池里为空就跳过,不强求凑数。
    static readonly RiskLevel[][] RiskBands =
    {
        new[] { RiskLevel.Safe, RiskLevel.Low },
        new[] { RiskLevel.Medium },
        new[] { RiskLevel.High },
    };

    readonly List<ChoicePreset> allChoices;

    public NodeChoicePool(List<ChoicePreset> allChoices)
    {
        this.allChoices = allChoices ?? new List<ChoicePreset>();
    }

    /// <summary>生成最多 count 个不重复的 Choice。池子本身凑不够 count 个就照实给,不硬凑。</summary>
    public List<ChoicePreset> GenerateChoices(NodeTier stage, int count)
    {
        List<ChoicePreset> eligible = FilterByStage(stage);
        List<ChoicePreset> result = new List<ChoicePreset>(count);
        HashSet<ChoicePreset> used = new HashSet<ChoicePreset>();

        foreach (RiskLevel[] band in RiskBands)
        {
            if (result.Count >= count) break;

            ChoicePreset picked = WeightedPick(FindEligibleInBand(eligible, used, band));
            if (picked == null) continue;

            result.Add(picked);
            used.Add(picked);
        }

        // 风险分档没凑够 count 个(某几档为空,或池子本身太小)——从剩下没用过的可用池里按权重补齐。
        while (result.Count < count)
        {
            ChoicePreset picked = WeightedPick(FindRemaining(eligible, used));
            if (picked == null) break;

            result.Add(picked);
            used.Add(picked);
        }

        return result;
    }

    List<ChoicePreset> FilterByStage(NodeTier stage)
    {
        // Min Stage 是累积可用:Early 的 Choice 到 Mid/Late 依然可能出现,不是只在对应档才出现。
        return allChoices.FindAll(c => (int)c.minStage <= (int)stage);
    }

    static List<ChoicePreset> FindEligibleInBand(List<ChoicePreset> eligible, HashSet<ChoicePreset> used, RiskLevel[] band)
    {
        return eligible.FindAll(c => !used.Contains(c) && System.Array.IndexOf(band, c.riskLevel) >= 0);
    }

    static List<ChoicePreset> FindRemaining(List<ChoicePreset> eligible, HashSet<ChoicePreset> used)
    {
        return eligible.FindAll(c => !used.Contains(c));
    }

    static ChoicePreset WeightedPick(List<ChoicePreset> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;

        float totalWeight = 0f;
        foreach (ChoicePreset c in candidates) totalWeight += Mathf.Max(0f, c.weight);

        // 权重全是 0/负数(配置错误)时退回等概率兜底,不直接崩掉。
        if (totalWeight <= 0f) return candidates[Random.Range(0, candidates.Count)];

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (ChoicePreset c in candidates)
        {
            cumulative += Mathf.Max(0f, c.weight);
            if (roll <= cumulative) return c;
        }

        return candidates[candidates.Count - 1]; // 浮点误差兜底
    }
}
