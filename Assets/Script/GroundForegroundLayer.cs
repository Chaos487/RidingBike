using UnityEngine;

/// <summary>
/// 屏幕底部常驻的前景剪影层——纯装饰,不影响碰撞/玩法,只是给画面加一层"比真地形还近"的
/// 视觉纵深(参考 Alto's Odyssey 那种沙丘剪影效果)。
///
/// 故意不用 BackgroundScroller 那套摄像机视差公式:那套的 parallaxFactor = 1 已经是"跟真地形
/// 一样快"的上限,没法做出比真地形更快的滚动——这层想显得"更近",靠的是遮挡关系(画在最前面,
/// sortingOrder 比车/地形都高)+ 常驻屏幕底部 + 颜色更深这三个视觉线索,不是滚动速度。
///
/// 每个采样点的基准高度直接查 EndlessTerrainGenerator.TryGetHeightAt(实际地形高度),不是贴着
/// 摄像机的 Y——摄像机会因为车起跳/落地/加速缩放上下晃,贴摄像机会让这层跟着一起没道理地跳动;
/// 贴真地形高度则完全不受这些影响，车跳多高这层都纹丝不动，而且天然保证"任意位置都比真地形低
/// sinkDepth 米"，不会有一段真地形比这层还低、导致这层意外"浮"到真地形上面的情况。
///
/// X 方向以摄像机当前位置为中心，覆盖宽度不是写死的常量,而是每帧从摄像机当前的
/// orthographicSize/aspect 实时算出"镜头实际看得到多宽"再加一截安全余量(edgeMargin)——
/// 车接近 Station 减速、CameraDirector 跟着变焦(低速聚焦放大/加速拉远)的时候,镜头看得到的
/// 范围会变,固定宽度早晚会在某个缩放状态下不够用,只有跟镜头实际视野绑定才能保证任何情况下
/// 都不会穿帮。halfWidth 保留作为"最小宽度"下限,真正生效的宽度取它和镜头当前需求的较大值。
/// 覆盖宽度也不能无限大,必须明显小于地形的 generateAheadDistance(默认 50m)和
/// despawnBehindDistance(默认 25m),否则采样点会落在地形还没生成/已经回收的区间——这层用的
/// 镜头缩放范围(CameraDirectorSettings 的 minOrthoSize~maxOrthoSize)决定了最大可能需要的宽度,
/// 只要镜头缩放没有离谱地超出正常范围就不会撞到这个上限。
///
/// 整个 Mesh 每帧都根据摄像机当前位置/视野重新生成(不是增量延伸/回收)——顶点数量本来就很少
/// (通常一两百个)，每帧重建的开销可以忽略，换来的是镜头状态变化时这层能立刻跟上，不用维护
/// 额外的延伸/回收状态。
/// </summary>
public class GroundForegroundLayer : MonoBehaviour
{
    [Tooltip("跟随的目标摄像机——采样窗口的中心点和实际需要覆盖多宽都从它的当前状态实时算，" +
             "不用车身,因为镜头会有 look-ahead/阻尼偏移，跟车身不完全同步。")]
    public Camera trackCamera;
    [Tooltip("查询实际地形高度用的地形生成器。")]
    public EndlessTerrainGenerator terrain;

    [Header("覆盖范围")]
    [Tooltip("左右各铺多宽(米)的下限——实际生效宽度是这个值和\"镜头当前视野 + Edge Margin\"" +
             "两者的较大值,镜头缩放变化不会让覆盖宽度跌破这个下限。")]
    public float halfWidth = 20f;
    [Tooltip("在镜头实际能看到的边缘之外，再多铺多少米(米)——防止镜头缩放/look-ahead 变化的" +
             "那一两帧里，网格边缘还没来得及跟上就已经进入可视范围。")]
    public float edgeMargin = 5f;
    [Tooltip("采样间距(米)，越小曲线越平滑，但顶点数越多。")]
    public float sampleSpacing = 1f;

    [Header("形状(独立于真地形起伏的低频噪声,叠加在真实高度之上)")]
    public float noiseScale = 0.03f;
    [Tooltip("起伏幅度(米)，波峰到基准线的最大高度。必须小于 Sink Depth，否则波峰可能反而" +
             "高过真地形在同一位置的实际高度。")]
    public float hillHeight = 2f;
    [Tooltip("网格往下延伸多深(米)，保证镜头怎么缩放都看不到底边穿帮。")]
    public float groundThickness = 60f;

