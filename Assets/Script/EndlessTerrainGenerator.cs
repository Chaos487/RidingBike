using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限滚动地形:按"平地 -> 上坡 -> 下坡 -> 平地 -> ..."的顺序循环生成,
/// 每个阶段的长度、坡的高度差都可以单独配置(见 EndlessRunSettings)。
/// 阶段之间用 SmoothStep 过渡(两端导数为 0),所以任意相邻阶段衔接处都不会有尖角,
/// 地形整体是一条连续光滑的曲线,不是拼接直线段。
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
    public float groundThickness = 3f;

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
    public Color groundColor = new Color(0.35f, 0.6f, 0.25f);

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

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "GeneratedGround" };
        meshFilter.mesh = mesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        meshRenderer.sharedMaterial = new Material(shader) { color = groundColor };
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
        groundColor = settings.groundColor;

        edgeCollider.edgeRadius = edgeRadius;
        meshRenderer.sharedMaterial.color = groundColor;
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
        while (frontX - trackTarget.position.x < generateAheadDistance)
        {
            ExtendFront();
            changed = true;
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

    void RebuildMesh()
    {
        int n = points.Count;
        if (n < 2) return;

        Vector3[] verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            Vector2 p = points[i];
            verts[i * 2] = new Vector3(p.x, p.y, 0f);
            verts[i * 2 + 1] = new Vector3(p.x, p.y - groundThickness, 0f);
        }

        // 每段两个三角形,正反两种绕序都写入,避免猜错渲染管线的三角形环绕方向导致地面不可见。
        int[] tris = new int[(n - 1) * 12];
        for (int i = 0; i < n - 1; i++)
        {
            int vi = i * 2;
            int ti = i * 12;
            tris[ti] = vi; tris[ti + 1] = vi + 2; tris[ti + 2] = vi + 1;
            tris[ti + 3] = vi + 1; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
            tris[ti + 6] = vi; tris[ti + 7] = vi + 1; tris[ti + 8] = vi + 2;
            tris[ti + 9] = vi + 1; tris[ti + 10] = vi + 3; tris[ti + 11] = vi + 2;
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }
}
