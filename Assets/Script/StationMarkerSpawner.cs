using UnityEngine;

/// <summary>
/// Station 的世界可见标记——占位版,一根旗杆 + 三角旗,纯色 Mesh(跟 ObstacleSpawner 同一套手法)。
/// 正式美术阶段直接换这里生成的 Mesh/贴图,不影响 NodeManager 那边的调用方式。
///
/// 地形是按需要往前生成的(EndlessTerrainGenerator.generateAheadDistance),NodeManager 提前
/// 排下一个 Station 的 X 坐标时,那段地形往往还没生成出来,查不到地面高度——所以这里不是
/// "收到请求立刻生成",而是每帧轮询地形有没有生成到目标 X,生成到了才摆标记贴地。
/// </summary>
public class StationMarkerSpawner : MonoBehaviour
{
    EndlessTerrainGenerator terrain;
    GameObject currentMarker;
    Material sharedMaterial;

    float pendingX;
    bool waitingForSpawn;

    void Awake()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        sharedMaterial = new Material(shader) { color = new Color(0.95f, 0.75f, 0.15f) };
    }

    public void Initialize(EndlessTerrainGenerator terrainGenerator)
    {
        terrain = terrainGenerator;
    }

    /// <summary>登记下一个 Station 的 X 坐标——地形生成到这里之后会自动贴地摆一个标记,
    /// 之前登记的那个标记(上一个已经走过的 Station)会被替换掉。</summary>
    public void RequestMarkerAt(float x)
    {
        pendingX = x;
        waitingForSpawn = true;
    }

    void Update()
    {
        if (!waitingForSpawn || terrain == null) return;

        if (terrain.TryGetHeightAt(pendingX, out float groundY))
        {
            SpawnMarker(pendingX, groundY);
            waitingForSpawn = false;
        }
    }

    void SpawnMarker(float x, float groundY)
    {
        if (currentMarker != null) Destroy(currentMarker);

        GameObject marker = new GameObject("StationMarker");
        marker.transform.position = new Vector2(x, groundY);

        MeshFilter meshFilter = marker.AddComponent<MeshFilter>();
        meshFilter.mesh = BuildFlagMesh();

        MeshRenderer meshRenderer = marker.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedMaterial;
        meshRenderer.sortingOrder = 1; // 盖在地面 Mesh(sortingOrder -1)上面

        currentMarker = marker;
    }

    static Mesh BuildFlagMesh()
    {
        const float poleHalfWidth = 0.08f;
        const float poleHeight = 3f;
        const float flagWidth = 1.1f;
        const float flagBottom = 2.2f;
        const float flagTop = 2.9f;

        Vector3[] verts =
        {
            // 旗杆(矩形,4 顶点)
            new Vector3(-poleHalfWidth, 0f, 0f),
            new Vector3(poleHalfWidth, 0f, 0f),
            new Vector3(poleHalfWidth, poleHeight, 0f),
            new Vector3(-poleHalfWidth, poleHeight, 0f),
            // 三角旗(3 顶点,挂在旗杆右侧上方)
            new Vector3(poleHalfWidth, flagTop, 0f),
            new Vector3(poleHalfWidth, flagBottom, 0f),
            new Vector3(poleHalfWidth + flagWidth, (flagTop + flagBottom) * 0.5f, 0f),
        };

        int[] tris =
        {
            // 正反两种绕序都写入,避免猜错渲染管线的三角形环绕方向。
            0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 5, 6,
        };

        Mesh mesh = new Mesh { name = "StationMarkerMesh", vertices = verts, triangles = tris };
        mesh.RecalculateBounds();
        return mesh;
    }
}
