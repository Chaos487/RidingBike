using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Node 三选一面板的 UI 交互——只管"显示三个选项、选中高亮、Confirm 按钮显隐、点确认时通知外部
/// 选了第几个",不知道 Effect/Pool/DecisionCurve 这些游戏逻辑,由 NodeManager 在收到 OnConfirmed
/// 之后自己去处理生效。
///
/// 运行时挂到 EndlessRunCanvas.prefab 里的 NodePanel 节点上(NodeManager.Initialize 里
/// AddComponent 出来的,跟项目里其他系统的接线方式一样,不需要预先在 prefab 里挂这个脚本)。
/// NodePanel 默认是隐藏状态(inactive),所以查找子物体引用这一步故意不放在 Awake() 里——
/// AddComponent 在一个已经 inactive 的物体上时 Awake 的调用时机不完全可靠,改成显式 Initialize()
/// 由 NodeManager 在 AddComponent 之后立刻调用,不依赖 Unity 生命周期的隐含时机。
/// </summary>
public class NodeChoiceUI : MonoBehaviour
{
    static readonly Color CardNormalColor = new Color(0.15f, 0.15f, 0.18f, 0.92f);
    static readonly Color CardSelectedColor = new Color(0.25f, 0.45f, 0.3f, 0.95f);
    // 会把玩家直接扣死的选项标红——即使选中也保持红色(用更亮的红区分"选中"),不套用普通的
    // 绿色选中态,不然玩家看不出这个选项本来就是危险的。
    static readonly Color CardLethalColor = new Color(0.5f, 0.12f, 0.12f, 0.92f);
    static readonly Color CardLethalSelectedColor = new Color(0.75f, 0.18f, 0.18f, 0.95f);

    readonly Image[] cardImages = new Image[3];
    readonly Button[] cardButtons = new Button[3];
    readonly Text[] titleTexts = new Text[3];
    readonly Text[] descTexts = new Text[3];
    readonly bool[] cardLethal = new bool[3];
    GameObject confirmButtonObject;
    Button confirmButton;

    int choiceCount;
    int selectedIndex = -1;

    /// <summary>玩家点了 Confirm 时触发,参数是选中的卡片序号(0/1/2)。</summary>
    public event Action<int> OnConfirmed;

    public void Initialize()
    {
        FindReferences();
        gameObject.SetActive(false);
    }

    /// <summary>lethalFlags[i] = true 表示选了 choices[i] 会直接把玩家扣死——卡片会标红提醒,
    /// 但依然可以选(不禁用按钮),玩家可以是故意的。</summary>
    public void ShowChoices(IReadOnlyList<ChoicePreset> choices, IReadOnlyList<bool> lethalFlags)
    {
        selectedIndex = -1;
        choiceCount = choices.Count;
        if (confirmButtonObject != null) confirmButtonObject.SetActive(false);

        for (int i = 0; i < 3; i++)
        {
            bool hasChoice = i < choices.Count;
            if (cardButtons[i] != null) cardButtons[i].gameObject.SetActive(hasChoice);
            if (!hasChoice) continue;

            if (titleTexts[i] != null) titleTexts[i].text = choices[i].title;
            if (descTexts[i] != null) descTexts[i].text = choices[i].description;
            cardLethal[i] = lethalFlags != null && i < lethalFlags.Count && lethalFlags[i];
            if (cardImages[i] != null) cardImages[i].color = cardLethal[i] ? CardLethalColor : CardNormalColor;
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void HandleCardClicked(int index)
    {
        if (index < 0 || index >= choiceCount) return;

        selectedIndex = index;
        for (int i = 0; i < 3; i++)
        {
            if (cardImages[i] == null) continue;

            bool selected = i == index;
            cardImages[i].color = cardLethal[i]
                ? (selected ? CardLethalSelectedColor : CardLethalColor)
                : (selected ? CardSelectedColor : CardNormalColor);
        }

        if (confirmButtonObject != null) confirmButtonObject.SetActive(true);
    }

    void HandleConfirmClicked()
    {
        if (selectedIndex < 0 || selectedIndex >= choiceCount) return;
        OnConfirmed?.Invoke(selectedIndex);
    }

    void FindReferences()
    {
        for (int i = 0; i < 3; i++)
        {
            Transform card = transform.Find($"Choice{i}");
            if (card == null)
            {
                Debug.LogError($"NodeChoiceUI: 在 NodePanel 里找不到 \"Choice{i}\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
                continue;
            }

            int captured = i;
            cardImages[i] = card.GetComponent<Image>();
            cardButtons[i] = card.GetComponent<Button>();
            if (cardButtons[i] != null) cardButtons[i].onClick.AddListener(() => HandleCardClicked(captured));

            Transform title = card.Find("TitleText");
            titleTexts[i] = title != null ? title.GetComponent<Text>() : null;

            Transform desc = card.Find("DescriptionText");
            descTexts[i] = desc != null ? desc.GetComponent<Text>() : null;
        }

        Transform confirm = transform.Find("ConfirmButton");
        if (confirm == null)
        {
            Debug.LogError("NodeChoiceUI: 在 NodePanel 里找不到 \"ConfirmButton\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return;
        }
        confirmButtonObject = confirm.gameObject;
        confirmButton = confirm.GetComponent<Button>();
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
        confirmButtonObject.SetActive(false);
    }
}
