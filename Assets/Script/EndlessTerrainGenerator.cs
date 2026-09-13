using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限滚动地形,用正弦波叠加生成一条连续光滑的地形曲线(类似《滑雪大冒险》的做法),
/// 而不是随机拼接直线段——这样天然没有衔接尖角,长长的上/下坡是连续的一整条曲线。
/// </summary>
[RequireComponent(typeof(EdgeCollider2D))]
public class EndlessTerrainGenerator : MonoBehaviour
{
    public enum SlopeDirection { Flat, Uphill, Downhill }

    [Serializable]
    public struct Wave
    {
        public float wavelength;
        public float amplitude;
    }

    [Header("Reference")]
    [Tooltip("跟随生成的目标,通常是骑行者。")]
    public Transform trackTarget;

    [Header("Generation Range")]
    [Tooltip("目标前方保持多远的已生成地形。")]
    public float generateAheadDistance = 50f;
    [Tooltip("目标身后超过这个距离的地形会被回收。")]
    public float despawnBehindDistance = 25f;
    [Tooltip("地形采样点间距,越小曲线越平滑,但点数越多。")]
    public float sampleSpacing = 0.4f;

    [Header("Start")]
    [Tooltip("起点前方的安全过渡长度:坡度振幅会在这段距离内从 0 平滑过渡到完整幅度,不是硬切的平地。")]
    public float startFlatLength = 20f;
    [Tooltip("地面视觉网格的厚度。")]
    public float groundThickness = 3f;

    [Header("Rolling Hills")]
    [Tooltip("主波:决定又长又连续的大坡度起伏,波长越大坡越长、越平缓。")]
    public Wave primaryWave = new Wave { wavelength = 80f, amplitude = 4.5f };
    [Tooltip("次波:叠加在主波上增加细节变化,幅度明显小于主波,否则会打散主波的连续感。")]
    public Wave secondaryWave = new Wave { wavelength = 24f, amplitude = 1f };

    [Header("Obstacle Hook")]
    [Tooltip("大约每隔多远对地形采样一次,供障碍物生成使用。")]
    public float obstacleCheckIntervalMin = 6f;
    public float obstacleCheckIntervalMax = 12f;
    [Tooltip("坡度角小于这个值(度)视为平地。")]
    public float flatAngleThreshold = 5f;

    [Header("Collider")]
    [Tooltip("地面碰撞体的圆角半径,给极少数陡峭处再加一层缓冲。")]
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

    float startX;
    float startY;
    float frontX;
    float nextObstacleCheckX;
    float primaryPhase;
    float secondaryPhase;

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
        startX = startPoint.x;
        startY = startPoint.y;
        primaryPhase = UnityEngine.Random.Range(0f, 1000f);
        secondaryPhase = UnityEngine.Random.Range(0f, 1000f);

        points.Clear();
        frontX = startX;
        points.Add(new Vector2(frontX, ComputeHeight(frontX)));

        nextObstacleCheckX = startX + startFlatLength + UnityEngine.Random.Range(obstacleCheckIntervalMin, obstacleCheckIntervalMax);

        while (frontX < startX + generateAheadDistance)
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
        points.Add(new Vector2(frontX, ComputeHeight(frontX)));

        if (frontX >= nextObstacleCheckX)
        {
            float slopeDeg = GetSlopeAngle(frontX);
            SlopeDirection direction = ClassifySlope(slopeDeg);
            OnGroundSampled?.Invoke(new Vector2(frontX, ComputeHeight(frontX)), slopeDeg, direction);
            nextObstacleCheckX = frontX + UnityEngine.Random.Range(obstacleCheckIntervalMin, obstacleCheckIntervalMax);
        }
    }

    float ComputeHeight(float x)
    {
        float local = x - startX;
        float ramp = Mathf.SmoothStep(0f, 1f, local / Mathf.Max(startFlatLength, 0.01f));

        float h = WaveHeight(primaryWave, local, primaryPhase) + WaveHeight(secondaryWave, local, secondaryPhase);
        return startY + h * ramp;
    }

    static float WaveHeight(Wave wave, float x, float phase)
    {
        if (wave.wavelength <= 0f) return 0f;
        float angularFreq = 2f * Mathf.PI / wave.wavelength;
        return wave.amplitude * Mathf.Sin(x * angularFreq + phase);
    }

    float GetSlopeAngle(float x)
    {
        const float eps = 0.5f;
        float slope = (ComputeHeight(x + eps) - ComputeHeight(x - eps)) / (2f * eps);
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
