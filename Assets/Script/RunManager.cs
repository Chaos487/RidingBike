using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 管理单局 endless run 的状态:显示距离/时速/氮气就绪状态/连击数、弹出特技/贴身险提示、
/// 监听摔车、结算并支持按 R 重开。
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
        BuildUI();
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
        float fill = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        // 临时验证用:排查"血条不掉血"的问题。确认没问题之后可以删掉。
        Debug.Log($"[RunManager] UpdateHpBar currentHp={currentHp} maxHp={maxHp} fill={fill} hpBarFillIsNull={hpBarFill == null}");
        if (hpBarFill != null) hpBarFill.fillAmount = fill;
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

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("EndlessRunCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        distanceText = CreateText(canvasGO.transform, "DistanceText", new Vector2(0f, 1f), new Vector2(20f, -20f), 220f, 24, TextAnchor.UpperLeft);
        speedText = CreateText(canvasGO.transform, "SpeedText", new Vector2(0f, 1f), new Vector2(240f, -20f), 220f, 24, TextAnchor.UpperLeft);
        boostText = CreateText(canvasGO.transform, "BoostText", new Vector2(0f, 1f), new Vector2(20f, -50f), 300f, 22, TextAnchor.UpperLeft);
        comboText = CreateText(canvasGO.transform, "ComboText", new Vector2(0f, 1f), new Vector2(20f, -80f), 300f, 22, TextAnchor.UpperLeft);

        toastText = CreateText(canvasGO.transform, "ToastText", new Vector2(0.5f, 1f), new Vector2(0f, -140f), 600f, 36, TextAnchor.UpperCenter);
        toastText.text = string.Empty;

        statusText = CreateText(canvasGO.transform, "StatusText", new Vector2(0.5f, 0.5f), Vector2.zero, 500f, 32, TextAnchor.MiddleCenter);
        statusText.gameObject.SetActive(false);

        hpLabelText = CreateText(canvasGO.transform, "HpLabelText", new Vector2(1f, 1f), new Vector2(-20f, -20f), 220f, 22, TextAnchor.UpperRight);
        hpLabelText.text = "HP";
        hpBarFill = CreateHpBar(canvasGO.transform, new Vector2(-20f, -46f), 220f, 20f);
    }

    static Image CreateHpBar(Transform parent, Vector2 anchoredPos, float width, float height)
    {
        GameObject bgGO = new GameObject("HpBarBackground");
        bgGO.transform.SetParent(parent, false);

        RectTransform bgRt = bgGO.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(1f, 1f);
        bgRt.anchorMax = new Vector2(1f, 1f);
        bgRt.pivot = new Vector2(1f, 1f);
        bgRt.anchoredPosition = anchoredPos;
        bgRt.sizeDelta = new Vector2(width, height);

        Image background = bgGO.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject fillGO = new GameObject("HpBarFill");
        fillGO.transform.SetParent(bgGO.transform, false);

        RectTransform fillRt = fillGO.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);

        Image fill = fillGO.AddComponent<Image>();
        fill.color = new Color(0.85f, 0.2f, 0.2f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        return fill;
    }

    static Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, float width, int fontSize, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(width, 120f);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }
}
