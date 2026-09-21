using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Goals/Settings/Language/Stats 四个 Tab 之间的切换逻辑——开始画面的 Menu 面板
/// (`MainMenuController`)和骑行中的暂停面板(`PauseController`)两处复用同一份，两边的
/// Tab 内容目前都只是占位文字,以后要接真实数据也只用改这一份。不是 MonoBehaviour,纯逻辑
/// 类,由用到它的脚本在自己的 Awake() 里 new 一个出来,传入各自面板下 TabBar/ContentArea
/// 所在的根节点(要求这个根节点下直接有 "TabBar/{Name}TabButton" 和
/// "ContentArea/{Name}Panel" 这两条路径,名字对不上会在对应 Tab 上留空,不报错也不崩)。
/// </summary>
public class TabGroupController
{
    static readonly string[] TabNames = { "Goals", "Settings", "Language", "Stats" };

    readonly Button[] tabButtons = new Button[TabNames.Length];
    readonly Text[] tabTexts = new Text[TabNames.Length];
    readonly GameObject[] tabPanels = new GameObject[TabNames.Length];

    public TabGroupController(Transform root)
    {
        for (int i = 0; i < TabNames.Length; i++)
        {
            Transform tabButtonTransform = root.Find($"TabBar/{TabNames[i]}TabButton");
            if (tabButtonTransform != null)
            {
                tabButtons[i] = tabButtonTransform.GetComponent<Button>();
                tabTexts[i] = tabButtonTransform.GetComponent<Text>();
            }

            Transform tabPanelTransform = root.Find($"ContentArea/{TabNames[i]}Panel");
            tabPanels[i] = tabPanelTransform != null ? tabPanelTransform.gameObject : null;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i; // 闭包要捕获副本,不能直接用循环变量
            if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(index));
        }

        ShowTab(0);
    }

    public void ShowTab(int index)
    {
        for (int i = 0; i < tabPanels.Length; i++)
        {
            bool selected = i == index;
            if (tabPanels[i] != null) tabPanels[i].SetActive(selected);
            if (tabTexts[i] != null)
            {
                tabTexts[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                tabTexts[i].color = selected ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
        }
    }
}
