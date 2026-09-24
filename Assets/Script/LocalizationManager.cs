using System;
using UnityEngine;

/// <summary>只能往末尾加——存进 PlayerPrefs 的是这个枚举的序号(ordinal)，插中间会让玩家
/// 设备上已经存的语言选择读出来变成另一种语言，这个项目里其它枚举(NodeSettings.EffectType
/// 等)也是同样的规矩。</summary>
public enum Locale
{
    English,
    SimplifiedChinese,
    TraditionalChinese,
    Japanese,
    German,
    French,
    Spanish,
}

/// <summary>
/// 当前语言 + 翻译查表入口。纯静态类，不挂在任何 GameObject 上(不需要生命周期，跟
/// `ScreenBlurState` 是同一类"全局状态"，只是这个会主动持久化到 PlayerPrefs)。
///
/// v1 范围只覆盖"常驻 UI"——Tab 栏/Menu/暂停面板按钮、Settings/Run Summary/Goals/Stats
/// 这几个面板的固定文案，大概 40 条 key(具体内容在 `LocalizationTable.cs`)。骑行中右侧
/// Feat 列表的弹字(PERFECT!/NEAR MISS!/Backflip x{n})、Goal 具体目标标题(数据来自
/// `GoalSettings.asset`)、Node 三选一文案都还没接，继续显示英文，等以后单独扩。
/// </summary>
public static class LocalizationManager
{
    const string LocaleKey = "RidingBike_Locale";

    public static Locale CurrentLocale { get; private set; } = Locale.English;

    /// <summary>语言切换时触发——Goals/Stats/Settings 这几个 Tab 可能在同一次打开菜单期间
    /// 被切换过语言(先去 Language Tab 选一个，再切回 Goals Tab)，订阅这个事件才能立刻刷新，
    /// 不用等下次重新打开面板。</summary>
    public static event Action OnLocaleChanged;

    public static void Initialize()
    {
        int saved = PlayerPrefs.GetInt(LocaleKey, (int)Locale.English);
        CurrentLocale = Enum.IsDefined(typeof(Locale), saved) ? (Locale)saved : Locale.English;
    }

    public static void SetLocale(Locale locale)
    {
        if (CurrentLocale == locale) return;

        CurrentLocale = locale;
        PlayerPrefs.SetInt(LocaleKey, (int)locale);
        PlayerPrefs.Save();

        OnLocaleChanged?.Invoke();
    }

    /// <summary>按当前语言查一条文案——key 查不到就原样返回 key 本身(方便一眼看出哪条忘了
    /// 加翻译，而不是空白或者报错崩掉)。</summary>
    public static string Get(string key) => LocalizationTable.Lookup(key, CurrentLocale);

    /// <summary>项目里所有运行时搭 UI 的地方(RunSummaryUI/GoalsUIUtil/StatsUIUtil/
    /// SettingsTabUI/LanguageTabUI 的 CreateText，以及 TabGroupController/MainMenuController/
    /// PauseController 改预制体里已有 Text 组件的地方)都应该用这个方法拿字体，不要再直接写
    /// `Resources.GetBuiltinResource&lt;Font&gt;("LegacyRuntime.ttf")`——内置的 Arial 没有
    /// 中文/日文字形，简体中文/繁體中文/日本語这三种语言选中之后，用 Arial 画出来的文字会是
    /// 一片空白方框。
    ///
    /// **需要手动配置**：Unity 内置字体不含 CJK 字形，这件事没法用代码绕过去，必须往项目里
    /// 加一份真正带中日文字形的字体资产(比如 Noto Sans CJK/思源黑体，免费可商用)，导入后
    /// 放到 `Assets/Resources/NotoSansCJK.ttf`(或者改这个方法里的名字对上实际文件名)。
    /// 在字体资产加进来之前，这个方法会退回内置 Arial——英文/数字显示完全正常，只有
    /// 中文/日文文字会是空白方框，不会报错崩溃。</summary>
    public static Font GetFont() => Resources.Load<Font>("NotoSansCJK") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
}
