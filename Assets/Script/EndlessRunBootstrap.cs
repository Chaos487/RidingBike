using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时自动装配 endless run 所需的系统,不依赖手动编辑场景文件:
/// 场景加载后自动找到 Bike,停用场景里原来那块静态地面,
/// 在同一个高度接上程序化生成的无限地形 + 障碍物 + 摔车判定 + 结算 UI。
/// [RuntimeInitializeOnLoadMethod] 只在应用启动时触发一次,RunManager 重开是靠
/// SceneManager.LoadScene 重载同一个场景,所以额外订阅 sceneLoaded 让每次重开都能重新装配。
/// </summary>
public static class EndlessRunBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded += (scene, mode) => Setup();
        Setup();
    }

    static void Setup()
    {
        BikeController bike = Object.FindFirstObjectByType<BikeController>();
        if (bike == null) return;

        float startY = bike.transform.position.y - 1.5f;
        GameObject oldGround = GameObject.Find("ground");
        if (oldGround != null)
        {
            Collider2D col = oldGround.GetComponent<Collider2D>();
            if (col != null) startY = col.bounds.max.y;
            oldGround.SetActive(false);
        }

        Vector2 startPoint = new Vector2(bike.transform.position.x - 5f, startY);

        GameObject systems = new GameObject("EndlessRunSystems");

        EndlessTerrainGenerator terrain = systems.AddComponent<EndlessTerrainGenerator>();
        terrain.trackTarget = bike.transform;

        ObstacleSpawner obstacleSpawner = systems.AddComponent<ObstacleSpawner>();
        terrain.OnSegmentGenerated += obstacleSpawner.HandleSegmentGenerated;

        terrain.Initialize(startPoint);

        CrashDetector crashDetector = systems.AddComponent<CrashDetector>();
        crashDetector.bikeRigidbody = bike.bikeRigidbody != null ? bike.bikeRigidbody : bike.GetComponent<Rigidbody2D>();
        crashDetector.groundCheckDistance = bike.groundCheckDistance;
        crashDetector.groundLayer = bike.groundLayer;

        RunManager runManager = systems.AddComponent<RunManager>();
        runManager.Initialize(bike.transform, crashDetector, bike);
    }
}
