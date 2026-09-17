using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限滚动地形:按"平地 -> 上坡 -> 下坡 -> 平地 -> ..."的顺序循环生成,
/// 每个阶段的长度、坡的高度差都可以单独配置(见 EndlessRunSettings)。
/// 阶段之间用 SmoothStep 过渡(两端导数为 0),所以任意相邻阶段衔接处都不会有尖角,
/// 地形整体是一条连续光滑的曲线,不是拼接直线段。
///
/// 断层(Gap):每次平地结束时按 gapChance 的概率不生成小山坡、改成生成一段"假谷"——
/// 陡降 -> 谷底(跨度就是 minGapSpan~maxGapSpan)-> 陡升,跟正常的坡完全复用同一套
/// SmoothStep 插值(SampleHeight),只是高度差更大、坡长更短。地形依然是一条连续曲线,
/// 碰撞体/网格都不需要真的断开——玩家跳不过去就会掉进谷底，摔车判定/HP 系统会接管，
/// 不需要额外写"掉进无底洞"这种特殊逻辑。
///
/// 挂在 Assets/prefab/Ground.prefab 上——几何体是运行时按地形曲线生成的,没法预先在
/// 编辑器里摆好,但视觉(材质/贴图)可以:MeshRenderer 上手动指定一个 Material 就会优先用它
/// (什么贴图/渐变都行),不指定的话退回运行时生成的纯色材质(用 groundColor 这个字段)。
/// </summary>
[RequireComponent(typeof(EdgeCollider2D))]
public class EndlessTerrainGenerator : MonoBehaviour
{
    public enum SlopeDirection { Flat, Uphill, Downhill, Gap }
    enum Phase { Flat, Rising, Falling, GapDrop, GapFloor, GapRise }

    [Header("Reference")]
    [Tooltip("跟随生成的目标,通常是骑行者。")]
    public Transform trackTarget;

    [Header("Generation Range")]
    public float generateAheadDistance = 50f;
    public float despawnBehindDistance = 25f;
    public float sampleSpacing = 0.4f;

    [Header("Start")]
    public float startFlatLength = 20f;
    public float groundThickness = 100f;

    [Header("平地段长度 (米)")]
    public float minFlatLength = 4f;
    public float maxFlatLength = 12f;

    [Header("上坡段长度 (米)")]
    public float minUphillLength = 15f;
    public float maxUphillLength = 35f;

    [Header("下坡段长度 (米)")]
    public float minDownhillLength = 15f;
    public float maxDownhillLength = 35f;

    [Header("坡的高度差 (米)")]
    public float minHillHeight = 2f;
    public float maxHillHeight = 5f;

    [Header("断层 (Gap)")]
    [Tooltip("每次平地结束时,有多大概率不生成小山坡、改成生成一次断层。0 = 关闭。")]
    [Range(0f, 1f)]
    public float gapChance = 0.15f;
    [Tooltip("断层的跨度(米)——玩家必须在空中飞过这段距离,否则会掉进谷底。")]
    public float minGapSpan = 3f;
    public float maxGapSpan = 6f;
    [Tooltip("断层的深度(米),要明显深到掉下去会摔车/扣血,不能只是颠簸一下。")]
    public float gapDepth = 6f;
    [Tooltip("断层两侧陡坡的长度(米),越短越接近垂直峭壁。")]
    public float gapEdgeLength = 1.5f;

    [Header("Obstacle Hook")]
    public float obstacleCheckIntervalMin = 6f;
    public float obstacleCheckIntervalMax = 12f;
    public float flatAngleThreshold = 5f;

    [Header("Collider")]
    public float edgeRadius = 0.1f;

