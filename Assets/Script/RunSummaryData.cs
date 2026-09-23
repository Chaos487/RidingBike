/// <summary>
/// 单局摔车结算的数据快照——RunManager 在 EnterRunSummary() 里一次性收集好、打包成这个,
/// 交给 RunSummaryUI。RunSummaryUI 只管显示,不反过来读任何游戏系统,也不自己算 Total/破紀錄。
/// </summary>
public struct RunSummaryData
{
    /// <summary>本局跑了多远(米)。</summary>
    public float distance;
    /// <summary>本局 Trick System 累计得分(跟 Landing Quality 完全解耦,见 TrickSystem 注释)。</summary>
    public int trickScore;
    /// <summary>本局单次特技的最高得分(参考 Alto's Odyssey "best combo" 的副标题用法)。</summary>
    public int bestTrickScore;
    /// <summary>本局捡到的齿轮数量——货币已经在拾取那一刻实时发放/存盘了,这里只是用于展示的计数。</summary>
    public int gearsCollected;
    /// <summary>本局 Total Score,直接取 RunManager 现有的统一计分入口(Landing Quality + Trick + Near Miss)。</summary>
    public int totalScore;
    /// <summary>这局 Total Score 是否刷新了历史最高分。</summary>
    public bool isNewHighScore;
}
