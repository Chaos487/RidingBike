using UnityEngine;

/// <summary>
/// 屏幕底部常驻的前景剪影层——纯装饰,不影响碰撞/玩法,只是给画面加一层"比真地形还近"的
/// 视觉纵深(参考 Alto's Odyssey 那种沙丘剪影效果)。
///
/// 故意不用 BackgroundScroller 那套摄像机视差公式:那套的 parallaxFactor = 1 已经是"跟真地形
/// 一样快"的上限,没法做出比真地形更快的滚动——这层想显得"更近",靠的是遮挡关系(画在最前面,
/// sortingOrder 比车/地形都高)+ 常驻屏幕底部 + 颜色更深这三个视觉线索,不是滚动速度。
///
/// 实现上更接近 EndlessTerrainGenerator(世界空间程序化 Mesh),但拿掉了碰撞体/断层/障碍物
/// 挂钩,形状用独立的低频 Perlin Noise(不读真地形的坡度数据),而且整个 Mesh 是每帧跟着摄像机
/// 当前位置重新生成的(不是只在延伸/回收时局部更新)——这样车爬坡/下坡导致摄像机 Y 变化时,
/// 这层能立刻跟着贴到镜头底部，不会因为地形起伏而在画面里飘忽不定。顶点数量本来就很少
/// (由 halfWidth/sampleSpacing 决定，通常一两百个)，每帧重建的开销可以忽略。
/// </summary>
public class GroundForegroundLayer : MonoBehaviour
{
    [Tooltip("跟随的目标,用主摄像机的 Transform——这层要贴着镜头底部，不是贴着地形本身。")]
    public Transform trackTarget;

    [Header("覆盖范围")]
    [Tooltip("以摄像机为中心，左右各铺多宽(米)。要盖住镜头在最大缩放时的可视范围，留够余量。")]
    public float halfWidth = 40f;
    [Tooltip("采样间距(米)，越小曲线越平滑，但顶点数越多。")]
    public float sampleSpacing = 1f;

    [Header("形状(独立于真地形的低频噪声曲线)")]
    public float noiseScale = 0.03f;
    [Tooltip("起伏幅度(米)，波峰到基准线的最大高度。")]
    public float hillHeight = 2f;
    [Tooltip("网格往下延伸多深(米)，保证镜头怎么缩放都看不到底边穿帮。")]
    public float groundThickness = 60f;

    [Header("位置/外观")]
    [Tooltip("基准线比摄像机中心低多少米——需要在 Play 模式里实际盯着调:太小会整层铺满屏幕挡住玩法，" +
             "太大又完全看不到，跟镜头缩放范围(CameraDirectorSettings 的 minOrthoSize~maxOrthoSize)配合着调。")]
    public float sinkDepth = 3f;
    public Color silhouetteColor = new Color(0.22f, 0.14f, 0.12f);
    [Tooltip("渲染排序，必须比车/障碍物(默认 0)和真地形(-1)都高，才能真的盖在最前面。")]
    public int sortingOrder = 10;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

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

    public void Initialize(Transform cameraTransform)
    {
        trackTarget = cameraTransform;
    }

    void LateUpdate()
    {
        if (trackTarget == null) return;
        RebuildMesh(trackTarget.position);
    }

    void RebuildMesh(Vector3 center)
    {
        int columns = Mathf.Max(2, Mathf.CeilToInt(halfWidth * 2f / Mathf.Max(sampleSpacing, 0.05f)) + 1);
        float startX = center.x - halfWidth;

        Vector3[] verts = new Vector3[columns * 2];
        for (int i = 0; i < columns; i++)
        {
            float worldX = startX + i * sampleSpacing;
            float noise = Mathf.PerlinNoise(worldX * noiseScale, 0f);
            float topY = center.y - sinkDepth + (noise - 0.5f) * 2f * hillHeight;

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
