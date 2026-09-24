using UnityEngine;
using UnityEngine.UI;

/// <summary>只能往末尾加——存进 PlayerPrefs 的是这个枚举的序号，插中间会让玩家设备上已经
/// 存的选择读出来变成另一边，这个项目里其它枚举都是同样的规矩(见 Locale 枚举注释)。
/// Right 排第一是为了兼容 BoostButton 现在硬编码在右下角这个既有默认值——旧存档没有这条
/// PlayerPrefs 记录时，GetInt 的默认值 0 要能落在"维持现状"这一边，不能读出来变成 Left。</summary>
public enum ButtonSide { Right, Left }

/// <summary>
/// 参考 Alto's Odyssey 的 Settings 面板——Sounds/Music 两条音量滑条 + 加速按钮挂左边还是
/// 右边，接进菜单/暂停面板里现成的 SettingsPanel 占位节点，跟 GoalsTabUI/StatsTabUI 是
/// 同一套路(两处 Tab 面板各建一份)。不做分辨率——这个项目的输入/布局都是照手机触屏做的，
/// "分辨率"是桌面/Steam 场景的概念，手机上没有这个用户概念。
///
/// 音量：`AudioManager` 本来没有音量控制 API(6 个音效槽位各自是独立 AudioSource，没有接
/// AudioMixer)，这里给每个槽位的音量再乘一个运行时缩放系数(`AudioManager.SetSoundsVolume`/
/// `SetMusicVolume`)，不用新建 AudioMixer 资产。
///
/// 加速按钮左右：这个项目没有独立的跳跃按钮(跳跃是全屏幕任意位置点按触发)，改哪边不会跟
/// 任何其它按钮冲突——直接改 `BoostButton` 的 RectTransform 锚点。
/// </summary>
public class SettingsTabUI : MonoBehaviour
{
    const string BoostButtonSideKey = "RidingBike_BoostButtonSide";

    class PanelRefs
    {
        public Slider soundsSlider;
        public Slider musicSlider;
        public Text soundsLabel;
        public Text musicLabel;
        public Text boostButtonLabel;
        public Text leftLabel;
        public Text rightLabel;
        public Image leftBackground;
        public Image rightBackground;
    }

    PanelRefs[] panelRefs;
    ButtonSide currentSide;

    public void Initialize()
    {
        currentSide = (ButtonSide)PlayerPrefs.GetInt(BoostButtonSideKey, (int)ButtonSide.Right);
        ApplyBoostButtonSide();

        Transform menuSettingsPanel = transform.Find("MenuPanel/ContentArea/SettingsPanel");
        Transform pauseSettingsPanel = transform.Find("PausePanel/TabArea/ContentArea/SettingsPanel");

        panelRefs = new[]
        {
            menuSettingsPanel != null ? BuildInto(menuSettingsPanel) : null,
            pauseSettingsPanel != null ? BuildInto(pauseSettingsPanel) : null,
        };

        ApplyLocalization();
        RefreshSideHighlight();
        LocalizationManager.OnLocaleChanged += ApplyLocalization;
    }

    void OnDestroy()
    {
        LocalizationManager.OnLocaleChanged -= ApplyLocalization;
    }

    void ApplyLocalization()
    {
        if (panelRefs == null) return;

        foreach (PanelRefs refs in panelRefs)
        {
            if (refs == null) continue;
            if (refs.soundsLabel != null) refs.soundsLabel.text = LocalizationManager.Get("settings.sounds");
            if (refs.musicLabel != null) refs.musicLabel.text = LocalizationManager.Get("settings.music");
            if (refs.boostButtonLabel != null) refs.boostButtonLabel.text = LocalizationManager.Get("settings.boostButton");
            if (refs.leftLabel != null) refs.leftLabel.text = LocalizationManager.Get("settings.left");
            if (refs.rightLabel != null) refs.rightLabel.text = LocalizationManager.Get("settings.right");
        }
    }

