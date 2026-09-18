using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roguelike Node 系统的调度者:检测触发、算 Node 计数、管理安全区、暂停/恢复,
/// 把"当前该出什么档位"交给 DecisionCurve、"三选一具体是哪三个"交给 NodeChoicePool、
/// "选中的效果怎么生效"交给 NodeEffectSystem、"面板怎么显示/怎么点"交给 NodeChoiceUI——
/// 自己不重复实现这几块逻辑,只负责在正确的时机调用它们(架构见 GitHub #3 存档设计 + 后续讨论)。
///
/// 安全区是反向查表:提前把"接下来这段 X 是安全区"登记进 safeZones,
/// EndlessTerrainGenerator(断层)和 ObstacleSpawner(障碍物)通过 overlapsSafeZone/isInSafeZone
/// 这两个委托反过来查——跟断层给 GapFallHandler 用的 TryGetGapAt 是同一类模式,但方向相反。
///
/// 下一个 Node 的安全区必须在当前 Node 一结束就立刻登记(不能等快到了再算),因为地形是提前
/// generateAheadDistance(默认 50m)生成好的——触发间隔(默认 80~100m)减去安全区前段长度(默认 15m)
/// 必须明显大于 50m,否则登记的时候那段地形可能已经生成过了,来不及避开。
/// </summary>
public class NodeManager : MonoBehaviour
{
    float minNodeInterval = 80f;
    float maxNodeInterval = 100f;
    float safeZoneBefore = 15f;
    float safeZoneAfter = 15f;
    int midTierStartIndex = 3;
    int lateTierStartIndex = 6;
    List<ChoicePreset> choicePresets;

    BikeController bike;
    BikeDamageSystem damageSystem;
    NodeChoiceUI ui;
    DecisionCurve decisionCurve;
    NodeChoicePool choicePool;

    readonly List<(float start, float end)> safeZones = new List<(float start, float end)>();
    List<ChoicePreset> currentChoices;

    float nextTriggerX;
    int nodeCount;
    bool started;
    bool ended;
    bool nodePanelOpen;

    public void ApplySettings(NodeSettings settings)
    {
        if (settings == null)
        {
            choicePresets = null; // Initialize() 里退回内置的最小兜底池
            return;
        }

        minNodeInterval = settings.minNodeInterval;
        maxNodeInterval = settings.maxNodeInterval;
        safeZoneBefore = settings.safeZoneBefore;
        safeZoneAfter = settings.safeZoneAfter;
        midTierStartIndex = settings.midTierStartIndex;
        lateTierStartIndex = settings.lateTierStartIndex;
        choicePresets = settings.choices;
    }

    public void Initialize(BikeController bikeController, BikeDamageSystem bikeDamageSystem, Transform canvasRoot)
    {
        bike = bikeController;
        damageSystem = bikeDamageSystem;

        if (choicePresets == null || choicePresets.Count == 0) choicePresets = BuildFallbackChoices();

        decisionCurve = new DecisionCurve(midTierStartIndex, lateTierStartIndex);
        choicePool = new NodeChoicePool(choicePresets);

        SetupUI(canvasRoot);

        // 第一个 Node 的安全区必须在这里(EndlessRunBootstrap.Setup() 的同一帧、terrain 开始
        // Update() 之前)就登记好,见类注释。
        ScheduleNextTrigger(bike.bikeRigidbody.position.x);
    }

    void SetupUI(Transform canvasRoot)
    {
        if (canvasRoot == null) return;

        Transform panel = canvasRoot.Find("NodePanel");
        if (panel == null)
        {
            Debug.LogError("NodeManager: 在 UI 预制体里找不到 \"NodePanel\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return;
        }

        ui = panel.gameObject.AddComponent<NodeChoiceUI>();
        ui.Initialize();
        ui.OnConfirmed += HandleConfirmed;
    }

    /// <summary>RunManager.OnGameStarted 触发时调用——开始界面静止期间不检查触发,不然玩家
    /// 还没点 Start 就可能被判定"已经骑到了第一个 Node"。</summary>
    public void BeginRun()
    {
        started = true;
    }

    /// <summary>BikeDamageSystem.OnFinalCrash 触发时调用——这一局已经结束,不用再触发新 Node。</summary>
    public void HandleFinalCrash()
    {
        ended = true;
    }

    void Update()
    {
        if (!started || ended || nodePanelOpen) return;

        float x = bike.bikeRigidbody.position.x;
        if (x >= nextTriggerX) TriggerNode();

        safeZones.RemoveAll(z => z.end < x - 200f);
    }

    void ScheduleNextTrigger(float fromX)
    {
        float interval = UnityEngine.Random.Range(minNodeInterval, maxNodeInterval);
        nextTriggerX = fromX + interval;
        safeZones.Add((nextTriggerX - safeZoneBefore, nextTriggerX + safeZoneAfter));
    }

    void TriggerNode()
    {
        nodePanelOpen = true;
        nodeCount++;

        NodeTier stage = decisionCurve.GetStage(nodeCount);
        currentChoices = choicePool.GenerateChoices(stage, 3);

        if (ui != null) ui.ShowChoices(currentChoices);

        Time.timeScale = 0f;
        bike.enabled = false;
    }

    void HandleConfirmed(int selectedIndex)
    {
        if (currentChoices == null || selectedIndex < 0 || selectedIndex >= currentChoices.Count) return;

        NodeEffectSystem.ApplyChoice(currentChoices[selectedIndex], bike, damageSystem);
        ClosePanel();
    }

    void ClosePanel()
    {
        if (ui != null) ui.Hide();
        nodePanelOpen = false;

        Time.timeScale = 1f;
        bike.enabled = true;

        // 从这个 Node 自己的触发点(而不是当前帧的车身位置)往后排——暂停期间车身没有移动,
        // 两者数值上一样,但这样写意图更清楚:下一个安全区是接着这一个排的,不依赖物理有没有在暂停期间漂移。
        ScheduleNextTrigger(nextTriggerX);
    }

    /// <summary>[rangeStart, rangeEnd] 是否跟任意一个已登记的安全区有重叠。供 EndlessTerrainGenerator
    /// 生成断层前反向查询。</summary>
    public bool OverlapsSafeZone(float rangeStart, float rangeEnd)
    {
        foreach ((float start, float end) zone in safeZones)
        {
            if (rangeEnd >= zone.start && rangeStart <= zone.end) return true;
        }
        return false;
    }

    /// <summary>单点是否落在安全区内。供 ObstacleSpawner 生成障碍物前反向查询。</summary>
    public bool IsInSafeZone(float x) => OverlapsSafeZone(x, x);

    static List<ChoicePreset> BuildFallbackChoices()
    {
        return new List<ChoicePreset>
        {
            new ChoicePreset
            {
                title = "轻装上阵",
                description = "跳跃力度 +3",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.JumpForceFlat, value = 3f } },
            },
            new ChoicePreset
            {
                title = "强化引擎",
                description = "车速上限 +8%",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.MaxSpeedPercent, value = 8f } },
            },
            new ChoicePreset
            {
                title = "快速补给",
                description = "氮气回能距离 -20%",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.BoostRechargePercent, value = 20f } },
            },
        };
    }
}
