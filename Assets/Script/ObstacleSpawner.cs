using UnityEngine;

/// <summary>
/// 订阅 EndlessTerrainGenerator.OnSegmentGenerated,在新生成的地形段上按概率放置障碍物。
/// 障碍物是普通的实心 2D 碰撞体,摔车与否完全交给物理引擎和 CrashDetector 判定,这里不做任何脚本化的冲量。
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Rules")]
    [Range(0f, 1f)]
    [Tooltip("每个符合条件的地形段生成障碍物的概率。")]
    public float spawnChance = 0.5f;
    [Tooltip("两个障碍物之间的最小水平间距,避免连续两个挤在一起。")]
    public float minGapFromLastObstacle = 6f;
    [Tooltip("上坡不放障碍物,留作低速缓冲/救车区间。")]
    public bool skipUphill = true;

    [Header("Obstacle Shape")]
    public Vector2 obstacleSize = new Vector2(0.6f, 0.4f);
    public Color obstacleColor = new Color(0.5f, 0.35f, 0.2f);

    Material sharedMaterial;
    float lastObstacleX = float.NegativeInfinity;

    void Awake()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        sharedMaterial = new Material(shader) { color = obstacleColor };
    }

    public void HandleSegmentGenerated(Vector2 start, Vector2 end, EndlessTerrainGenerator.SegmentType type)
    {
        if (skipUphill && type == EndlessTerrainGenerator.SegmentType.Uphill) return;
        if (end.x - lastObstacleX < minGapFromLastObstacle) return;
        if (Random.value > spawnChance) return;

        float t = Random.Range(0.3f, 0.7f);
        Vector2 groundPos = Vector2.Lerp(start, end, t);
        Vector2 dir = (end - start).normalized;
        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        SpawnObstacle(groundPos, angleDeg);
        lastObstacleX = groundPos.x;
    }

    void SpawnObstacle(Vector2 groundPos, float groundAngleDeg)
    {
        GameObject obstacle = new GameObject("Obstacle");
        obstacle.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0f, 0f, groundAngleDeg));

        MeshFilter meshFilter = obstacle.AddComponent<MeshFilter>();
        meshFilter.mesh = BuildBoxMesh(obstacleSize);

        MeshRenderer meshRenderer = obstacle.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedMaterial;

        BoxCollider2D box = obstacle.AddComponent<BoxCollider2D>();
        box.size = obstacleSize;
        box.offset = new Vector2(0f, obstacleSize.y * 0.5f);
    }

    static Mesh BuildBoxMesh(Vector2 size)
    {
        float hw = size.x * 0.5f;
        Mesh mesh = new Mesh
        {
            name = "ObstacleMesh",
            vertices = new Vector3[]
            {
                new Vector3(-hw, 0f, 0f),
                new Vector3(hw, 0f, 0f),
                new Vector3(hw, size.y, 0f),
                new Vector3(-hw, size.y, 0f),
            },
            // 正反两种绕序都写入,避免猜错渲染管线的三角形环绕方向。
            triangles = new int[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 },
        };
        mesh.RecalculateBounds();
        return mesh;
    }
}
