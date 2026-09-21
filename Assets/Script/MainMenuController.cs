using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始画面左上角的 Menu 入口——只负责 Goals/Settings/Language/Stats 四个 Tab 之间的
/// 面板切换显示,每个 Tab 的内容目前都是占位文字,不接任何真实数据(存档/设置/多语言/统计
/// 这些逻辑都还没做),后续要做真实功能时再往对应的 Panel 里加东西,这个脚本不用大改。
/// 跟 RunManager 一样按名字在预制体里 Find 子物体,改层级/改名字记得同步改这里。
/// 挂在跟 RunManager 同一个 Canvas 根节点上(EndlessRunBootstrap.SetupRunManagerUI 里一起
/// AddComponent),Menu 入口目前只在"开始前"那个画面有意义,骑行开始后由
/// RunManager.OnGameStarted 通知 HandleGameStarted 收起来。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    static readonly string[] TabNames = { "Goals", "Settings", "Language", "Stats" };

    GameObject menuButtonObject;
    GameObject menuPanel;
    readonly Button[] tabButtons = new Button[TabNames.Length];
    readonly Text[] tabTexts = new Text[TabNames.Length];
    readonly GameObject[] tabPanels = new GameObject[TabNames.Length];
    Button backButton;

    void Awake()
    {
        FindUIReferences();
        WireButtons();
        ShowTab(0);

        if (menuPanel != null) menuPanel.SetActive(false);
    }

    void FindUIReferences()
    {
        Transform menuButtonTransform = transform.Find("MenuButton");
        menuButtonObject = menuButtonTransform != null ? menuButtonTransform.gameObject : null;

        Transform menuPanelTransform = transform.Find("MenuPanel");
        menuPanel = menuPanelTransform != null ? menuPanelTransform.gameObject : null;

        for (int i = 0; i < TabNames.Length; i++)
        {
            Transform tabButtonTransform = transform.Find($"MenuPanel/TabBar/{TabNames[i]}TabButton");
            if (tabButtonTransform != null)
            {
                tabButtons[i] = tabButtonTransform.GetComponent<Button>();
                tabTexts[i] = tabButtonTransform.GetComponent<Text>();
            }

            Transform tabPanelTransform = transform.Find($"MenuPanel/ContentArea/{TabNames[i]}Panel");
            tabPanels[i] = tabPanelTransform != null ? tabPanelTransform.gameObject : null;
        }

        Transform backButtonTransform = transform.Find("MenuPanel/BackButton");
        backButton = backButtonTransform != null ? backButtonTransform.GetComponent<Button>() : null;
    }

    void WireButtons()
    {
        if (menuButtonObject != null)
        {
            Button menuButton = menuButtonObject.GetComponent<Button>();
            if (menuButton != null) menuButton.onClick.AddListener(OpenMenu);
        }

        if (backButton != null) backButton.onClick.AddListener(CloseMenu);

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i; // 闭包要捕获副本,不能直接用循环变量
            if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(index));
        }
    }

    void OpenMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (menuButtonObject != null) menuButtonObject.SetActive(false);
    }

    void CloseMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (menuButtonObject != null) menuButtonObject.SetActive(true);
    }

    void ShowTab(int index)
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

    /// <summary>骑行真正开始后 Menu 入口没有意义了,先收起来(连带菜单面板一起,防止玩家
    /// 点开始前正好开着菜单)。目前只有"开始前"这一个入口,暂停中/结算画面都还不需要。</summary>
    public void HandleGameStarted()
    {
        if (menuButtonObject != null) menuButtonObject.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
    }
}