    [Header("Visual")]
    [Tooltip("只在 Ground.prefab 的 MeshRenderer 没有手动指定材质时才会用到，直接在预制体上改。")]
    public Color groundColor = new Color(0.35f, 0.6f, 0.25f);
    [Tooltip("地表(网格顶边)的顶点色，配合支持顶点色的材质(比如 Sprites/Default)可以做出上浅下深的渐变。" +
             "材质本身的颜色要留白(白色)，不然会把顶点色再乘一遍色，混出奇怪的结果。")]
    public Color gradientTopColor = new Color(0.86f, 0.72f, 0.5f);
    [Tooltip("地表以下多深处过渡成纯色(米)。渐变只发生在地表到这个深度之间，" +
             "再往下到 groundThickness 都是纯色——groundThickness 现在很深(填满屏幕用)，" +
             "如果渐变覆盖整个深度会被拉得几乎看不出来。")]
    public float gradientDepth = 6f;
    [Tooltip("过渡完之后(以及一路到最深处)的纯色顶点色。")]
    public Color gradientBottomColor = new Color(0.25f, 0.15f, 0.12f);
    [Tooltip("颜色噪声强度，在渐变基础上给每个顶点叠加一点明暗抖动，打破纯色块的死板感。" +
             "0 = 关闭。这个值是往 RGB 三个通道加同一个偏移(只改明暗，不改色相)，" +
             "不会导致颜色发闷或者偏色。")]
    [Range(0f, 0.3f)]
    public float colorNoiseAmount = 0.05f;
    [Tooltip("颜色噪声的空间频率——值越大，噪声看起来颗粒越细碎；值越小，越接近大片柔和的明暗过渡。" +
             "噪声按顶点的世界坐标采样(Perlin Noise)，同一个位置每次重建网格都会算出同样的结果，" +
             "不会因为地形持续生成/回收而一直闪烁。")]
    public float colorNoiseScale = 0.15f;

    /// <summary>沿地形按一定间距采样时触发,供障碍物生成等系统订阅。</summary>
    public event Action<Vector2, float, SlopeDirection> OnGroundSampled;

    // 记录已生成的每一段断层的 [起点X, 终点X] 和"掉下去之前的地面高度"，供 GapFallHandler
    // 查询"车身当前 X 是不是在某个断层范围内、掉了多深"——不用碰撞体/触发区判定，见 3.11 节。
    readonly struct GapRecord
    {
        public readonly float startX, endX, groundY;
        public GapRecord(float startX, float endX, float groundY)
        {
            this.startX = startX;
            this.endX = endX;
            this.groundY = groundY;
        }
    }

    readonly List<Vector2> points = new List<Vector2>();
    readonly List<GapRecord> gaps = new List<GapRecord>();
    EdgeCollider2D edgeCollider;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

    float frontX;
    float nextObstacleCheckX;

    Phase phase;
    float phaseStartX;
    float phaseLength;
    float phaseStartHeight;
    float phaseEndHeight;
    float pendingGapStartX;
    float pendingGapGroundY;
    float pendingHeightDelta; // 小山坡的"爬升多高"、断层的"陷下去多深"共用这一个字段，用完在下一次 AdvancePhase 里原样加回来，保证海拔不漂移

    void Awake()
    {
        edgeCollider = GetComponent<EdgeCollider2D>();
        edgeCollider.edgeRadius = edgeRadius;

        // Ground.prefab 上应该已经挂好了这两个组件，这里 GetComponent 兜底一下，
        // 万一哪天不是从预制体实例化出来的（比如临时测试）也不至于直接报错。
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        mesh = new Mesh { name = "GeneratedGround" };
        meshFilter.mesh = mesh;

        // 预制体的 MeshRenderer 上手动指定了材质就用那个（贴图/渐变随便做，这里不碰）；
        // 没指定的话（sharedMaterial 是 null）退回运行时生成的纯色材质，保证没配置也能跑。
        if (meshRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            meshRenderer.sharedMaterial = new Material(shader) { color = groundColor };
        }
        meshRenderer.sortingOrder = -1;
    }

