/// <summary>
/// 单局摔车结算的数据快照——RunManager 在 EnterRunSummary() 里调 ScoreSystem.BuildSummary()
/// 一次性收集好、打包成这个,交给 RunSummaryUI。RunSummaryUI 只管显示,不反过来读任何游戏
/// 系统,也不自己算 Total/破紀錄。每一项都带对应的"这一项算了多少分"，因为结算面板现在是
/// 一张统一列表——每一行右侧显示的都是分数，全部加起来正好等于 totalScore(见 ScoreSystem)。
/// </summary>
public struct RunSummaryData
{
    /// <summary>本局跑了多远(米)。</summary>
    public float distance;
    /// <summary>Distance 换算出来的分数(distance × ScoreSettings.scorePerMeter)。</summary>
    public int distanceScore;
    /// <summary>本局距离是否刷新了历史最远距离纪录——跟 isNewHighScore(Total Score 破紀錄)
    /// 是两套独立的记录，可能同时触发，也可能只触发一个。</summary>
    public bool isNewDistanceRecord;
    /// <summary>破距离纪录额外拿到的分数(ScoreSettings.newDistanceRecordBonus)，没破紀錄时是 0。</summary>
    public int distanceRecordBonus;

    /// <summary>本局 Trick System 累计得分(跟 Landing Quality 完全解耦,见 TrickSystem 注释)。</summary>
    public int trickScore;
    /// <summary>本局单次特技的最高得分(参考 Alto's Odyssey "best combo" 的副标题用法)。</summary>
    public int bestTrickScore;

    /// <summary>本局 Landing Quality(Perfect/Good/Not Bad 落地)累计得分。</summary>
    public int landingQualityScore;
    /// <summary>本局 Near Miss(贴身擦过障碍物)累计得分。</summary>
    public int nearMissScore;

    /// <summary>本局捡到的齿轮数量——货币已经在拾取那一刻实时发放/存盘了,这里只是计数。</summary>
    public int gearsCollected;
    /// <summary>Gears 换算出来的分数(gearsCollected × ScoreSettings.scorePerGear)。</summary>
    public int gearScore;

    /// <summary>本局经过了几个 Roguelike Station(不管选了哪个选项都算)。</summary>
    public int nodeCount;
    /// <summary>Node 换算出来的分数(nodeCount × ScoreSettings.scorePerNode)。</summary>
    public int nodeScore;

    /// <summary>结算那一刻的血量上限(不是剩余血量——摔车判定本身就是血量归零那一刻触发的，
    /// 剩余血量永远是 0；这个值会被 Node 选项加成/削弱)。</summary>
    public float finalMaxHp;
    /// <summary>Max HP 换算出来的分数(finalMaxHp × ScoreSettings.scorePerMaxHpPoint)。</summary>
    public int maxHpScore;

    /// <summary>本局 Total Score——ScoreSystem 里所有分类累计值的加总，也正好等于结算面板
    /// 上所有行的加总。</summary>
    public int totalScore;
    /// <summary>这局 Total Score 是否刷新了历史最高分。</summary>
    public bool isNewHighScore;
}
