using UnityEngine;

/// <summary>
/// 订阅 EndlessTerrainGenerator.OnGroundSampled,按概率在采样点上放置障碍物。
/// 障碍物是普通的实心 2D 碰撞体,摔车与否完全交给物理引擎和 CrashDetector 判定,这里不做任何脚本化的冲量。
/// 每个障碍物还会附带一个比实心碰撞体大一圈的触发区(NearMissDetector),用来判定"贴身擦过"。
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Rules")]
    [Range(0f, 1f)]
    [Tooltip("每个符合条件的采样点生成障碍物的概率。")]
    public float spawnChance = 0.5f;
    [Tooltip("两个障碍物之间的最小水平间距,避免连续两个挤在一起。")]
    public float minGapFromLastObstacle = 6f;
    [Tooltip("上坡不放障碍物,留作低速缓冲/救车区间。")]
    public bool skipUphill = true;

    [Header("Obstacle Shape")]
    public Vector2 obstacleSize = new Vector2(0.6f, 0.4f);
    public Color obstacleColor = new Color(0.5f, 0.35f, 0.2f);
    [Tooltip("碰撞体圆角半径。方块直角会让高速经过的轮子在棱角处被解算出巨大冲量,把悬挂拉爆,所以要把角磨圆。")]
    public float edgeRadius = 0.08f;

    [Header("Near Miss")]
    [Tooltip("贴身擦过判定区比实心碰撞体各边多出多少(米)，车轮进这个区又出去、期间没真的撞上实心碰撞体就算一次 Near Miss。")]
    public float nearMissMargin = 0.5f;

    /// <summary>任意一个障碍物判定出一次贴身擦过时触发。</summary>
    public event System.Action OnNearMiss;

    Material sharedMaterial;
    float lastObstacleX = float.NegativeInfinity;

    void Awake()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        sharedMaterial = new Material(shader) { color = obstacleColor };
    }

    /// <summary>用 EndlessRunSettings 资产里的数值覆盖默认参数,方便在编辑器里手调。</summary>
    public void ApplySettings(EndlessRunSettings settings)
    {
        if (settings == null) return;

        spawnChance = settings.obstacleSpawnChance;
        minGapFromLastObstacle = settings.obstacleMinGap;
        skipUphill = settings.skipObstaclesOnUphill;
        obstacleSize = settings.obstacleSize;
        obstacleColor = settings.obstacleColor;
        edgeRadius = settings.obstacleEdgeRadius;

        sharedMaterial.color = obstacleColor;
    }

    public void HandleGroundSampled(Vector2 groundPoint, float slopeAngleDeg, EndlessTerrainGenerator.SlopeDirection direction)
    {
        // 断层(谷底/两侧陡坡)从不放障碍物——那里本来就是要跳过去的风险点，放个障碍物
        // 要么直接埋在深坑里看不见，要么挡在陡坡上判定诡异，没有意义。
        if (direction == EndlessTerrainGenerator.SlopeDirection.Gap) return;
        if (skipUphill && direction == EndlessTerrainGenerator.SlopeDirection.Uphill) return;
        if (groundPoint.x - lastObstacleX < minGapFromLastObstacle) return;
        if (Random.value > spawnChance) return;

        SpawnObstacle(groundPoint, slopeAngleDeg);
        lastObstacleX = groundPoint.x;
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
        box.edgeRadius = edgeRadius;

        BoxCollider2D nearMissBox = obstacle.AddComponent<BoxCollider2D>();
        nearMissBox.isTrigger = true;
        nearMissBox.size = obstacleSize + new Vector2(nearMissMargin * 2f, nearMissMargin * 2f);
        nearMissBox.offset = box.offset;

        NearMissDetector detector = obstacle.AddComponent<NearMissDetector>();
        detector.OnNearMiss += () => OnNearMiss?.Invoke();
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
