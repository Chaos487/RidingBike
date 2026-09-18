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

    [Header("Boost 按钮(手机端)")]
    [Tooltip("氮气就绪时圆圈按钮的颜色，要够亮/够跳，一眼看出来能点。")]
    public Color boostReadyColor = new Color(1f, 0.65f, 0.15f, 1f);
    [Tooltip("氮气没就绪时圆圈按钮的颜色，故意做得偏灰/半透明，暗示点了也没用。")]
    public Color boostNotReadyColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

    Sequence toastTweener;
    Tweener hpPunchTweener;
    float startX;
    bool runEnded;
    bool waitingForStart;

    /// <summary>玩家点了 Start、真正开始骑行的那一刻触发——比如 AudioManager 的骑行音效循环
    /// 要等这个，不能在 EndlessRunBootstrap.Setup() 里一创建就放，不然开始界面还静止着，
    /// 引擎声就已经在响了。</summary>
    public event Action OnGameStarted;

    void Awake()
    {
        FindUIReferences();
    }

    public void Initialize(Transform bike, BikeDamageSystem bikeDamageSystem, BikeController controller)
    {
        bikeTransform = bike;
        damageSystem = bikeDamageSystem;
        bikeController = controller;
        startX = bikeTransform.position.x;
        damageSystem.OnFinalCrash += HandleCrash;
        damageSystem.OnHpChanged += HandlePartialDamage;

        // 开局先按满血刷一次血条文字,不然要等到第一次扣血才会显示"100/100"。
        UpdateHpBar(damageSystem.maxHp, damageSystem.maxHp);

        EnterStartGate();
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
    }

    /// <summary>接上特技/连击这两个反馈系统，弹出对应的 UI 提示。跟 Initialize 分开是因为
    /// EndlessRunBootstrap 里这两个系统要在 RunManager 之后才创建(它们依赖 LandingDetector)。</summary>
    public void InitializeFeedback(TrickSystem trick, ComboSystem combo, ObstacleSpawner obstacles)
    {
        trickSystem = trick;
        comboSystem = combo;
        obstacleSpawner = obstacles;

        trickSystem.OnTrickScored += HandleTrickScored;
        trickSystem.OnTrickFailed += HandleTrickFailed;
        comboSystem.OnComboChanged += HandleComboChanged;
        obstacleSpawner.OnNearMiss += HandleNearMiss;
    }

    void Update()
    {
        if (waitingForStart) return; // 按钮点击走 HandleStartClicked，这里不用轮询任何输入

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
        distanceText.text = $"距离: {distance:0} m";

        // 破纪录的时候实时更新——玩家能看到"最远距离"这个数字跟着当前距离一起往上跳，
        // 比只在结算画面才告诉他"破紀錄了"更有即时反馈。PlayerPrefs.SetFloat 本身只是写内存缓存，
        // 不会每帧都落盘，真正的磁盘写入(Save())留到 HandleCrash 里做一次就够。
        if (distance > bestDistance)
        {
            bestDistance = distance;
            PlayerPrefs.SetFloat(BestDistanceKey, bestDistance);
            if (bestDistanceText != null) bestDistanceText.text = $"最远距离: {bestDistance:0} m";
        }

        float speedKmh = bikeController != null ? Mathf.Abs(bikeController.bikeRigidbody.linearVelocity.x) * 3.6f : 0f;
        speedText.text = $"时速: {speedKmh:0} km/h";

        if (bikeController != null)
        {
            bool ready = bikeController.IsBoostReady;
            boostText.text = ready
                ? "氮气: 就绪 (Shift)"
                : $"氮气: 还差 {bikeController.DistanceUntilBoostReady:0} m";

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
        ShowToast($"{degrees:0}°  +{score}");
    }

    void HandleTrickFailed(float degrees)
    {
        ShowToast($"特技失败 ({degrees:0}°)");
    }

    void HandleNearMiss()
    {
        ShowToast("NEAR MISS!");
    }

    void HandlePartialDamage(float currentHp, float maxHp)
    {
        UpdateHpBar(currentHp, maxHp);
        PlayHpBarPunch();
        ShowToast($"摔车了! 剩余血量 {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}");
    }

    void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpBarFill != null) hpBarFill.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        if (hpValueText != null) hpValueText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, currentHp))}/{Mathf.CeilToInt(maxHp)}";
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
        comboText.text = comboCount > 0 ? $"连击 x{comboCount}" : string.Empty;
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
        statusText.text = $"摔车了! 距离 {distance:0} m\n按 R / 点击屏幕重新开始";
        statusText.gameObject.SetActive(true);

        // 摔车结算是个自然的存盘点——真正落盘一次，防止手机端切后台/被系统杀掉的时候丢掉这一局刚破的纪录
        // (Update() 里 SetFloat 只更新内存缓存，不保证真的写到磁盘)。
        PlayerPrefs.Save();
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
        if (bestDistanceText != null) bestDistanceText.text = $"最远距离: {bestDistance:0} m";
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
