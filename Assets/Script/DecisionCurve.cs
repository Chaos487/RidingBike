/// <summary>
/// 把"第几个 Node"换算成 Decision Curve 的当前档位(Early/Mid/Late)。
/// 故意做成一个不依赖 MonoBehaviour 的纯逻辑类——只认 Node 计数,不用管 Unity 生命周期,
/// 单独测/单独调都不需要起场景。
/// </summary>
public class DecisionCurve
{
    readonly int midTierStartIndex;
    readonly int lateTierStartIndex;

    public DecisionCurve(int midTierStartIndex, int lateTierStartIndex)
    {
        this.midTierStartIndex = midTierStartIndex;
        this.lateTierStartIndex = lateTierStartIndex;
    }

    /// <summary>nodeIndex 从 1 开始计数(第一个 Node 是 1)。</summary>
    public NodeTier GetStage(int nodeIndex)
    {
        if (nodeIndex >= lateTierStartIndex) return NodeTier.Late;
        if (nodeIndex >= midTierStartIndex) return NodeTier.Mid;
        return NodeTier.Early;
    }
}