    void ApplyBoostButtonSide()
    {
        Transform boostButton = transform.Find("BoostButton");
        if (boostButton == null) return;

        RectTransform rt = (RectTransform)boostButton;
        if (currentSide == ButtonSide.Right)
        {
            // 跟预制体里原来硬编码的右下角位置完全一致(见 EndlessRunCanvas.prefab)。
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-120f, 120f);
        }
        else
        {
            // 镜像到左下角——同样的边距，只是换一边。
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(120f, 120f);
        }
    }

    void SetSide(ButtonSide side)
    {
        if (currentSide == side) return;

        currentSide = side;
        PlayerPrefs.SetInt(BoostButtonSideKey, (int)side);
        PlayerPrefs.Save();

        ApplyBoostButtonSide();
        RefreshSideHighlight();
    }

    void RefreshSideHighlight()
    {
        if (panelRefs == null) return;

        Color selectedColor = Color.white;
        Color unselectedColor = new Color(1f, 1f, 1f, 0.12f);

        foreach (PanelRefs refs in panelRefs)
        {
            if (refs == null) continue;
            if (refs.leftBackground != null) refs.leftBackground.color = currentSide == ButtonSide.Left ? selectedColor : unselectedColor;
            if (refs.rightBackground != null) refs.rightBackground.color = currentSide == ButtonSide.Right ? selectedColor : unselectedColor;
            if (refs.leftLabel != null) refs.leftLabel.color = currentSide == ButtonSide.Left ? Color.black : Color.white;
            if (refs.rightLabel != null) refs.rightLabel.color = currentSide == ButtonSide.Right ? Color.black : Color.white;
        }
    }

    PanelRefs BuildInto(Transform panel)
    {
        // 占位文字节点本身清空——Tab 按钮本身已经写着"Settings"，面板里没必要再重复一次标题。
        Text placeholderText = panel.GetComponent<Text>();
        if (placeholderText != null) placeholderText.text = string.Empty;

        GameObject rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(panel, false);
        RectTransform rowsRect = (RectTransform)rows.transform;
        rowsRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowsRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowsRect.pivot = new Vector2(0.5f, 0.5f);
        rowsRect.anchoredPosition = Vector2.zero;
        rowsRect.sizeDelta = new Vector2(560f, 0f);

        VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rows.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        PanelRefs refs = new PanelRefs();

        refs.soundsLabel = BuildSliderRow(rows.transform, AudioManager.Instance != null ? AudioManager.Instance.SoundsVolume : 1f,
            OnSoundsChanged, out refs.soundsSlider);
        refs.musicLabel = BuildSliderRow(rows.transform, AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 1f,
            OnMusicChanged, out refs.musicSlider);
        refs.boostButtonLabel = BuildSideToggleRow(rows.transform, out refs.leftLabel, out refs.rightLabel,
            out refs.leftBackground, out refs.rightBackground);

        return refs;
    }

    void OnSoundsChanged(float value) => AudioManager.Instance?.SetSoundsVolume(value);
    void OnMusicChanged(float value) => AudioManager.Instance?.SetMusicVolume(value);

    Text BuildSliderRow(Transform parent, float initialValue, System.Action<float> onChanged, out Slider slider)
    {
        GameObject row = new GameObject("SliderRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 56f;

        Text label = CreateText(row.transform, string.Empty, 24, FontStyle.Normal, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f), new Vector2(0.38f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

        slider = BuildSlider(row.transform, new Vector2(0.4f, 0f), Vector2.one, initialValue, onChanged);

        return label;
    }

    Slider BuildSlider(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float initialValue, System.Action<float> onChanged)
    {
        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
        sliderObj.transform.SetParent(parent, false);
        RectTransform sliderRect = (RectTransform)sliderObj.transform;
        sliderRect.anchorMin = anchorMin;
        sliderRect.anchorMax = anchorMax;
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        GameObject background = new GameObject("Background", typeof(RectTransform));
        background.transform.SetParent(sliderObj.transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0f, 0.35f);
        backgroundRect.anchorMax = new Vector2(1f, 0.65f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.15f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = (RectTransform)fillArea.transform;
        fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRect.offsetMin = new Vector2(4f, 0f);
        fillAreaRect.offsetMax = new Vector2(-4f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(1f, 1f, 1f, 0.9f);

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = (RectTransform)handleArea.transform;
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = (RectTransform)handle.transform;
        handleRect.sizeDelta = new Vector2(28f, 28f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initialValue;
        slider.onValueChanged.AddListener(v => onChanged(v));

        return slider;
    }

    /// <summary>"Boost Button" 一行——左边标签，右边一对 Left/Right 分段按钮(参考图里那种
    /// 白底黑字选中、灰底白字未选中的样式)。</summary>
    Text BuildSideToggleRow(Transform parent, out Text leftLabel, out Text rightLabel, out Image leftBackground, out Image rightBackground)
    {
        GameObject row = new GameObject("SideToggleRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 56f;

        Text label = CreateText(row.transform, string.Empty, 24, FontStyle.Normal, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

        (leftBackground, leftLabel) = BuildSideButton(row.transform, new Vector2(0.5f, 0f), new Vector2(0.74f, 1f), () => SetSide(ButtonSide.Left));
        (rightBackground, rightLabel) = BuildSideButton(row.transform, new Vector2(0.76f, 0f), new Vector2(1f, 1f), () => SetSide(ButtonSide.Right));

        return label;
    }

    (Image background, Text label) BuildSideButton(Transform parent, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject("SideButton", typeof(RectTransform));
        buttonObj.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)buttonObj.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image background = buttonObj.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.12f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(onClick);

        Text label = CreateText(buttonObj.transform, string.Empty, 22, FontStyle.Normal, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        label.color = Color.white;
        label.raycastTarget = false;

        return (background, label);
    }

    Text CreateText(Transform parent, string content, int fontSize, FontStyle style, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)obj.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Text text = obj.AddComponent<Text>();
        text.font = LocalizationManager.GetFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }
}
