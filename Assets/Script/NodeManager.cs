using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roguelike Node 系统:骑行到达触发点 -> 暂停(Time.timeScale = 0) -> 弹出三选一 -> 选中 -> 确认
/// -> 生效 -> 恢复。跟 RunManager.EnterStartGate 是同一套"挂起 BikeController、真正暂停"的思路。
///
/// 安全区是反向查表:这里提前把"接下来这段 X 是安全区"登记进 safeZones,EndlessTerrainGenerator
/// (断层)和 ObstacleSpawner(障碍物)在各自的生成逻辑里通过 overlapsSafeZone/isInSafeZone
/// 这两个委托反过来查——跟断层给 GapFallHandler 用的 TryGetGapAt 是同一类模式,但方向相反。
///
/// 下一个 Node 的安全区必须在当前 Node 一结束就立刻登记(不能等快到了再算),因为地形是提前
/// generateAheadDistance(默认 50m)生成好的——触发间隔(默认 80~100m)减去安全区前段长度(默认 15m)
/// 必须明显大于 50m,否则登记的时候那段地形可能已经生成过了,来不及避开。
///
/// Choice 类型第一版收窄成白名单(EffectType):不做通用效果引擎,直接改 BikeController/
/// BikeDamageSystem 的现有数值字段。Decision Curve 按"第几个 Node"计数,不按骑行距离。
/// </summary>
public class NodeManager : MonoBehaviour
{
    float minNodeInterval = 80f;
    float maxNodeInterval = 100f;
    float safeZoneBefore = 15f;
    float safeZoneAfter = 15f;
    int midTierStartIndex = 3;
    int lateTierStartIndex = 6;
    List<ChoicePreset> choicePool;

    BikeController bike;
    BikeDamageSystem damageSystem;

    Transform nodePanel;
    readonly Image[] choiceCardImages = new Image[3];
    readonly Button[] choiceButtons = new Button[3];
    readonly Text[] choiceTitleTexts = new Text[3];
    readonly Text[] choiceDescTexts = new Text[3];
    GameObject confirmButtonObject;
    Button confirmButton;

    static readonly Color CardNormalColor = new Color(0.15f, 0.15f, 0.18f, 0.92f);
    static readonly Color CardSelectedColor = new Color(0.25f, 0.45f, 0.3f, 0.95f);

    readonly List<(float start, float end)> safeZones = new List<(float start, float end)>();
    readonly List<ChoicePreset> currentChoices = new List<ChoicePreset>(3);

    float nextTriggerX;
    int nodeCount;
    int selectedIndex = -1;
    bool started;
    bool ended;
    bool nodePanelOpen;

    public void ApplySettings(NodeSettings settings)
    {
        if (settings == null)
        {
            choicePool = null; // NodeSettings.BuildDefaultChoices 是私有的，没有资产就退回到脚本内建的最小兜底池
            return;
        }

        minNodeInterval = settings.minNodeInterval;
        maxNodeInterval = settings.maxNodeInterval;
        safeZoneBefore = settings.safeZoneBefore;
        safeZoneAfter = settings.safeZoneAfter;
        midTierStartIndex = settings.midTierStartIndex;
        lateTierStartIndex = settings.lateTierStartIndex;
        choicePool = settings.choices;
    }

    public void Initialize(BikeController bikeController, BikeDamageSystem bikeDamageSystem, Transform canvasRoot)
    {
        bike = bikeController;
        damageSystem = bikeDamageSystem;

        if (choicePool == null || choicePool.Count == 0) choicePool = BuildFallbackChoices();

        FindUIReferences(canvasRoot);

        // 第一个 Node 的安全区必须在这里(EndlessRunBootstrap.Setup() 的同一帧、terrain 开始
        // Update() 之前)就登记好,见类注释。
        ScheduleNextTrigger(bike.bikeRigidbody.position.x);
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

        NodeTier tier = GetTier(nodeCount);
        PickChoices(tier, 3, currentChoices);
        ShowPanel();

        Time.timeScale = 0f;
        bike.enabled = false;
    }

    NodeTier GetTier(int index)
    {
        if (index >= lateTierStartIndex) return NodeTier.Late;
        if (index >= midTierStartIndex) return NodeTier.Mid;
        return NodeTier.Early;
    }

    void PickChoices(NodeTier tier, int count, List<ChoicePreset> result)
    {
        List<ChoicePreset> pool = choicePool.FindAll(c => c.tier == tier);
        if (pool.Count < count) pool = new List<ChoicePreset>(choicePool);
        Shuffle(pool);

        result.Clear();
        int n = Mathf.Min(count, pool.Count);
        for (int i = 0; i < n; i++) result.Add(pool[i]);
    }

