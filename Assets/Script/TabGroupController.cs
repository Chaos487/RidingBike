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
    // 跟 TabNames 一一对应，Language 这个 Tab 名字本身也要翻译("语言"/"言語"/...)，
    // 跟它面板里那 7 个语言按钮的母语名字不是一回事(那些不翻译，见 LanguageTabUI)。
    static readonly string[] TabLocalizationKeys = { "tab.goals", "tab.settings", "tab.language", "tab.stats" };

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

        ApplyLocalization();
        ShowTab(0);
    }

    /// <summary>Tab 栏四个标题按当前语言重新画一遍——语言可能是在这次打开菜单期间被切换的
    /// (比如先去 Language Tab 选了新语言，再切回来看别的 Tab)，调用方(MainMenuController/
    /// PauseController)订阅 LocalizationManager.OnLocaleChanged 后要转调这个方法。</summary>
    public void ApplyLocalization()
    {
        for (int i = 0; i < tabTexts.Length; i++)
        {
            if (tabTexts[i] == null) continue;
            tabTexts[i].text = LocalizationManager.Get(TabLocalizationKeys[i]);
            // 预制体里烘焙的 Text 组件默认用内置 Arial，没有中文/日文字形——换成
            // LocalizationManager.GetFont()，见该方法注释。
            tabTexts[i].font = LocalizationManager.GetFont();
        }
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
