using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 骑行中的暂停入口——左下角一个暂停按钮(手机端点它,桌面端 Esc 键),布局参照 Alto's
/// Odyssey 的暂停画面做成左右分屏:左边一列 Home/Restart/Resume,右边是 Goals/Settings/
/// Language/Stats 四个 Tab(四个都已接真实数据/功能,Tab 切换逻辑用 `TabGroupController`,
/// 跟开始画面的 Menu 面板共用同一份)。
///
/// 真正的"暂停"(Time.timeScale/BikeController 开关)在 `RunManager.TogglePause()` 里做,
/// 这个脚本只管 UI 和调用它——保证 Esc 键和点暂停按钮触发的是同一条状态机路径，不会两边
/// 状态不同步。挂在跟 RunManager 同一个 Canvas 根节点上(EndlessRunBootstrap.SetupRunManagerUI)。
/// </summary>
public class PauseController : MonoBehaviour
{
    RunManager runManager;
    GoalsTabUI goalsTabUI;
    StatsTabUI statsTabUI;

    GameObject pauseButtonObject;
    GameObject pausePanel;
    Button resumeButton;
    Button restartButton;
    Button homeButton;
    TabGroupController tabGroup;

    public void Initialize(RunManager manager, GoalsTabUI goals, StatsTabUI stats)
    {
        runManager = manager;
        goalsTabUI = goals;
        statsTabUI = stats;
        runManager.OnGameStarted += HandleGameStarted;
        runManager.OnRunEnded += HandleRunEnded;
        runManager.OnPauseStateChanged += HandlePauseStateChanged;
    }

