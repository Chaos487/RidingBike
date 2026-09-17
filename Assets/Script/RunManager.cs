using DG.Tweening;
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
    Image hpBarFill;

    Sequence toastTweener;
    float startX;
    bool runEnded;

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
        if (runEnded)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        float distance = Mathf.Max(0f, bikeTransform.position.x - startX);
        distanceText.text = $"距离: {distance:0} m";

        float speedKmh = bikeController != null ? Mathf.Abs(bikeController.bikeRigidbody.linearVelocity.x) * 3.6f : 0f;
        speedText.text = $"时速: {speedKmh:0} km/h";

        if (bikeController != null)
        {
            boostText.text = bikeController.IsBoostReady
                ? "氮气: 就绪 (Shift)"
                : $"氮气: 还差 {bikeController.DistanceUntilBoostReady:0} m";
        }
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
        ShowToast($"摔车了! 剩余血量 {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}");
    }

    void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpBarFill != null) hpBarFill.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
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

        UpdateHpBar(0f, 1f);

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
        statusText.text = $"摔车了! 距离 {distance:0} m\n按 R 重新开始";
        statusText.gameObject.SetActive(true);
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
        hpBarFill = FindImage("HpBarBackground/HpBarFill");

        if (toastText != null) toastText.text = string.Empty;
        if (statusText != null) statusText.gameObject.SetActive(false);

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

    static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
    }
}
