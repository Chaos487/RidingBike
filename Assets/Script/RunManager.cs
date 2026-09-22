using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 管理单局 endless run 的状态:显示距离/时速/氮气就绪状态/连击数、弹出特技/贴身险提示、
/// 监听摔车、结算并支持按 R 重开。
/// UI 视觉本身是 Assets/prefab/EndlessRunCanvas.prefab(手动在 Editor 里搭的,改颜色/字体/布局
/// 直接在那份预制体上改,不用碰这个脚本)——这个脚本是在 EndlessRunBootstrap 里
/// Instantiate 完预制体之后直接挂到它根节点上的,Awake() 按名字把预制体里的子物体找出来。
/// 改预制体层级/改物体名字的话，下面 FindUIReferences() 里对应的路径也要跟着改。
/// </summary>
public class RunManager : MonoBehaviour
{
    [Header("Toast")]
    public float toastHoldSeconds = 1f;
    public float toastFadeSeconds = 0.4f;

    Transform bikeTransform;
    BikeDamageSystem damageSystem;
    BikeController bikeController;
    TrickSystem trickSystem;
    ComboSystem comboSystem;
    ObstacleSpawner obstacleSpawner;

    Text distanceText;
    Text speedText;
    Text boostText;
    Text comboText;
    Text toastText;
    Text statusText;
    Text hpLabelText;
    Text bestDistanceText;
    Text gearText;
    GearManager gearManager;
    Image hpBarFill;
    TMP_Text hpValueText;
    GameObject hpBarRoot;
    Button startButton;
    Button boostButton;
    Image boostButtonImage;

    // PlayerPrefs 在 PC/iOS 上都是内置的、跨平台的本地持久化(Windows 存注册表，iOS 存 plist)，
    // 不用另外写一套存档逻辑——这是这个项目第一次真正"跨局/跨启动"持久化的数据。
    const string BestDistanceKey = "RidingBike_BestDistance";
    float bestDistance;

    [Header("HP 扣血反馈")]
    [Tooltip("扣血瞬间整个血条框(HpBarRoot)放大再回弹的幅度,0 = 关闭。")]
    public float hpPunchStrength = 0.25f;
    [Tooltip("放大回弹动画的总时长(秒)。")]
    public float hpPunchDuration = 0.35f;
    [Tooltip("回弹震荡次数,越大弹得越多次。")]
    public int hpPunchVibrato = 8;
    [Tooltip("回弹的弹性,0 = 不回弹(单纯放大再缩回),1 = 弹性拉满。")]
    [Range(0f, 1f)]
    public float hpPunchElasticity = 0.6f;

    [Header("HP 低血量闪烁")]
    [Tooltip("当前血量 / 满血值 低于这个比例时,血条开始闪烁提示玩家——防止玩家没注意到自己" +
             "残血,在 Node 里选了扣血选项直接把自己扣死。")]
    [Range(0f, 1f)]
    public float hpDangerRatio = 0.2f;
    [Tooltip("闪烁一次(暗→亮或亮→暗单程)的时长,越小闪得越快。")]
    public float hpBlinkDuration = 0.4f;
    [Tooltip("闪烁时血条最暗淡到的透明度。")]
    [Range(0f, 1f)]
    public float hpBlinkMinAlpha = 0.25f;

    [Header("Boost 按钮(手机端)")]
    [Tooltip("氮气就绪时圆圈按钮的颜色，要够亮/够跳，一眼看出来能点。")]
    public Color boostReadyColor = new Color(1f, 0.65f, 0.15f, 1f);
    [Tooltip("氮气没就绪时圆圈按钮的颜色，故意做得偏灰/半透明，暗示点了也没用。")]
    public Color boostNotReadyColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

    Sequence toastTweener;
    Tweener hpPunchTweener;
    Tweener hpBlinkTweener;
    bool hpBlinking;
    float startX;
    bool runEnded;
    bool waitingForStart;
    bool paused;

