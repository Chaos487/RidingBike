using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限横向滚动的一层视差背景。一个物体只管一层——要做多层视差(远景动得慢、近景动得快)
/// 就挂多份这个组件，每份指定不同的 Sprite 和 parallaxFactor，互相独立，谁也不知道谁的存在。
///
/// 图本身不是无缝贴图(左右边缘对不上),所以不用 SpriteRenderer 的 Tiled 模式,
/// 改成相邻面板交替水平镜像(flipX)拼接——镜像之后相邻两块的交界像素天生完全对称,
/// 保证不会出现生硬的接缝。
/// 面板用 Sprite 原始的 Center 或 Bottom 轴心做定位都可以，跟视差数学无关。
///
/// 视差实现:面板不是按"镜头位置 / 面板宽度"重新计算出来的固定网格位置(那样在 factor != 1
/// 时会导致面板永远追不上镜头、渐渐漂到镜头视野之外——两者的绝对世界坐标会越差越远)。
/// 改成每一帧让每块面板按 deltaX * (1 - parallaxFactor) 自己挪动一点:
/// factor=1 时这一项是 0，面板完全不动(世界固定，跟地面一样，滚动最快、感觉最近)；
/// factor=0 时面板跟镜头挪动量完全一致，相对镜头纹丝不动(感觉无限远，比如天空)。
/// 面板相对镜头漂出覆盖范围时，直接把它挪到覆盖范围另一端接着用(不销毁重建)，
/// 挪动的是奇数倍(2*panelsAheadBehind+1)的面板宽度，所以顺手把镜像状态反转一下，
/// 保证挪完之后接缝依然对称。
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    [Tooltip("背景图，一张不透明的天空+远景一体图(前景层的话可以用带透明背景的剪影图)。")]
    public Sprite backgroundSprite;
    [Tooltip("跟随的目标,推荐用主摄像机的 Transform 而不是车身——Cinemachine 的 Follow 阻尼本来就会" +
             "把车身物理位置的小幅波动/抖动平滑掉，背景跟摄像机走等于免费继承这份平滑，" +
             "不需要额外搭一个\"平滑锚点\"；摄像机的 look-ahead 偏移也会被自然带进来，背景对齐视野中心更准。")]
    public Transform trackTarget;
    [Tooltip("视差系数:1 = 跟地面一样快(世界完全固定，滚动最快，感觉最近，比如近景剪影)；" +
             "0 = 完全跟着镜头走，相对屏幕纹丝不动(感觉无限远，比如天空/太阳/月亮)。" +
             "中间值越小，这一层在画面上滚动得越慢，看起来越远——多层背景靠这个数值差异做出纵深感。")]
    [Range(0f, 1f)]
    public float parallaxFactor = 1f;
    [Tooltip("整块背景图额外放大的倍数。放大是为了保证在最大缩放(空中+加速镜头拉到最远)时，" +
             "背景的高度依然能盖满整个可视区域，不会在画面上下露出空白——需要大于等于" +
             "(镜头最大 OrthographicSize * 2) / 背景原始高度(米)。")]
    public float scale = 1.4f;
    [Tooltip("垂直方向的额外偏移(米)，背景图本身不会跟着地形起伏，只整体上下平移这一个固定值，" +
             "用来把图里的地平线大致对到地形高度——需要在 Inspector 里手动试。")]
    public float verticalOffset = 3f;
    [Tooltip("trackTarget 左右各预铺多少块面板。面板本身很宽，1 通常已经有很大余量。")]
    public int panelsAheadBehind = 1;
    [Tooltip("渲染排序，必须比地形(EndlessTerrainGenerator 用 -1)更靠后，才不会盖住地面；" +
             "多层背景之间也用这个决定谁在前谁在后，数值越小越靠后(越远)。")]
    public int sortingOrder = -10;

    float panelWorldWidth;
    float lastTrackX;
    readonly List<SpriteRenderer> panels = new List<SpriteRenderer>();

    void Start()
    {
        if (backgroundSprite == null || trackTarget == null)
        {
            enabled = false;
            return;
        }

        panelWorldWidth = backgroundSprite.bounds.size.x * scale;
        lastTrackX = trackTarget.position.x;

        float anchorY = trackTarget.position.y + verticalOffset;
        for (int i = -panelsAheadBehind; i <= panelsAheadBehind; i++)
        {
            SpawnPanel(trackTarget.position.x + i * panelWorldWidth, anchorY, i);
        }
    }

    void LateUpdate()
    {
        float deltaX = trackTarget.position.x - lastTrackX;
        lastTrackX = trackTarget.position.x;

        float ownDeltaX = deltaX * (1f - parallaxFactor);
        float anchorY = trackTarget.position.y + verticalOffset;

        foreach (SpriteRenderer panel in panels)
        {
            Vector3 pos = panel.transform.position;
            pos.x += ownDeltaX;
            pos.y = anchorY;
            panel.transform.position = pos;
        }

        RecyclePanels();
    }

    void RecyclePanels()
    {
        float camX = trackTarget.position.x;
        float totalSpan = panelWorldWidth * (panelsAheadBehind * 2 + 1);
        float behindThreshold = -panelWorldWidth * (panelsAheadBehind + 0.5f);
        float aheadThreshold = panelWorldWidth * (panelsAheadBehind + 0.5f);

        foreach (SpriteRenderer panel in panels)
        {
            float relativeX = panel.transform.position.x - camX;
            if (relativeX < behindThreshold)
            {
                MovePanel(panel, totalSpan);
            }
            else if (relativeX > aheadThreshold)
            {
                MovePanel(panel, -totalSpan);
            }
        }
    }

    void MovePanel(SpriteRenderer panel, float deltaX)
    {
        Vector3 pos = panel.transform.position;
        pos.x += deltaX;
        panel.transform.position = pos;

        // 挪动的是奇数倍(2*panelsAheadBehind+1)面板宽度，在整条无限拼接序列里的奇偶性必然反转，
        // 镜像状态跟着反转才能让接缝继续对称——沿用旧的镜像会导致挪完之后跟新邻居同侧不对称。
        panel.flipX = !panel.flipX;
    }

    void SpawnPanel(float worldX, float worldY, int index)
    {
        GameObject go = new GameObject($"BackgroundPanel_{panels.Count}");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(worldX, worldY, 0f);
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = backgroundSprite;
        sr.flipX = index % 2 != 0; // 奇偶交替镜像，让相邻面板的接缝左右对称
        sr.sortingOrder = sortingOrder;

        panels.Add(sr);
    }
}