    void Awake()
    {
        FindUIReferences();
        WireButtons();

        LocalizationManager.OnLocaleChanged += ApplyLocalization;
        ApplyLocalization();

        if (pauseButtonObject != null) pauseButtonObject.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void OnDestroy()
    {
        LocalizationManager.OnLocaleChanged -= ApplyLocalization;
    }

    void ApplyLocalization()
    {
        // 预制体里烘焙的 Text 组件默认用内置 Arial，没有中文/日文字形，这里连字体一起换——
        // 见 LocalizationManager.GetFont() 注释。
        ApplyButtonLocalization(resumeButton, "button.resume");
        ApplyButtonLocalization(restartButton, "button.restart");
        ApplyButtonLocalization(homeButton, "button.home");
        tabGroup?.ApplyLocalization();
    }

    static void ApplyButtonLocalization(Button button, string key)
    {
        if (button == null) return;
        Text text = button.GetComponent<Text>();
        if (text == null) return;

        text.text = LocalizationManager.Get(key);
        text.font = LocalizationManager.GetFont();
    }

    void Update()
    {
        if (runManager == null || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (!runManager.IsPaused && !runManager.CanPause) return; // 开始前/结算画面不响应 Esc

        TogglePause();
    }

    void FindUIReferences()
    {
        Transform pauseButtonTransform = transform.Find("PauseButton");
        pauseButtonObject = pauseButtonTransform != null ? pauseButtonTransform.gameObject : null;

        Transform pausePanelTransform = transform.Find("PausePanel");
        pausePanel = pausePanelTransform != null ? pausePanelTransform.gameObject : null;

        Transform resumeTransform = transform.Find("PausePanel/ActionList/ResumeButton");
        resumeButton = resumeTransform != null ? resumeTransform.GetComponent<Button>() : null;

        Transform restartTransform = transform.Find("PausePanel/ActionList/RestartButton");
        restartButton = restartTransform != null ? restartTransform.GetComponent<Button>() : null;

        Transform homeTransform = transform.Find("PausePanel/ActionList/HomeButton");
        homeButton = homeTransform != null ? homeTransform.GetComponent<Button>() : null;

        // TabArea 下面挂的是跟 MenuPanel 一样的 TabBar/ContentArea 结构。要保留引用——
        // 语言切换时要转调 tabGroup.ApplyLocalization() 重新刷新 Tab 标题文字。
        Transform tabAreaTransform = transform.Find("PausePanel/TabArea");
        if (tabAreaTransform != null) tabGroup = new TabGroupController(tabAreaTransform);
    }

    void WireButtons()
    {
        if (pauseButtonObject != null)
        {
            Button pauseButton = pauseButtonObject.GetComponent<Button>();
            if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        }

        if (resumeButton != null) resumeButton.onClick.AddListener(TogglePause);
        if (restartButton != null) restartButton.onClick.AddListener(ReloadScene);
        if (homeButton != null) homeButton.onClick.AddListener(ReloadScene);
    }

    void TogglePause()
    {
        // Station 三选一开着时也允许打开/关闭这个暂停面板——RunManager.TogglePause() 自己会
        // 判断游戏是不是已经因为三选一被冻结了，冻结的话只切换 UI 状态，不会去碰
        // Time.timeScale/BikeController，所以这里不用关心 Station 状态。
        if (runManager != null) runManager.TogglePause();
    }

    /// <summary>Restart 和 Home 现在是同一个行为——项目没有单独的主菜单场景,"回到主菜单"
    /// 就是重新加载当前场景,自然会落回 EnterStartGate 的 tap to start 画面,跟摔车结算画面
    /// 的重开走的也是同一条路径,只是这里允许没摔车、骑行中途就直接重开。</summary>
    void ReloadScene()
    {
        // Time.timeScale 是全局静态值，LoadScene 不会自动重置它——暂停中(0)时点 Restart/Home
        // 不手动改回 1 的话，新场景加载出来会直接卡在 0(虽然 EnterStartGate 后面也会设一次，
        // 这里显式复位更保险，不依赖谁先谁后)。ScreenBlurState 同理也是 static，一起清零
        // (见该类 Reset() 注释)——正常暂停面板 Open/Close 本来是配对的，加这行是为了兜底
        // Run Summary/GoalsRecapUI 那种"打开就不会再关"的面板不小心漏加计数的情况。
        Time.timeScale = 1f;
        ScreenBlurState.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void HandleGameStarted()
    {
        if (pauseButtonObject != null) pauseButtonObject.SetActive(true);
    }

    /// <summary>摔车结算后暂停入口就没用了——正常情况下摔车不可能在暂停中发生(暂停时
    /// BikeController 被禁用、物理也停了),但保险起见还是把面板一起收掉。</summary>
    void HandleRunEnded()
    {
        if (pauseButtonObject != null) pauseButtonObject.SetActive(false);
        SetPausePanelOpen(false);
    }

    void HandlePauseStateChanged(bool paused)
    {
        SetPausePanelOpen(paused);
        if (pauseButtonObject != null) pauseButtonObject.SetActive(!paused);

        // 打开的瞬间刷新一次——Goals/Stats 这两个 Tab 背后的跨局计数器在骑行过程中随时会变
        // (不像开局那一刻的初始建表)，暂停期间看到的应该是最新数字，不是开局那一刻的快照。
        if (paused)
        {
            if (goalsTabUI != null) goalsTabUI.Refresh();
            if (statsTabUI != null) statsTabUI.Refresh();
        }
    }

    /// <summary>面板显示状态和 ScreenBlurState 的开关绑在一起管——只有真的发生"开↔关"切换
    /// 才喊 ScreenBlurState,HandleRunEnded 在面板本来就没开的时候强制关一次不应该额外
    /// 影响计数(虽然 EndBlur() 自己夹了下限,这里保持语义清楚)。</summary>
    void SetPausePanelOpen(bool open)
    {
        if (pausePanel == null || pausePanel.activeSelf == open) return;

        pausePanel.SetActive(open);
        if (open) ScreenBlurState.BeginBlur();
        else ScreenBlurState.EndBlur();
    }
}