    [Header("位置/外观")]
    [Tooltip("基准线比对应位置的真实地形低多少米——需要大于 Hill Height，保证这层任何时候都不会" +
             "高过真地形；具体数值需要在 Play 模式里实际盯着调,太小会跟真地形贴太近看不出层次，" +
             "太大又完全看不到。")]
    public float sinkDepth = 3f;
    public Color silhouetteColor = new Color(0.22f, 0.14f, 0.12f);
    [Tooltip("渲染排序，必须比车/障碍物(默认 0)和真地形(-1)都高，才能真的盖在最前面。")]
    public int sortingOrder = 10;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    float lastKnownGroundY;
    bool hasKnownGroundY;

    void Awake()
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "GroundForegroundMesh" };
        meshFilter.mesh = mesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        meshRenderer.sharedMaterial = new Material(shader) { color = silhouetteColor };
        meshRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>用 GroundForegroundLayerSettings 资产里的数值覆盖默认参数,方便在编辑器里手调,
    /// Play 模式下改了也不会随着退出 Play 被重置。Awake() 已经用脚本默认值把材质/渲染顺序建好了,
    /// 这里额外把这两个已经创建出来的运行时对象同步一遍，不是单纯改字段就够。</summary>
    public void ApplySettings(GroundForegroundLayerSettings settings)
    {
        if (settings == null) return;

        halfWidth = settings.halfWidth;
        edgeMargin = settings.edgeMargin;
        sampleSpacing = settings.sampleSpacing;
        noiseScale = settings.noiseScale;
        hillHeight = settings.hillHeight;
        groundThickness = settings.groundThickness;
        sinkDepth = settings.sinkDepth;
        silhouetteColor = settings.silhouetteColor;
        sortingOrder = settings.sortingOrder;

        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = sortingOrder;
            if (meshRenderer.sharedMaterial != null) meshRenderer.sharedMaterial.color = silhouetteColor;
        }
    }

    public void Initialize(Camera camera, EndlessTerrainGenerator terrainGenerator)
    {
        trackCamera = camera;
        terrain = terrainGenerator;
    }

    void LateUpdate()
    {
        if (trackCamera == null || terrain == null) return;

        // 镜头实际能看到的半宽(正交摄像机:orthographicSize 是半高，乘 aspect 换算成半宽)，
        // 加安全余量之后跟配置的下限取较大值——镜头缩放越大，这个数越大，网格自动跟着铺宽。
        float visibleHalfWidth = trackCamera.orthographicSize * trackCamera.aspect + edgeMargin;
        float effectiveHalfWidth = Mathf.Max(halfWidth, visibleHalfWidth);

        RebuildMesh(trackCamera.transform.position.x, effectiveHalfWidth);
    }

    void RebuildMesh(float centerX, float currentHalfWidth)
    {
        int columns = Mathf.Max(2, Mathf.CeilToInt(currentHalfWidth * 2f / Mathf.Max(sampleSpacing, 0.05f)) + 1);
        float startX = centerX - currentHalfWidth;

        Vector3[] verts = new Vector3[columns * 2];
        for (int i = 0; i < columns; i++)
        {
            float worldX = startX + i * sampleSpacing;

            // 查不到真实高度(地形还没生成到/已经回收)就沿用上一个采样到的高度，保持一条
            // 平的延伸，不会因为查不到就在 Mesh 上开一个洞或者塌到 0。
            if (terrain.TryGetHeightAt(worldX, out float groundY))
            {
                lastKnownGroundY = groundY;
                hasKnownGroundY = true;
            }
            else if (hasKnownGroundY)
            {
                groundY = lastKnownGroundY;
            }
            else
            {
                groundY = trackCamera.transform.position.y; // 极端兜底：开局第一帧地形可能还没来得及生成
            }

            float noise = Mathf.PerlinNoise(worldX * noiseScale, 0f);
            float topY = groundY - sinkDepth + (noise - 0.5f) * 2f * hillHeight;

            verts[i * 2] = new Vector3(worldX, topY, 0f);
            verts[i * 2 + 1] = new Vector3(worldX, topY - groundThickness, 0f);
        }

        int[] tris = new int[(columns - 1) * 12];
        int ti = 0;
        for (int i = 0; i < columns - 1; i++)
        {
            int topLeft = i * 2;
            int bottomLeft = topLeft + 1;
            int topRight = topLeft + 2;
            int bottomRight = topLeft + 3;

            // 正反两种绕序都写入，避免猜错渲染管线的三角形环绕方向导致这层不可见。
            tris[ti++] = topLeft; tris[ti++] = bottomLeft; tris[ti++] = topRight;
            tris[ti++] = topRight; tris[ti++] = bottomLeft; tris[ti++] = bottomRight;
            tris[ti++] = topLeft; tris[ti++] = topRight; tris[ti++] = bottomLeft;
            tris[ti++] = topRight; tris[ti++] = bottomRight; tris[ti++] = bottomLeft;
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }
}
