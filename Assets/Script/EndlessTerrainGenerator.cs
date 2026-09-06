using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EdgeCollider2D))]
public class EndlessTerrainGenerator : MonoBehaviour
{
    public enum SegmentType { Flat, Uphill, Downhill }

    [Header("Reference")]
    [Tooltip("跟随生成的目标,通常是骑行者。")]
    public Transform trackTarget;

    [Header("Generation Range")]
    [Tooltip("目标前方保持多远的已生成地形。")]
    public float generateAheadDistance = 40f;
    [Tooltip("目标身后超过这个距离的地形会被回收。")]
    public float despawnBehindDistance = 25f;

    [Header("Start")]
    [Tooltip("起点前方的安全平地长度,避免一开始就上坡/下坡。")]
    public float startFlatLength = 18f;
    [Tooltip("地面视觉网格的厚度。")]
    public float groundThickness = 3f;

    [Header("Segment Length")]
    public float minSegmentLength = 5f;
    public float maxSegmentLength = 12f;

    [Header("Slope Angles (deg)")]
    public float minSlopeAngle = 8f;
    public float maxSlopeAngle = 20f;

    [Header("Weights")]
    public float flatWeight = 1f;
    public float uphillWeight = 1f;
    public float downhillWeight = 1f;
    [Tooltip("同方向坡度最多连续出现几段,超过后强制切换方向,避免地形无限爬升/下沉。")]
    public int maxConsecutiveSameDirection = 2;

    [Header("Smoothing")]
    [Tooltip("相邻两段坡度角最多能变化多少度。限制这个值可以避免陡下坡紧接陡上坡这种尖锐 V 形坑/尖峰——轮子撞进这种没有过渡的尖角会被物理引擎解算出巨大冲量,把悬挂瞬间拉爆、轮子看起来像飞出去了。")]
    public float maxAngleChangePerSegment = 14f;
    [Tooltip("地面碰撞体的圆角半径,给尖角再加一层缓冲。")]
    public float edgeRadius = 0.1f;

    [Header("Elevation Band (相对起点)")]
    public float minElevation = -5f;
    public float maxElevation = 3f;

    [Header("Visual")]
    public Color groundColor = new Color(0.35f, 0.6f, 0.25f);

    /// <summary>每生成一段新地形时触发,供障碍物生成等系统订阅。</summary>
    public event Action<Vector2, Vector2, SegmentType> OnSegmentGenerated;

    readonly List<Vector2> points = new List<Vector2>();
    EdgeCollider2D edgeCollider;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

    Vector2 cursor;
    float originY;
    SegmentType lastType = SegmentType.Flat;
    int consecutiveCount;
    float lastAngleDeg;

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

    public void Initialize(Vector2 startPoint)
    {
        points.Clear();
        cursor = startPoint;
        originY = startPoint.y;
        lastType = SegmentType.Flat;
        lastAngleDeg = 0f;
        consecutiveCount = 0;
        points.Add(cursor);

        Vector2 flatEnd = cursor + Vector2.right * startFlatLength;
        points.Add(flatEnd);
        cursor = flatEnd;

        while (cursor.x < startPoint.x + generateAheadDistance)
        {
            GenerateNextSegment();
        }

        RebuildCollider();
        RebuildMesh();
    }

    void Update()
    {
        if (trackTarget == null || points.Count == 0) return;

        bool changed = false;
        while (cursor.x - trackTarget.position.x < generateAheadDistance)
        {
            GenerateNextSegment();
            changed = true;
        }

        changed |= TrimBehind(trackTarget.position.x - despawnBehindDistance);

        if (changed)
        {
            RebuildCollider();
            RebuildMesh();
        }
    }

    void GenerateNextSegment()
    {
        SegmentType type = PickNextType();
        float length = UnityEngine.Random.Range(minSegmentLength, maxSegmentLength);

        (Vector2 end, float angleDeg) = ComputeSegmentEnd(type, length);

        float relativeElevation = end.y - originY;
        if (relativeElevation > maxElevation && type != SegmentType.Downhill)
        {
            type = SegmentType.Downhill;
            (end, angleDeg) = ComputeSegmentEnd(type, length);
        }
        else if (relativeElevation < minElevation && type != SegmentType.Uphill)
        {
            type = SegmentType.Uphill;
            (end, angleDeg) = ComputeSegmentEnd(type, length);
        }

        Vector2 start = cursor;
        points.Add(end);
        cursor = end;
        lastAngleDeg = angleDeg;

        consecutiveCount = (type == lastType && type != SegmentType.Flat) ? consecutiveCount + 1 : 1;
        lastType = type;

        OnSegmentGenerated?.Invoke(start, end, type);
    }

    (Vector2 end, float angleDeg) ComputeSegmentEnd(SegmentType type, float length)
    {
        float targetAngleDeg = type switch
        {
            SegmentType.Uphill => UnityEngine.Random.Range(minSlopeAngle, maxSlopeAngle),
            SegmentType.Downhill => -UnityEngine.Random.Range(minSlopeAngle, maxSlopeAngle),
            _ => 0f,
        };

        // 限制相对上一段的角度变化,避免陡下坡紧接陡上坡这类没有过渡的尖角。
        float angleDeg = Mathf.Clamp(targetAngleDeg, lastAngleDeg - maxAngleChangePerSegment, lastAngleDeg + maxAngleChangePerSegment);

        Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
        return (cursor + dir * length, angleDeg);
    }

    SegmentType PickNextType()
    {
        float fw = flatWeight;
        float uw = uphillWeight;
        float dw = downhillWeight;

        if (consecutiveCount >= maxConsecutiveSameDirection)
        {
            if (lastType == SegmentType.Uphill) uw = 0f;
            else if (lastType == SegmentType.Downhill) dw = 0f;
        }

        float total = fw + uw + dw;
        float r = UnityEngine.Random.Range(0f, total);
        if (r < fw) return SegmentType.Flat;
        r -= fw;
        return r < uw ? SegmentType.Uphill : SegmentType.Downhill;
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
