using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Roguelike Node 系统的调度者,现在以"物理化 Station"的方式呈现(见 Station/Pit Stop 设计讨论):
/// Riding(正常骑行) → Approaching(预警+平滑减速) → Paused(真正停站、三选一) →
/// Exiting(平滑加速) → 回到 Riding。触发/安全区/Node 计数/暂停这些底层机制不变,只是把原来
/// "瞬间暂停"的呈现换成了这套减速进站/加速出站的过渡。
///
/// "当前该出什么档位"交给 DecisionCurve、"三选一具体是哪三个"交给 NodeChoicePool、
/// "选中的效果怎么生效"交给 NodeEffectSystem、"面板怎么显示/怎么点"交给 NodeChoiceUI——
/// 自己不重复实现这几块逻辑,只负责在正确的时机调用它们。
///
/// 安全区是反向查表:提前把"接下来这段 X 是安全区"登记进 safeZones,
/// EndlessTerrainGenerator(断层)和 ObstacleSpawner(障碍物)通过 overlapsSafeZone/isInSafeZone
/// 这两个委托反过来查——跟断层给 GapFallHandler 用的 TryGetGapAt 是同一类模式,但方向相反。
/// 安全区现在覆盖的是"预警减速开始→...→出站加速结束"这一整段,不只是暂停点前后一小段缓冲。
///
/// 下一个 Station 的安全区必须在当前这次一结束就立刻登记(不能等快到了再算),因为地形是提前
/// generateAheadDistance(默认 50m)生成好的——触发间隔减去安全区前段长度(= 进站减速距离)
/// 必须明显大于 50m,否则登记的时候那段地形可能已经生成过了,来不及避开。
/// </summary>
public class NodeManager : MonoBehaviour
{
    enum StationFlowState { Riding, Approaching, Paused, Exiting }

    float minNodeInterval = 110f;
    float maxNodeInterval = 140f;
    float safeZoneBefore = 30f;
    float safeZoneAfter = 30f;
    float stationSpeedKmh = 12f;
    float approachSlowdownDuration = 2.5f;
    float exitAccelerationDuration = 2f;
    float stationUiDelay = 0.15f;
    int midTierStartIndex = 3;
    int lateTierStartIndex = 6;
    List<ChoicePreset> choicePresets;

    BikeController bike;
    BikeDamageSystem damageSystem;
    RunManager runManager;
    StationMarkerSpawner markerSpawner;
    NodeChoiceUI ui;
    DecisionCurve decisionCurve;
    NodeChoicePool choicePool;

    readonly List<(float start, float end)> safeZones = new List<(float start, float end)>();
    List<ChoicePreset> currentChoices;
    Tweener speedCapTweener;

    StationFlowState flowState = StationFlowState.Riding;
    float nextTriggerX;
    float approachStartX;
    int nodeCount;
    bool started;
    bool ended;

    /// <summary>Station 三选一面板是否正开着(游戏已经因为这个原因暂停)。RunManager 通过
    /// isPausedByOtherSystem 反向查询这个值——玩家在三选一开着的时候依然可以打开/关闭骑行中的
    /// 暂停面板，但这种情况下 TogglePause() 不会真的去碰 Time.timeScale/BikeController.enabled
    /// (那两个已经被这里冻结了，且要冻结到玩家真正选完为止)，只切换暂停面板本身的显示状态，
    /// 见 RunManager.TogglePause 的注释。</summary>
    public bool IsStationPaused => flowState == StationFlowState.Paused;