    /// <summary>用 EndlessRunSettings 资产里的数值覆盖默认参数,方便在编辑器里手调。</summary>
    public void ApplySettings(EndlessRunSettings settings)
    {
        if (settings == null) return;

        generateAheadDistance = settings.generateAheadDistance;
        despawnBehindDistance = settings.despawnBehindDistance;
        sampleSpacing = settings.sampleSpacing;
        startFlatLength = settings.startFlatLength;
        groundThickness = settings.groundThickness;
        minFlatLength = settings.minFlatLength;
        maxFlatLength = settings.maxFlatLength;
        minUphillLength = settings.minUphillLength;
        maxUphillLength = settings.maxUphillLength;
        minDownhillLength = settings.minDownhillLength;
        maxDownhillLength = settings.maxDownhillLength;
        minHillHeight = settings.minHillHeight;
        maxHillHeight = settings.maxHillHeight;
        gapChance = settings.gapChance;
        minGapSpan = settings.minGapSpan;
        maxGapSpan = settings.maxGapSpan;
        gapDepth = settings.gapDepth;
        gapEdgeLength = settings.gapEdgeLength;
        obstacleCheckIntervalMin = settings.obstacleCheckIntervalMin;
        obstacleCheckIntervalMax = settings.obstacleCheckIntervalMax;
        flatAngleThreshold = settings.flatAngleThreshold;
        edgeRadius = settings.edgeRadius;

        edgeCollider.edgeRadius = edgeRadius;
    }

    public void Initialize(Vector2 startPoint)
    {
        points.Clear();
        gaps.Clear();
        frontX = startPoint.x;
        points.Add(startPoint);

        phase = Phase.Flat;
        phaseStartX = frontX;
        phaseLength = startFlatLength;
        phaseStartHeight = startPoint.y;
        phaseEndHeight = startPoint.y;

        nextObstacleCheckX = frontX + startFlatLength + UnityEngine.Random.Range(obstacleCheckIntervalMin, obstacleCheckIntervalMax);

        while (frontX < startPoint.x + generateAheadDistance)
        {
            ExtendFront();
        }

        RebuildCollider();
        RebuildMesh();
    }

    void Update()
    {
        if (trackTarget == null || points.Count == 0) return;

        bool changed = false;
        int safetyIterations = 0;
        const int maxIterationsPerUpdate = 2000; // 200m 的余量，正常情况下远用不到——只用来防止目标位置异常跳变时死循环卡死

        while (frontX - trackTarget.position.x < generateAheadDistance)
        {
            ExtendFront();
            changed = true;

            safetyIterations++;
            if (safetyIterations >= maxIterationsPerUpdate)
            {
                Debug.LogWarning($"EndlessTerrainGenerator: 单帧生成地形段数超过安全上限，trackTarget.position.x={trackTarget.position.x}，可能发生了物理异常跳变，本帧提前结束生成。");
                break;
            }
        }

        changed |= TrimBehind(trackTarget.position.x - despawnBehindDistance);
        gaps.RemoveAll(g => g.endX < trackTarget.position.x - despawnBehindDistance);

        if (changed)
        {
            RebuildCollider();
            RebuildMesh();
        }
    }

    void ExtendFront()
    {
        frontX += sampleSpacing;

        while (frontX >= phaseStartX + phaseLength)
        {
            AdvancePhase();
        }

        float height = SampleHeight(frontX);
        points.Add(new Vector2(frontX, height));

        if (frontX >= nextObstacleCheckX)
        {
            float slopeDeg = GetSlopeAngle(frontX);
            SlopeDirection direction = IsGapPhase(phase) ? SlopeDirection.Gap : ClassifySlope(slopeDeg);
            OnGroundSampled?.Invoke(new Vector2(frontX, height), slopeDeg, direction);
            nextObstacleCheckX = frontX + UnityEngine.Random.Range(obstacleCheckIntervalMin, obstacleCheckIntervalMax);
        }
    }

    static bool IsGapPhase(Phase p) => p == Phase.GapDrop || p == Phase.GapFloor || p == Phase.GapRise;

