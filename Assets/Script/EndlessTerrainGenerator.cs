using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限滚动地形:按"平地 -> 上坡 -> 下坡 -> 平地 -> ..."的顺序循环生成,
/// 每个阶段的长度、坡的高度差都可以单独配置(见 EndlessRunSettings)。
/// 阶段之间用 SmoothStep 过渡(两端导数为 0),所以任意相邻阶段衔接处都不会有尖角,
/// 地形整体是一条连续光滑的曲线,不是拼接直线段。
///
/// 挂在 Assets/prefab/Ground.prefab 上——几何体是运行时按地形曲线生成的,没法预先在
/// 编辑器里摆好,但视觉(材质/贴图)可以:MeshRenderer 上手动指定一个 Material 就会优先用它
/// (什么贴图/渐变都行),不指定的话退回运行时生成的纯色材质(用 groundColor 这个字段)。
/// </summary>
[RequireComponent(typeof(EdgeCollider2D))]
public class EndlessTerrainGenerator : MonoBehaviour
{
    public enum SlopeDirection { Flat, Uphill, Downhill }
    enum Phase { Flat, Rising, Falling }

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

    /// <summary>沿地形按一定间距采样时触发,供障碍物生成等系统订阅。</summary>
    public event Action<Vector2, float, SlopeDirection> OnGroundSampled;

    readonly List<Vector2> points = new List<Vector2>();
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
    float pendingHillHeight;

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
        obstacleCheckIntervalMin = settings.obstacleCheckIntervalMin;
        obstacleCheckIntervalMax = settings.obstacleCheckIntervalMax;
        flatAngleThreshold = settings.flatAngleThreshold;
        edgeRadius = settings.edgeRadius;

        edgeCollider.edgeRadius = edgeRadius;
    }

    public void Initialize(Vector2 startPoint)
    {
        points.Clear();
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
            SlopeDirection direction = ClassifySlope(slopeDeg);
            OnGroundSampled?.Invoke(new Vector2(frontX, height), slopeDeg, direction);
            nextObstacleCheckX = frontX + UnityEngine.Random.Range(obstacleCheckIntervalMin, obstacleCheckIntervalMax);
        }
    }

    // 平地 -> 上坡 -> 下坡 -> 平地 -> ... 循环。下坡总是落回上坡爬升前的高度(用同一个
    // pendingHillHeight),所以海拔不会累计漂移,不需要额外的海拔带修正逻辑。
    void AdvancePhase()
    {
        float nextPhaseStartX = phaseStartX + phaseLength;
        float baseHeight = phaseEndHeight;

        switch (phase)
        {
            case Phase.Flat:
                phase = Phase.Rising;
                pendingHillHeight = UnityEngine.Random.Range(minHillHeight, maxHillHeight);
                phaseLength = UnityEngine.Random.Range(minUphillLength, maxUphillLength);
                phaseStartHeight = baseHeight;
                phaseEndHeight = baseHeight + pendingHillHeight;
                break;
            case Phase.Rising:
                phase = Phase.Falling;
                phaseLength = UnityEngine.Random.Range(minDownhillLength, maxDownhillLength);
                phaseStartHeight = baseHeight;
                phaseEndHeight = baseHeight - pendingHillHeight;
                break;
            default:
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
                verts[vi + s] = new Vector3(p.x, p.y - depth, 0f);
                colors[vi + s] = Color.Lerp(gradientTopColor, gradientBottomColor, easedT);
            }

            verts[vi + GradientSteps + 1] = new Vector3(p.x, p.y - groundThickness, 0f);
            colors[vi + GradientSteps + 1] = gradientBottomColor;
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