    /// <summary>本局一共到达过几个 Station(不管玩家最后选了哪个选项)——ScoreSystem 结算时
    /// 用这个乘 ScoreSettings.scorePerNode 算分，RunManager 通过反向查询 Func 拿这个值，
    /// 跟 isPausedByOtherSystem 是同一个套路(RunManager 比 NodeManager 先创建，没法直接持有引用)。</summary>
    public int NodeCount => nodeCount;

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
        stationSpeedKmh = settings.stationSpeedKmh;
        approachSlowdownDuration = settings.approachSlowdownDuration;
        exitAccelerationDuration = settings.exitAccelerationDuration;
        stationUiDelay = settings.stationUiDelay;
        midTierStartIndex = settings.midTierStartIndex;
        lateTierStartIndex = settings.lateTierStartIndex;
        choicePresets = settings.choices;
    }

    public void Initialize(BikeController bikeController, BikeDamageSystem bikeDamageSystem, Transform canvasRoot,
        RunManager runManagerRef, StationMarkerSpawner stationMarkerSpawner)
    {
        bike = bikeController;
        damageSystem = bikeDamageSystem;
        runManager = runManagerRef;
        markerSpawner = stationMarkerSpawner;

        if (choicePresets == null || choicePresets.Count == 0) choicePresets = BuildFallbackChoices();

        decisionCurve = new DecisionCurve(midTierStartIndex, lateTierStartIndex);
        choicePool = new NodeChoicePool(choicePresets);

        SetupUI(canvasRoot);

        // 第一个 Station 的安全区必须在这里(EndlessRunBootstrap.Setup() 的同一帧、terrain 开始
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
    /// 还没点 Start 就可能被判定"已经骑到了第一个 Station"。</summary>
    public void BeginRun()
    {
        started = true;
    }

    /// <summary>BikeDamageSystem.OnFinalCrash 触发时调用——这一局已经结束,不用再触发新 Station,
    /// 也不用再管进站/出站的速度渐变了。</summary>
    public void HandleFinalCrash()
    {
        ended = true;
        speedCapTweener?.Kill();
    }

    void Update()
    {
        if (!started || ended) return;

        float x = bike.bikeRigidbody.position.x;

        switch (flowState)
        {
            case StationFlowState.Riding:
                if (x >= approachStartX) EnterApproaching();
                break;
            case StationFlowState.Approaching:
                if (x >= nextTriggerX) EnterStation();
                break;
            // Paused/Exiting 不需要逐帧检查触发——Paused 等玩家确认,Exiting 的速度渐变交给 DOTween。
        }

        safeZones.RemoveAll(z => z.end < x - 200f);
    }

    void ScheduleNextTrigger(float fromX)
    {
        float interval = UnityEngine.Random.Range(minNodeInterval, maxNodeInterval);
        nextTriggerX = fromX + interval;
        approachStartX = nextTriggerX - safeZoneBefore;
        safeZones.Add((nextTriggerX - safeZoneBefore, nextTriggerX + safeZoneAfter));

        if (markerSpawner != null) markerSpawner.RequestMarkerAt(nextTriggerX);
    }

    /// <summary>接近 Station:弹一次"即将进站"提示,车速开始平滑降到站内低速。这段路上禁止
    /// 跳跃/空翻/氮气——只让车平稳减速进站,不让玩家在快到站的时候搞事;安全区本身保证不会有
    /// 环境性的致命内容,禁跳跃纯粹是为了让"进站"这个动作看起来更稳、更有仪式感。</summary>
    void EnterApproaching()
    {
        flowState = StationFlowState.Approaching;
        bike.jumpAndBoostLocked = true;
        if (runManager != null) runManager.ShowStationApproachWarning();
        StartSpeedRamp(stationSpeedKmh, approachSlowdownDuration);
    }

    /// <summary>到达 Station:真正冻结游戏、弹三选一。停稳(禁用 BikeController、真正暂停)
    /// 和"面板出现"之间故意留一个 stationUiDelay 的间隔,不是一到站就硬切出菜单。</summary>
    void EnterStation()
    {
        flowState = StationFlowState.Paused;
        speedCapTweener?.Kill();
        bike.externalSpeedCapKmh = null; // 交还给暂停本身去"定住"车身,不需要临时限速再管

        Time.timeScale = 0f;
        bike.enabled = false;

        nodeCount++;
        NodeTier stage = decisionCurve.GetStage(nodeCount);
        currentChoices = choicePool.GenerateChoices(stage, 3);

        List<bool> lethalFlags = new List<bool>(currentChoices.Count);
        foreach (ChoicePreset choice in currentChoices)
        {
            lethalFlags.Add(NodeEffectSystem.WouldBeLethal(choice, damageSystem));
        }

        StartCoroutine(OpenPanelAfterDelay(lethalFlags));
    }

    IEnumerator OpenPanelAfterDelay(List<bool> lethalFlags)
    {
        // 暂停期间 Time.timeScale = 0,WaitForSeconds 会被同步冻结,必须用 Realtime 版本。
        if (stationUiDelay > 0f) yield return new WaitForSecondsRealtime(stationUiDelay);

        if (ui != null) ui.ShowChoices(currentChoices, lethalFlags);
    }

    void HandleConfirmed(int selectedIndex)
    {
        if (currentChoices == null || selectedIndex < 0 || selectedIndex >= currentChoices.Count) return;

        NodeEffectSystem.ApplyChoice(currentChoices[selectedIndex], bike, damageSystem);

        // ApplyChoice 可能通过 ModifyMaxHp 直接把玩家扣死——damageSystem.OnFinalCrash 是同步
        // 触发的,RunManager/CameraDirector 这时候已经跑完摔车结算(锁 BikeController、接管镜头、
        // 显示结算画面),HandleFinalCrash 也已经把 ended 置 true。这种情况绝不能再走正常的
        // "出站加速"流程,不然等于把已经结束的一局又救活,车会继续往前跑。
        if (ended)
        {
            if (ui != null) ui.Hide();
            // timeScale 仍然要恢复(暂停时压到了 0),不然摔车结算的镜头缓动/物理表现会跟着一起
            // 冻结，效果跟正常摔车(此时 timeScale 本来就是 1)不一致。
            Time.timeScale = 1f;
            return;
        }

        StartExiting();
    }

    /// <summary>离站:关面板、恢复暂停,车速从站内低速平滑加速回(刚生效的新)正常封顶——
    /// 车速目标用 bike.maxSpeedKmh 而不是缓存的旧值,这样选中的车速类 Effect 会立刻反映在
    /// 这次加速的终点上。</summary>
    void StartExiting()
    {
        if (ui != null) ui.Hide();

        flowState = StationFlowState.Exiting;
        Time.timeScale = 1f;
        bike.enabled = true;
        bike.jumpAndBoostLocked = false; // 出站开始就还给玩家控制,禁跳跃只针对"进站"这一段
        bike.externalSpeedCapKmh = stationSpeedKmh;

        StartSpeedRamp(bike.maxSpeedKmh, exitAccelerationDuration, FinishExiting);
    }

    void FinishExiting()
    {
        bike.externalSpeedCapKmh = null;
        flowState = StationFlowState.Riding;

        // 从这个 Station 自己的触发点(而不是出站加速跑完之后的当前车身位置)往后排——
        // 意图更清楚:下一个 Station 是接着这一个排的,不依赖出站加速具体跑了多远。
        ScheduleNextTrigger(nextTriggerX);
    }

    void StartSpeedRamp(float targetKmh, float duration, TweenCallback onComplete = null)
    {
        speedCapTweener?.Kill();

        float from = bike.externalSpeedCapKmh ?? bike.maxSpeedKmh;
        bike.externalSpeedCapKmh = from;

        speedCapTweener = DOTween.To(() => bike.externalSpeedCapKmh.Value, v => bike.externalSpeedCapKmh = v, targetKmh, duration)
            .SetEase(Ease.InOutSine);
        if (onComplete != null) speedCapTweener.OnComplete(onComplete);
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
                title = "Light Load",
                description = "Jump Force +3",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.JumpForceFlat, value = 3f } },
            },
            new ChoicePreset
            {
                title = "Engine Boost",
                description = "Max Speed +8%",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.MaxSpeedPercent, value = 8f } },
            },
            new ChoicePreset
            {
                title = "Fast Refuel",
                description = "Boost Recharge -20%",
                minStage = NodeTier.Early,
                riskLevel = RiskLevel.Safe,
                weight = 10f,
                effects = { new EffectEntry { type = EffectType.BoostRechargePercent, value = 20f } },
            },
        };
    }
}
