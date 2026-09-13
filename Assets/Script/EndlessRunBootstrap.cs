using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时自动装配 endless run 所需的系统,不依赖手动编辑场景文件:
/// 场景加载后自动找到 Bike,停用场景里原来那块静态地面,
/// 在同一个高度接上程序化生成的无限地形 + 障碍物 + 摔车判定 + 结算 UI + 速度反应式镜头。
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

        EndlessRunSettings runSettings = FindSettings<EndlessRunSettings>();

        GameObject systems = new GameObject("EndlessRunSystems");

        EndlessTerrainGenerator terrain = systems.AddComponent<EndlessTerrainGenerator>();
        terrain.trackTarget = bike.transform;
        terrain.ApplySettings(runSettings);

        ObstacleSpawner obstacleSpawner = systems.AddComponent<ObstacleSpawner>();
        obstacleSpawner.ApplySettings(runSettings);
        terrain.OnGroundSampled += obstacleSpawner.HandleGroundSampled;

        terrain.Initialize(startPoint);

        CrashDetector crashDetector = systems.AddComponent<CrashDetector>();
        crashDetector.bikeRigidbody = bike.bikeRigidbody != null ? bike.bikeRigidbody : bike.GetComponent<Rigidbody2D>();
        crashDetector.bikeController = bike;
        crashDetector.groundCheckDistance = bike.groundCheckDistance;
        crashDetector.groundLayer = bike.groundLayer;

        RunManager runManager = systems.AddComponent<RunManager>();
        runManager.Initialize(bike.transform, crashDetector, bike);

        LandingDetector landingDetector = systems.AddComponent<LandingDetector>();
        landingDetector.ApplySettings(FindSettings<LandingDetectorSettings>());
        landingDetector.Initialize(bike, bike.FrontWheelContact, bike.BackWheelContact);

        SetupCamera(bike, crashDetector, landingDetector);
    }

    static void SetupCamera(BikeController bike, CrashDetector crashDetector, LandingDetector landingDetector)
    {
        CinemachineCamera cmCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        if (cmCamera == null) return;

        CinemachineImpulseSource impulseSource = bike.GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null) impulseSource = bike.gameObject.AddComponent<CinemachineImpulseSource>();

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CinemachineImpulseListener>() == null)
        {
            mainCamera.gameObject.AddComponent<CinemachineImpulseListener>();
        }

        CameraDirector cameraDirector = cmCamera.gameObject.AddComponent<CameraDirector>();
        cameraDirector.ApplySettings(FindSettings<CameraDirectorSettings>());
        cameraDirector.Initialize(bike, crashDetector, impulseSource, landingDetector);
    }

    // 优先在编辑器里按类型搜整个 Assets(不要求放在 Resources 目录下,创建在哪里都能找到);
    // 找不到就退回 Resources.Load,给打包后的版本留一条路。没有资产的话直接用脚本里的默认值。
    static T FindSettings<T>() where T : ScriptableObject
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
        }
#endif
        return Resources.Load<T>(typeof(T).Name);
    }
}