    // 平地 -> 上坡 -> 下坡 -> 平地 -> ... 循环，或者平地 -> 断层陡降 -> 断层谷底 -> 断层陡升 -> 平地。
    // 上坡/下坡、陡降/陡升都是同一套"落回出发前的高度"逻辑(用同一个 pendingHeightDelta)，
    // 所以海拔不会累计漂移,不需要额外的海拔带修正逻辑。
    void AdvancePhase()
    {
        float nextPhaseStartX = phaseStartX + phaseLength;
        float baseHeight = phaseEndHeight;

        switch (phase)
        {
            case Phase.Flat:
                if (UnityEngine.Random.value < gapChance)
                {
                    phase = Phase.GapDrop;
                    pendingHeightDelta = gapDepth;
                    phaseLength = gapEdgeLength;
                    phaseStartHeight = baseHeight;
                    phaseEndHeight = baseHeight - pendingHeightDelta;
                    pendingGapStartX = nextPhaseStartX;
                    pendingGapGroundY = baseHeight;
                }
                else
                {
                    phase = Phase.Rising;
                    pendingHeightDelta = UnityEngine.Random.Range(minHillHeight, maxHillHeight);
                    phaseLength = UnityEngine.Random.Range(minUphillLength, maxUphillLength);
                    phaseStartHeight = baseHeight;
                    phaseEndHeight = baseHeight + pendingHeightDelta;
                }
                break;
            case Phase.Rising:
                phase = Phase.Falling;
                phaseLength = UnityEngine.Random.Range(minDownhillLength, maxDownhillLength);
                phaseStartHeight = baseHeight;
                phaseEndHeight = baseHeight - pendingHeightDelta;
                break;
            case Phase.GapDrop:
                phase = Phase.GapFloor;
                phaseLength = UnityEngine.Random.Range(minGapSpan, maxGapSpan);
                phaseStartHeight = baseHeight;
                phaseEndHeight = baseHeight; // 谷底保持水平，这一段的长度就是断层的跨度
                break;
            case Phase.GapFloor:
                phase = Phase.GapRise;
                phaseLength = gapEdgeLength;
                phaseStartHeight = baseHeight;
                phaseEndHeight = baseHeight + pendingHeightDelta; // 升回掉下去之前的高度
                break;
            default: // Falling、GapRise 结束后都回到 Flat
                if (phase == Phase.GapRise)
                {
                    gaps.Add(new GapRecord(pendingGapStartX, nextPhaseStartX, pendingGapGroundY));
                }
                phase = Phase.Flat;
                phaseLength = UnityEngine.Random.Range(minFlatLength, maxFlatLength);
                phaseStartHeight = baseHeight;
                phaseEndHeight = baseHeight;
                break;
        }

        phaseStartX = nextPhaseStartX;
    }

    float SampleHeight(float x)
    {
        float t = phaseLength > 0f ? Mathf.Clamp01((x - phaseStartX) / phaseLength) : 1f;
        float smoothT = Mathf.SmoothStep(0f, 1f, t);
        return Mathf.Lerp(phaseStartHeight, phaseEndHeight, smoothT);
    }

    float GetSlopeAngle(float x)
    {
        const float eps = 0.5f;
        float slope = (SampleHeight(x + eps) - SampleHeight(x - eps)) / (2f * eps);
        return Mathf.Atan(slope) * Mathf.Rad2Deg;
    }

    SlopeDirection ClassifySlope(float slopeDeg)
    {
        if (slopeDeg > flatAngleThreshold) return SlopeDirection.Uphill;
        if (slopeDeg < -flatAngleThreshold) return SlopeDirection.Downhill;
        return SlopeDirection.Flat;
    }

    /// <summary>查一下 x 是否落在某个已生成断层的 [起点, 终点] 范围内，是的话给出"掉下去之前
    /// 的地面高度"和断层终点 X(GapFallHandler 用后者算重新出现的落点)。断层数量任何时刻都
    /// 很少(generateAheadDistance 范围内最多几个)，线性找就够，不需要额外建索引。</summary>
    public bool TryGetGapAt(float x, out float groundY, out float gapEndX)
    {
        foreach (GapRecord gap in gaps)
        {
            if (x >= gap.startX && x <= gap.endX)
            {
                groundY = gap.groundY;
                gapEndX = gap.endX;
                return true;
            }
        }
        groundY = 0f;
        gapEndX = 0f;
        return false;
    }

    bool TrimBehind(float xThreshold)
    {
        int removeCount = 0;
        while (removeCount < points.Count - 2 && points[removeCount + 1].x < xThreshold)
        {
            removeCount++;
        }
        if (removeCount <= 0) return false;

        points.RemoveRange(0, removeCount);
        return true;
    }

    void RebuildCollider()
    {
        edgeCollider.points = points.ToArray();
    }

