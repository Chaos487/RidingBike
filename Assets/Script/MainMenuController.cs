using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始画面左上角的 Menu 入口——只负责打开/关闭菜单面板,Tab 切换逻辑交给
/// `TabGroupController`(跟骑行中的暂停面板 `PauseController` 共用同一份)。
/// 跟 RunManager 一样按名字在预制体里 Find 子物体,改层级/改名字记得同步改这里。
/// 挂在跟 RunManager 同一个 Canvas 根节点上(EndlessRunBootstrap.SetupRunManagerUI 里一起
/// AddComponent),Menu 入口目前只在"开始前"那个画面有意义,骑行开始后由
/// RunManager.OnGameStarted 通知 HandleGameStarted 收起来。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    GameObject menuButtonObject;
    GameObject menuPanel;
    Button backButton;
    TabGroupController tabGroup;

    void Awake()
    {
        FindUIReferences();
        WireButtons();

        if (menuPanel != null) menuPanel.SetActive(false);
    }

    void FindUIReferences()
    {
        Transform menuButtonTransform = transform.Find("MenuButton");
        menuButtonObject = menuButtonTransform != null ? menuButtonTransform.gameObject : null;

        Transform menuPanelTransform = transform.Find("MenuPanel");
        menuPanel = menuPanelTransform != null ? menuPanelTransform.gameObject : null;
        if (menuPanelTransform != null) tabGroup = new TabGroupController(menuPanelTransform);

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

    /// <summary>骑行真正开始后 Menu 入口没有意义了,先收起来(连带菜单面板一起,防止玩家
    /// 点开始前正好开着菜单)。目前只有"开始前"这一个入口,暂停中另有 PauseController 自己
    /// 的 Menu(同一份 Tab 内容,不同的入口/布局),结算画面都不需要。</summary>
    public void HandleGameStarted()
    {
        if (menuButtonObject != null) menuButtonObject.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
    }
}
