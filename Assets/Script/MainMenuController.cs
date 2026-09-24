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
    Text menuButtonLabel;
    GameObject menuPanel;
    Button backButton;
    Text backButtonLabel;
    TabGroupController tabGroup;

    void Awake()
    {
        FindUIReferences();
        WireButtons();

        LocalizationManager.OnLocaleChanged += ApplyLocalization;
        ApplyLocalization();

        if (menuPanel != null) menuPanel.SetActive(false);
    }

    void OnDestroy()
    {
        LocalizationManager.OnLocaleChanged -= ApplyLocalization;
    }

    void ApplyLocalization()
    {
        // 预制体里烘焙的 Text 组件默认用内置 Arial，没有中文/日文字形，这里连字体一起换——
        // 见 LocalizationManager.GetFont() 注释。
        if (menuButtonLabel != null)
        {
            menuButtonLabel.text = LocalizationManager.Get("button.menu");
            menuButtonLabel.font = LocalizationManager.GetFont();
        }
        if (backButtonLabel != null)
        {
            backButtonLabel.text = LocalizationManager.Get("button.back");
            backButtonLabel.font = LocalizationManager.GetFont();
        }
        tabGroup?.ApplyLocalization();
    }

    void FindUIReferences()
    {
        Transform menuButtonTransform = transform.Find("MenuButton");
        menuButtonObject = menuButtonTransform != null ? menuButtonTransform.gameObject : null;
        menuButtonLabel = menuButtonTransform != null ? menuButtonTransform.Find("MenuButtonLabel")?.GetComponent<Text>() : null;

        Transform menuPanelTransform = transform.Find("MenuPanel");
        menuPanel = menuPanelTransform != null ? menuPanelTransform.gameObject : null;
        if (menuPanelTransform != null) tabGroup = new TabGroupController(menuPanelTransform);

        Transform backButtonTransform = transform.Find("MenuPanel/BackButton");
        backButton = backButtonTransform != null ? backButtonTransform.GetComponent<Button>() : null;
        backButtonLabel = backButtonTransform != null ? backButtonTransform.Find("BackButtonLabel")?.GetComponent<Text>() : null;
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
        SetMenuOpen(true);
        if (menuButtonObject != null) menuButtonObject.SetActive(false);
    }

    void CloseMenu()
    {
        SetMenuOpen(false);
        if (menuButtonObject != null) menuButtonObject.SetActive(true);
    }

    /// <summary>骑行真正开始后 Menu 入口没有意义了,先收起来(连带菜单面板一起,防止玩家
    /// 点开始前正好开着菜单)。目前只有"开始前"这一个入口,暂停中另有 PauseController 自己
    /// 的 Menu(同一份 Tab 内容,不同的入口/布局),结算画面都不需要。</summary>
    public void HandleGameStarted()
    {
        if (menuButtonObject != null) menuButtonObject.SetActive(false);
        SetMenuOpen(false);
    }

    /// <summary>面板显示状态和 ScreenBlurState 的开关绑在一起管,不分散在好几个调用点各自
    /// 判断——只有真的从"开着"变成"关着"(或反过来)才会喊 ScreenBlurState,不然
    /// HandleGameStarted() 在面板本来就没开的时候也调一次 CloseMenu 逻辑,会把计数减到负数
    /// 之外的地方去(虽然 EndBlur() 自己有夹到 0 的保护,但语义上还是应该只在真的关闭时喊)。</summary>
    void SetMenuOpen(bool open)
    {
        if (menuPanel == null || menuPanel.activeSelf == open) return;

        menuPanel.SetActive(open);
        if (open) ScreenBlurState.BeginBlur();
        else ScreenBlurState.EndBlur();
    }
}