    // 渐变过渡段(地表 -> gradientDepth)内部再细分成几段，每段用 SmoothStep 算颜色，
    // 不是只在地表/过渡点两个顶点之间直接线性插值——纯线性插值只有 2 个点，GPU 在这两点间
    // 只能是恒定斜率，过渡段末尾突然接上后面纯色的最深段，会有一道能看出来的"折角"。
    // SmoothStep 两端导数都是 0，分的段数够多时曲线本身平滑，而且过渡段末尾(t=1)的斜率
    // 正好是 0，跟后面纯色段(斜率也是 0)平滑衔接，不会有折角。
    const int GradientSteps = 4;

    void RebuildMesh()
    {
        int n = points.Count;
        if (n < 2) return;

        int vertsPerColumn = GradientSteps + 2; // 过渡段 GradientSteps+1 个采样点 + 最深点
        int bandsPerColumn = vertsPerColumn - 1;

        Vector3[] verts = new Vector3[n * vertsPerColumn];
        Color[] colors = new Color[n * vertsPerColumn];
        for (int i = 0; i < n; i++)
        {
            Vector2 p = points[i];
            int vi = i * vertsPerColumn;

            for (int s = 0; s <= GradientSteps; s++)
            {
                float t = (float)s / GradientSteps;
                float depth = Mathf.Lerp(0f, gradientDepth, t);
                float easedT = Mathf.SmoothStep(0f, 1f, t);
                float vertexY = p.y - depth;
                verts[vi + s] = new Vector3(p.x, vertexY, 0f);
                colors[vi + s] = ApplyColorNoise(Color.Lerp(gradientTopColor, gradientBottomColor, easedT), p.x, vertexY);
            }

            float deepY = p.y - groundThickness;
            verts[vi + GradientSteps + 1] = new Vector3(p.x, deepY, 0f);
            colors[vi + GradientSteps + 1] = ApplyColorNoise(gradientBottomColor, p.x, deepY);
        }

        // 每列 vertsPerColumn 个顶点、bandsPerColumn 段，每段当成一个四边形来铺三角形。
        int[] tris = new int[(n - 1) * bandsPerColumn * 12];
        int ti = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int vi = i * vertsPerColumn;
            int viNext = vi + vertsPerColumn;
            for (int band = 0; band < bandsPerColumn; band++)
            {
                AppendQuadTriangles(tris, ref ti, vi + band, viNext + band, vi + band + 1, viNext + band + 1);
            }
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }

    // 用 Perlin Noise 按顶点的世界坐标采样，给同一个明暗偏移量加到 RGB 三个通道上(不改色相，
    // 只改明暗)。用世界坐标而不是顶点在数组里的序号，是因为地形一直在往前生成、往后回收，
    // 同一个位置不管被重建过多少次，采样结果都一样，不会出现地面纹理跟着重建过程闪烁的问题。
    Color ApplyColorNoise(Color baseColor, float worldX, float worldY)
    {
        if (colorNoiseAmount <= 0f) return baseColor;

        float noise = Mathf.PerlinNoise(worldX * colorNoiseScale, worldY * colorNoiseScale);
        float offset = (noise - 0.5f) * 2f * colorNoiseAmount;
        return new Color(
            Mathf.Clamp01(baseColor.r + offset),
            Mathf.Clamp01(baseColor.g + offset),
            Mathf.Clamp01(baseColor.b + offset),
            baseColor.a);
    }

    // 一个四边形(topLeft/topRight/bottomLeft/bottomRight 四个顶点索引)铺两个三角形，
    // 正反两种绕序都写入，避免猜错渲染管线的三角形环绕方向导致地面不可见。
    static void AppendQuadTriangles(int[] tris, ref int ti, int topLeft, int topRight, int bottomLeft, int bottomRight)
    {
        tris[ti] = topLeft; tris[ti + 1] = bottomLeft; tris[ti + 2] = topRight;
        tris[ti + 3] = topRight; tris[ti + 4] = bottomLeft; tris[ti + 5] = bottomRight;
        tris[ti + 6] = topLeft; tris[ti + 7] = topRight; tris[ti + 8] = bottomLeft;
        tris[ti + 9] = topRight; tris[ti + 10] = bottomRight; tris[ti + 11] = bottomLeft;
        ti += 12;
    }
}