    /// <summary>玩家点了 Start、真正开始骑行的那一刻触发——比如 AudioManager 的骑行音效循环
    /// 要等这个，不能在 EndlessRunBootstrap.Setup() 里一创建就放，不然开始界面还静止着，
    /// 引擎声就已经在响了。</summary>
    public event Action OnGameStarted;

    /// <summary>真正判死(摔车结算)那一刻触发——PauseController 用来把暂停入口收起来，
    /// 结算画面不需要能暂停。</summary>
    public event Action OnRunEnded;

    /// <summary>暂停状态切换时触发(true = 刚暂停，false = 刚恢复)，参数就是切换后的新状态。
    /// PauseController 点按钮/按 Esc 都走 TogglePause()，靠这个事件同步面板显示，不用两边
    /// 分别维护一份"是否暂停"。</summary>
    public event Action<bool> OnPauseStateChanged;

    public bool IsPaused => paused;

    /// <summary>只有正常骑行中(不在开始前/不在结算画面)才允许暂停。Station 三选一期间也算
    /// "正常骑行中"——玩家可以在三选一开着的时候照样打开暂停面板,见 TogglePause 的注释。</summary>
    public bool CanPause => !waitingForStart && !runEnded;

    /// <summary>反向查询:游戏是不是已经因为别的系统而处于暂停状态(目前只有 Station 三选一,
    /// 接的是 NodeManager.IsStationPaused)。为空时视为没有,不影响暂停正常工作。
    /// TogglePause() 靠这个判断"这次切换要不要真的去动 Time.timeScale/BikeController"——
    /// 不能用 NodeManager 的直接引用(RunManager 比 NodeManager 先创建,方向反了),所以用
    /// Func 反向查询,跟 GearSpawner.isInSafeZone 这类接线是同一个套路。</summary>
    public Func<bool> isPausedByOtherSystem;

    void Awake()
    {
        FindUIReferences();
    }

    public void Initialize(Transform bike, BikeDamageSystem bikeDamageSystem, BikeController controller, GearManager gearManagerRef)
    {
        bikeTransform = bike;
        damageSystem = bikeDamageSystem;
        bikeController = controller;
        gearManager = gearManagerRef;
        startX = bikeTransform.position.x;
        damageSystem.OnFinalCrash += HandleCrash;
        damageSystem.OnHpChanged += HandlePartialDamage;
        damageSystem.OnMaxHpChanged += HandleMaxHpChanged;

        // 开局先按满血刷一次血条文字,不然要等到第一次扣血才会显示"100/100"。
        UpdateHpBar(damageSystem.maxHp, damageSystem.maxHp);

        // GearManager.Initialize() 在这之前已经从存档读过一次数量并广播过事件了，那时候
        // RunManager 还没订阅、接不到——这里直接读它当前的值先显示一次，之后靠事件保持实时更新。
        if (gearManager != null)
        {
            HandleGearCountChanged(gearManager.CurrentGearCount);
            gearManager.OnGearCountChanged += HandleGearCountChanged;
        }

        EnterStartGate();
    }

    void HandleGearCountChanged(int count)
    {
        if (gearText != null) gearText.text = $"Gears: {count}";
    }

    /// <summary>开局前的静止画面:按住 Play 之后的初始状态就是开始界面，只多一个 Start 按钮——
    /// 真正暂停(Time.timeScale = 0)，其它 HUD 全部隐藏，只留 Start。每次重开(R/点按屏幕
    /// 重新加载场景)都会重新经过这里，不是只在 App 第一次启动时出现。
    ///
    /// 光靠 timeScale 挡不住 BikeController.Update() 里的按键/点击判定——Time.timeScale
    /// 不影响 Update() 执行、也不影响 Input 读取——所以还要把 BikeController 显式禁用掉，
    /// 不然玩家在开始界面点一下屏幕会被误读成一次跳跃输入，等按 Start 之后车身状态会很奇怪。</summary>
    void EnterStartGate()
    {
        waitingForStart = true;
        Time.timeScale = 0f;
        if (bikeController != null) bikeController.enabled = false;

        SetHudVisible(false);
        if (startButton != null) startButton.gameObject.SetActive(true);
    }

