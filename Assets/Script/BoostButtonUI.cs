using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 加速按钮的充能可视化——环形进度条随 `BikeController.DistanceSinceLastBoost` 慢慢描满，
/// 充满的瞬间闪一下。挂在 `BoostButton` 这个 GameObject 上(`EndlessRunCanvas.prefab`)，
/// 是个可选组件——没挂的话 `RunManager` 那边什么都不会做，原来 `boostReadyColor`/
/// `boostNotReadyColor` 那套纯色切换完全不受影响，两套视觉可以同时叠加。
///
/// 跟这个项目其它 UI 不一样的地方：`fillImage`/`flashImage` 这两个子物体是**在 Editor 里
/// 手摆的**，不是运行时代码生成的——想要能直接在 Inspector 里调颜色/位置/大小反复试效果，
/// 不用每次改代码，所以这里破例用 `[SerializeField]` 拖引用，而不是这个项目其它地方统一用的
/// `transform.Find(名字)` 运行时查找。手摆步骤：
///
/// 1. 在 `BoostButton` 下新建一个子物体 `Fill`，加 `Image` 组件，`Image Type` 设成
///    `Filled`，`Fill Method` 设成 `Radial 360`(预制体里 `BoostButton` 自己的 Image 组件
///    其实早就把 `Fill Method` 设过一次了，只是没切 Type，这次直接照抄用上)，`Fill Origin`
///    建议 `Top`，勾 `Clockwise`——颜色/RectTransform 大小位置随便调，这就是"充能颜色"。
/// 2. 再建一个子物体 `Flash`，同样加 `Image`，叠在 `Fill` 上面(层级顺序在它下面，
///    渲染在它前面)，初始透明度调成 0(颜色 Alpha=0)——这就是"充满瞬间"要闪的那个颜色，
///    平时看不见。
/// 3. 在 `BoostButton` 上加这个 `BoostButtonUI` 组件，把上面两个 Image 拖进
///    `Fill Image`/`Flash Image` 两个槽位。
/// </summary>
public class BoostButtonUI : MonoBehaviour
{
    [Header("充能环——Image Type = Filled, Fill Method = Radial 360")]
    [SerializeField] Image fillImage;

    [Header("充满瞬间的闪光——叠在充能环上面，平时 Alpha = 0")]
    [SerializeField] Image flashImage;

    [Header("闪光动效")]
    [SerializeField] float flashFadeInDuration = 0.08f;
    [SerializeField] float flashFadeOutDuration = 0.35f;
    [SerializeField] float flashPunchScale = 1.25f;

    BikeController bike;
    bool wasReady;

    public void Initialize(BikeController bikeController)
    {
        bike = bikeController;
        wasReady = bike != null && bike.IsBoostReady;

        if (fillImage != null) fillImage.fillAmount = wasReady ? 1f : 0f;

        if (flashImage != null)
        {
            flashImage.rectTransform.localScale = Vector3.one;
            Color c = flashImage.color;
            c.a = 0f;
            flashImage.color = c;
        }
    }

    void Update()
    {
        if (bike == null) return;

        // 充能进度现算，不是 BikeController 自己存好的一个值——跟 RunManager 里
        // `DistanceUntilBoostReady` 文字提示读的是同一套底层数据(DistanceSinceLastBoost/
        // boostRechargeDistance)，没有新增 BikeController 的公开 API。
        if (fillImage != null)
        {
            float progress = bike.boostRechargeDistance > 0f
                ? Mathf.Clamp01(bike.DistanceSinceLastBoost / bike.boostRechargeDistance)
                : 1f;
            fillImage.fillAmount = progress;
        }

        bool ready = bike.IsBoostReady;
        if (ready && !wasReady) PlayReadyFlash();
        wasReady = ready;
    }

    /// <summary>只在"从没就绪变成就绪"这一帧触发一次，不是每帧都判断 IsBoostReady 本身——
    /// 不然只要保持就绪状态(玩家迟迟不点 Boost)就会每帧重新触发一次动画，把 flashImage
    /// 焊死在全不透明状态，而不是真的"闪一下"。</summary>
    void PlayReadyFlash()
    {
        if (flashImage == null) return;

        flashImage.rectTransform.DOKill();
        flashImage.DOKill();

        flashImage.rectTransform.localScale = Vector3.one;
        Color c = flashImage.color;
        c.a = 1f;
        flashImage.color = c;

        Sequence seq = DOTween.Sequence();
        seq.Join(flashImage.rectTransform.DOScale(flashPunchScale, flashFadeInDuration).SetEase(Ease.OutQuad));
        seq.Join(flashImage.DOFade(1f, flashFadeInDuration));
        seq.Append(flashImage.DOFade(0f, flashFadeOutDuration));
        seq.Join(flashImage.rectTransform.DOScale(1f, flashFadeOutDuration));
    }
}