    static void Shuffle(List<ChoicePreset> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void ShowPanel()
    {
        selectedIndex = -1;
        if (confirmButtonObject != null) confirmButtonObject.SetActive(false);

        for (int i = 0; i < 3; i++)
        {
            bool hasChoice = i < currentChoices.Count;
            if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(hasChoice);
            if (!hasChoice) continue;

            if (choiceTitleTexts[i] != null) choiceTitleTexts[i].text = currentChoices[i].title;
            if (choiceDescTexts[i] != null) choiceDescTexts[i].text = currentChoices[i].description;
            if (choiceCardImages[i] != null) choiceCardImages[i].color = CardNormalColor;
        }

        if (nodePanel != null) nodePanel.gameObject.SetActive(true);
    }

    void HandleChoiceClicked(int index)
    {
        if (index < 0 || index >= currentChoices.Count) return;

        selectedIndex = index;
        for (int i = 0; i < 3; i++)
        {
            if (choiceCardImages[i] != null) choiceCardImages[i].color = i == index ? CardSelectedColor : CardNormalColor;
        }

        if (confirmButtonObject != null) confirmButtonObject.SetActive(true);
    }

    void HandleConfirmClicked()
    {
        if (selectedIndex < 0 || selectedIndex >= currentChoices.Count) return;

        ApplyChoice(currentChoices[selectedIndex]);
        ClosePanel();
    }

    void ApplyChoice(ChoicePreset choice)
    {
        foreach (EffectEntry effect in choice.effects)
        {
            ApplyEffect(effect);
        }
    }

    void ApplyEffect(EffectEntry effect)
    {
        switch (effect.type)
        {
            case EffectType.MaxSpeedPercent:
                bike.maxSpeedKmh *= 1f + effect.value / 100f;
                break;
            case EffectType.JumpForceFlat:
                bike.jumpForce += effect.value;
                break;
            case EffectType.MaxHpFlat:
                damageSystem.ModifyMaxHp(effect.value);
                break;
            case EffectType.AccelerationPercent:
                bike.cruiseMotorAcceleration *= 1f + effect.value / 100f;
                bike.cruiseTorque *= 1f + effect.value / 100f;
                break;
            case EffectType.BoostRechargePercent:
                bike.boostRechargeDistance = Mathf.Max(20f, bike.boostRechargeDistance * (1f - effect.value / 100f));
                break;
        }
    }

    void ClosePanel()
    {
        if (nodePanel != null) nodePanel.gameObject.SetActive(false);
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
                tier = NodeTier.Early,
                effects = { new EffectEntry { type = EffectType.JumpForceFlat, value = 3f } },
            },
            new ChoicePreset
            {
                title = "强化引擎",
                description = "车速上限 +8%",
                tier = NodeTier.Early,
                effects = { new EffectEntry { type = EffectType.MaxSpeedPercent, value = 8f } },
            },
            new ChoicePreset
            {
                title = "快速补给",
                description = "氮气回能距离 -20%",
                tier = NodeTier.Early,
                effects = { new EffectEntry { type = EffectType.BoostRechargePercent, value = 20f } },
            },
        };
    }

    void FindUIReferences(Transform canvasRoot)
    {
        if (canvasRoot == null) return;

        Transform panel = canvasRoot.Find("NodePanel");
        if (panel == null)
        {
            Debug.LogError("NodeManager: 在 UI 预制体里找不到 \"NodePanel\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return;
        }
        nodePanel = panel;
        nodePanel.gameObject.SetActive(false);

        for (int i = 0; i < 3; i++)
        {
            Transform card = panel.Find($"Choice{i}");
            if (card == null)
            {
                Debug.LogError($"NodeManager: 在 UI 预制体里找不到 \"Choice{i}\"。");
                continue;
            }

            int captured = i;
            choiceCardImages[i] = card.GetComponent<Image>();
            choiceButtons[i] = card.GetComponent<Button>();
            if (choiceButtons[i] != null) choiceButtons[i].onClick.AddListener(() => HandleChoiceClicked(captured));

            Transform title = card.Find("TitleText");
            choiceTitleTexts[i] = title != null ? title.GetComponent<Text>() : null;

            Transform desc = card.Find("DescriptionText");
            choiceDescTexts[i] = desc != null ? desc.GetComponent<Text>() : null;
        }

        Transform confirm = panel.Find("ConfirmButton");
        if (confirm == null)
        {
            Debug.LogError("NodeManager: 在 UI 预制体里找不到 \"ConfirmButton\"。");
            return;
        }
        confirmButtonObject = confirm.gameObject;
        confirmButton = confirm.GetComponent<Button>();
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
        confirmButtonObject.SetActive(false);
    }
}