    void HandleStartClicked()
    {
        if (!waitingForStart) return;
        waitingForStart = false;

        Time.timeScale = 1f;
        if (bikeController != null) bikeController.enabled = true;

        SetHudVisible(true);
        if (startButton != null) startButton.gameObject.SetActive(false);

        OnGameStarted?.Invoke();
    }

    /// <summary>骑行中途暂停/恢复——跟 EnterStartGate 一样，光靠 Time.timeScale 挡不住
    /// BikeController.Update() 里的按键判定，所以也要把它显式禁用掉，不然暂停面板开着的时候
    /// 点屏幕会被误读成一次跳跃输入。PauseController 的暂停按钮点击和 Esc 键都走这一个方法，
    /// 保证两条触发路径最终是同一个状态。
    ///
    /// Station 三选一期间也允许打开这个暂停面板，但那时候游戏已经因为三选一本身被冻结了
    /// (Time.timeScale=0、BikeController 已禁用)——这两下 Toggle(打开/关闭暂停面板)都不能
    /// 真的去碰 Time.timeScale/BikeController，不然点 Resume 会把三选一还没处理完的这段
    /// 解冻掉，车在玩家选完之前就先动起来了。isPausedByOtherSystem 为真时，这里只切换
    /// paused 这个状态给 PauseController 用来显示/隐藏暂停面板，物理这块交给三选一自己收尾。</summary>
    public void TogglePause()
    {
        if (!CanPause && !paused) return; // 开始前/结算画面不允许暂停；已经暂停的话允许恢复

        paused = !paused;

        bool frozenByOtherSystem = isPausedByOtherSystem != null && isPausedByOtherSystem();
        if (!frozenByOtherSystem)
        {
            Time.timeScale = paused ? 0f : 1f;
            if (bikeController != null) bikeController.enabled = !paused;
        }

        OnPauseStateChanged?.Invoke(paused);
    }

    void SetHudVisible(bool visible)
    {
        if (distanceText != null) distanceText.gameObject.SetActive(visible);
        if (speedText != null) speedText.gameObject.SetActive(visible);
        if (boostText != null) boostText.gameObject.SetActive(visible);
        if (comboText != null) comboText.gameObject.SetActive(visible);
        if (hpLabelText != null) hpLabelText.gameObject.SetActive(visible);
        if (hpBarRoot != null) hpBarRoot.SetActive(visible);
        if (boostButton != null) boostButton.gameObject.SetActive(visible);
        if (bestDistanceText != null) bestDistanceText.gameObject.SetActive(visible);
        if (gearText != null) gearText.gameObject.SetActive(visible);
    }

    /// <summary>接上特技/连击这两个反馈系统，弹出对应的 UI 提示。跟 Initialize 分开是因为
    /// EndlessRunBootstrap 里这两个系统要在 RunManager 之后才创建(它们依赖 LandingDetector)。
    /// 这里比 TrickSystem 更早订阅 landingDetector.OnLanded(调用方保证的顺序)——转出特技的落地,
    /// TrickSystem 随后弹出的更具体的提示(分数/Trick Failed)会拼在落地质量下面显示两行；
    /// 没转特技的普通落地则只有质量这一行。</summary>
    public void InitializeFeedback(TrickSystem trick, ComboSystem combo, ObstacleSpawner obstacles, LandingDetector landing)
    {
        trickSystem = trick;
        comboSystem = combo;
        obstacleSpawner = obstacles;

        landing.OnLanded += HandleLanded;
        trickSystem.OnTrickScored += HandleTrickScored;
        trickSystem.OnTrickFailed += HandleTrickFailed;
        comboSystem.OnComboChanged += HandleComboChanged;
        obstacleSpawner.OnNearMiss += HandleNearMiss;
    }

