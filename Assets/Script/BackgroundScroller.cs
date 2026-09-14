using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限横向滚动背景:把背景图当成一块块面板,沿 trackTarget 前方持续铺、身后自动回收。
/// 图本身不是无缝贴图(左右边缘对不上),所以不用 SpriteRenderer 的 Tiled 模式,
/// 改成相邻面板交替水平镜像(flipX)拼接——镜像之后相邻两块的交界像素天生完全对称,
/// 保证不会出现生硬的接缝,代价是画面会看出"翻转重复"的痕迹,先能用为主,
/// 不是最终美术效果(见设计文档第 19 节，正式美术留到 P3)。
/// 面板用 Sprite 原始的 Center 轴心(0.5, 0.5)做定位,所以贴图导入必须是 Single 模式、
/// Center 对齐——bg1.jpeg 已经改过来了。
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    [Tooltip("背景图,一张不透明的天空+远景一体图。")]
    public Sprite backgroundSprite;
    [Tooltip("跟随的目标,通常是 Bike——镜头本来就是跟它走的，直接用它避免额外的更新时序问题。")]
    public Transform trackTarget;
    [Tooltip("整块背景图额外放大的倍数。放大是为了保证在最大缩放(空中+加速镜头拉到最远)时，" +
             "背景的高度依然能盖满整个可视区域，不会在画面上下露出空白——需要大于等于" +
             "(镜头最大 OrthographicSize * 2) / 背景原始高度(米)。")]
    public float scale = 1.4f;
    [Tooltip("垂直方向的额外偏移(米)，背景图本身不会跟着地形起伏，只整体上下平移这一个固定值，" +
             "用来把图里的地平线大致对到地形高度——需要在 Inspector 里手动试。")]
    public float verticalOffset = 3f;
    [Tooltip("trackTarget 左右各预铺多少块面板。面板本身很宽，1 通常已经有很大余量。")]
    public int panelsAheadBehind = 1;
    [Tooltip("渲染排序，必须比地形(EndlessTerrainGenerator 用 -1)更靠后，才不会盖住地面。")]
    public int sortingOrder = -10;

    float panelWorldWidth;
    float anchorY;
    readonly Dictionary<int, SpriteRenderer> panels = new Dictionary<int, SpriteRenderer>();

    void Start()
    {
        if (backgroundSprite == null || trackTarget == null)
        {
            enabled = false;
            return;
        }

        panelWorldWidth = backgroundSprite.bounds.size.x * scale;
        anchorY = trackTarget.position.y + verticalOffset;

        RefreshPanels();
    }

    void LateUpdate()
    {
        anchorY = trackTarget.position.y + verticalOffset;
        RefreshPanels();
    }

    void RefreshPanels()
    {
        int centerIndex = Mathf.RoundToInt(trackTarget.position.x / panelWorldWidth);

        for (int i = centerIndex - panelsAheadBehind; i <= centerIndex + panelsAheadBehind; i++)
        {
            if (panels.TryGetValue(i, out SpriteRenderer existing))
            {
                Vector3 pos = existing.transform.position;
                pos.y = anchorY;
                existing.transform.position = pos;
            }
            else
            {
                SpawnPanel(i);
            }
        }

        List<int> toRemove = null;
        foreach (KeyValuePair<int, SpriteRenderer> kv in panels)
        {
            if (kv.Key < centerIndex - panelsAheadBehind || kv.Key > centerIndex + panelsAheadBehind)
            {
                (toRemove ??= new List<int>()).Add(kv.Key);
            }
        }
        if (toRemove != null)
        {
            foreach (int index in toRemove)
            {
                Destroy(panels[index].gameObject);
                panels.Remove(index);
            }
        }
    }

    void SpawnPanel(int index)
    {
        GameObject go = new GameObject($"BackgroundPanel_{index}");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(index * panelWorldWidth, anchorY, 0f);
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = backgroundSprite;
        sr.flipX = index % 2 != 0; // 奇偶交替镜像，让相邻面板的接缝左右对称
        sr.sortingOrder = sortingOrder;

        panels[index] = sr;
    }
}