    // 落地事件必然比同一次落地的 Trick 结算先触发(RunManager 比 TrickSystem 更早订阅
    // landingDetector.OnLanded，见 InitializeFeedback 的注释)，这里存一份给 HandleTrickScored/
    // HandleTrickFailed 拼到第二行用，两者共用同一个 Toast 组件、换行垂直排列显示。
    string lastLandingQualityLabel = string.Empty;

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        lastLandingQualityLabel = LandingQualityLabel(quality);
        ShowToast(lastLandingQualityLabel);
    }

    static string LandingQualityLabel(LandingDetector.Quality quality) => quality switch
    {
        LandingDetector.Quality.Perfect => "PERFECT!",
        LandingDetector.Quality.Good => "GOOD",
        _ => "NOT BAD",
    };

    void Update()
    {
        if (waitingForStart) return; // 按钮点击走 HandleStartClicked，这里不用轮询任何输入
        if (paused) return; // Time.timeScale = 0 挡不住 Update() 本身还在跑，这里顺便短路掉

        if (runEnded)
        {
            // 手机端没有 R 键：摔车结算画面这时候没有别的可点的 UI 跟它抢，屏幕任意位置点一下
            // 就重开，不用像 BikeController 里判断跳跃输入那样去排除点在 UI 上的情况。
            if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        float distance = Mathf.Max(0f, bikeTransform.position.x - startX);
        distanceText.text = $"Distance: {distance:0} m";

        // 破纪录的时候实时更新——玩家能看到"最远距离"这个数字跟着当前距离一起往上跳，
        // 比只在结算画面才告诉他"破紀錄了"更有即时反馈。PlayerPrefs.SetFloat 本身只是写内存缓存，
        // 不会每帧都落盘，真正的磁盘写入(Save())留到 HandleCrash 里做一次就够。
        if (distance > bestDistance)
        {
            bestDistance = distance;
            PlayerPrefs.SetFloat(BestDistanceKey, bestDistance);
            if (bestDistanceText != null) bestDistanceText.text = $"Best: {bestDistance:0} m";
        }

        float speedKmh = bikeController != null ? Mathf.Abs(bikeController.bikeRigidbody.linearVelocity.x) * 3.6f : 0f;
        speedText.text = $"Speed: {speedKmh:0} km/h";

        if (bikeController != null)
        {
            bool ready = bikeController.IsBoostReady;
            boostText.text = ready
                ? "Boost: Ready (Shift)"
                : $"Boost: {bikeController.DistanceUntilBoostReady:0} m to go";

            if (boostButtonImage != null) boostButtonImage.color = ready ? boostReadyColor : boostNotReadyColor;
        }
    }

    void HandleBoostButtonClicked()
    {
        // 没就绪的时候点了也没用——TryTriggerBoost() 自己内部会先判 IsBoostReady，这里不用重复判断。
        if (bikeController != null) bikeController.TryTriggerBoost();
    }

    void HandleTrickScored(int score, float degrees)
    {
        ShowToast($"{lastLandingQualityLabel}\n{degrees:0}°  +{score}");
    }

    void HandleTrickFailed(float degrees)
    {
        ShowToast($"{lastLandingQualityLabel}\nTrick Failed ({degrees:0}°)");
    }

    void HandleNearMiss()
    {
        ShowToast("NEAR MISS!");
    }

    /// <summary>NodeManager 进入 Approaching 状态时调用——车速开始平滑下降接近 Station 之前,
    /// 先给玩家一个"为什么车在变慢"的提示,顺带说明这段路跳跃/氮气都被锁住了
    /// (BikeController.jumpAndBoostLocked),不是无缘无故的减速/操作失灵。</summary>
    public void ShowStationApproachWarning()
    {
        ShowToast("Approaching station — jump/boost disabled");
    }

    void HandlePartialDamage(float currentHp, float maxHp)
    {
        UpdateHpBar(currentHp, maxHp);
        PlayHpBarPunch();
        ShowToast($"Crashed! HP {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}");
    }

    /// <summary>满血值被 Node 系统这类"非摔车"来源改动时触发——只静默刷新血条,不弹"摔车了"
    /// 的 toast、也不放放大回弹动画,那两个是专门给真的摔车用的反馈,用在这里会误导玩家。</summary>
    void HandleMaxHpChanged(float currentHp, float maxHp)
    {
        UpdateHpBar(currentHp, maxHp);
    }

    void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpBarFill != null) hpBarFill.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        if (hpValueText != null) hpValueText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, currentHp))}/{Mathf.CeilToInt(maxHp)}";

        // 两种情况都算危险,满足其一就闪烁:①当前血量本身撑不到满血值的 hpDangerRatio;
        // ②满血值被 Node 削得只剩开局起始满血值的 hpDangerRatio——哪怕当前正好是满状态
        // (比如被削到只剩 10 上限、当前 10/10),血量池薄成这样也该提醒玩家很危险。
        bool currentHpDanger = maxHp > 0f && currentHp / maxHp < hpDangerRatio;
        bool maxHpDanger = damageSystem != null && damageSystem.OriginalMaxHp > 0f &&
            maxHp / damageSystem.OriginalMaxHp < hpDangerRatio;
        SetHpDangerBlink(currentHpDanger || maxHpDanger);
    }

    /// <summary>残血(低于 hpDangerRatio)时让血条来回闪烁,提醒玩家现在很危险——比如在 Node
    /// 选项里点了个扣血选项可能会直接扣死。用 DOFade 在满不透明和 hpBlinkMinAlpha 之间来回跳,
    /// 状态没变化时不重复触发(不然每次 UpdateHpBar 都会打断正在播的闪烁,动画会卡顿）。</summary>
    void SetHpDangerBlink(bool active)
    {
        if (active == hpBlinking) return;
        hpBlinking = active;

        hpBlinkTweener?.Kill();

        if (hpBarFill == null) return;

        if (active)
        {
            Color c = hpBarFill.color;
            c.a = 1f;
            hpBarFill.color = c;
            hpBlinkTweener = hpBarFill.DOFade(hpBlinkMinAlpha, hpBlinkDuration).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            Color c = hpBarFill.color;
            c.a = 1f;
            hpBarFill.color = c;
        }
    }

    /// <summary>扣血瞬间整个血条框放大再回弹一下,提醒玩家扣血了——跟落地反馈同一个手法(DOTween
    /// 自带的 DOPunchScale),从 1 倍放大到 (1+hpPunchStrength) 倍再震荡回弹到 1 倍。
    /// 每次扣血都要先 Kill 掉上一次没播完的,不然短时间内连续扣血会叠加出越缩越小的诡异缩放。</summary>
    void PlayHpBarPunch()
    {
        if (hpBarRoot == null) return;

        hpPunchTweener?.Kill();
        hpBarRoot.transform.localScale = Vector3.one;
        hpPunchTweener = hpBarRoot.transform
            .DOPunchScale(Vector3.one * hpPunchStrength, hpPunchDuration, hpPunchVibrato, hpPunchElasticity);
    }

    void HandleComboChanged(int comboCount)
    {
        comboText.text = comboCount > 0 ? $"Combo x{comboCount}" : string.Empty;
    }

    void ShowToast(string message)
    {
        toastTweener?.Kill();

        toastText.text = message;
        Color c = toastText.color;
        c.a = 1f;
        toastText.color = c;

        toastTweener = DOTween.Sequence()
            .AppendInterval(toastHoldSeconds)
            .Append(toastText.DOFade(0f, toastFadeSeconds));
    }

    void HandleCrash()
    {
        if (runEnded) return;
        runEnded = true;

        UpdateHpBar(0f, damageSystem != null ? damageSystem.maxHp : 1f);

        if (bikeController != null)
        {
            if (bikeController.backWheelJoint != null)
            {
                JointMotor2D motor = bikeController.backWheelJoint.motor;
                motor.motorSpeed = 0f;
                bikeController.backWheelJoint.motor = motor;
            }
            bikeController.enabled = false;
        }

        float distance = Mathf.Max(0f, bikeTransform.position.x - startX);
        statusText.text = $"Crashed! Distance {distance:0} m\nPress R / tap screen to restart";
        statusText.gameObject.SetActive(true);

        // 摔车结算是个自然的存盘点——真正落盘一次，防止手机端切后台/被系统杀掉的时候丢掉这一局刚破的纪录
        // (Update() 里 SetFloat 只更新内存缓存，不保证真的写到磁盘)。
        PlayerPrefs.Save();

        OnRunEnded?.Invoke();
    }

    void FindUIReferences()
    {
        distanceText = FindText("DistanceText");
        speedText = FindText("SpeedText");
        boostText = FindText("BoostText");
        comboText = FindText("ComboText");
        toastText = FindText("ToastText");
        statusText = FindText("StatusText");
        hpLabelText = FindText("HpLabelText");
        bestDistanceText = FindText("BestDistanceText");
        gearText = FindText("GearText");
        hpBarFill = FindImage("HpBarRoot/HpBarBackground/HpBarFill");
        hpValueText = FindTMPText("HpBarRoot/HpValueText");
        Transform hpBarRootTransform = transform.Find("HpBarRoot");
        hpBarRoot = hpBarRootTransform != null ? hpBarRootTransform.gameObject : null;
        startButton = FindButton("StartButton");
        boostButton = FindButton("BoostButton");
        boostButtonImage = boostButton != null ? boostButton.GetComponent<Image>() : null;

        if (toastText != null) toastText.text = string.Empty;
        if (statusText != null) statusText.gameObject.SetActive(false);

        bestDistance = PlayerPrefs.GetFloat(BestDistanceKey, 0f);
        if (bestDistanceText != null) bestDistanceText.text = $"Best: {bestDistance:0} m";
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false); // EnterStartGate() 会在 Initialize() 里再打开，这里先关掉避免第一帧闪一下
            startButton.onClick.AddListener(HandleStartClicked);
        }
        if (boostButton != null)
        {
            boostButton.onClick.AddListener(HandleBoostButtonClicked);
            if (boostButtonImage != null) boostButtonImage.color = boostNotReadyColor; // 初始状态先按"没就绪"画，Update() 会立刻按真实状态纠正
        }

        // Image.Type.Filled 在没有指定 sprite 的时候会直接走"画整个矩形"的兜底逻辑，
        // fillAmount 完全不生效(这点跟 Type.Simple 不一样，纯色矩形那个技巧对 Filled 不成立)。
        // 运行时强制保证这两项，不依赖预制体里有没有设对；颜色/fillMethod/fillOrigin 这些纯视觉的
        // 交给预制体自己定，这里不碰。
        if (hpBarFill != null)
        {
            hpBarFill.type = Image.Type.Filled;
            hpBarFill.sprite = CreateSolidSprite();
        }
    }

    Text FindText(string path)
    {
        Transform t = transform.Find(path);
        if (t == null)
        {
            Debug.LogError($"[RunManager] 在 UI 预制体里找不到 \"{path}\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return null;
        }
        return t.GetComponent<Text>();
    }

    Image FindImage(string path)
    {
        Transform t = transform.Find(path);
        if (t == null)
        {
            Debug.LogError($"[RunManager] 在 UI 预制体里找不到 \"{path}\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return null;
        }
        return t.GetComponent<Image>();
    }

    Button FindButton(string path)
    {
        Transform t = transform.Find(path);
        if (t == null)
        {
            Debug.LogError($"[RunManager] 在 UI 预制体里找不到 \"{path}\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return null;
        }
        return t.GetComponent<Button>();
    }

    TMP_Text FindTMPText(string path)
    {
        Transform t = transform.Find(path);
        if (t == null)
        {
            Debug.LogError($"[RunManager] 在 UI 预制体里找不到 \"{path}\"，检查一下 EndlessRunCanvas.prefab 的层级/命名有没有改动。");
            return null;
        }
        return t.GetComponent<TMP_Text>();
    }

    static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
    }
}
