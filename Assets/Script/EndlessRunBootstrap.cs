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
        // Near Miss 判定区比实心碰撞体大一圈、用 isTrigger 实现；这个项目里 Physics2D 的
        // queriesHitTriggers 是开着的(项目默认)，不关掉的话 IsGrounded/坡度探测这些射线
        // 会把"贴近但没撞到"的触发区也当成地面命中，产生假阳性的触地判定。
        // 这个开关只影响 Raycast/Overlap 这类主动查询，不影响 OnTriggerEnter/Exit 回调，
        // 所以关掉之后 NearMissDetector 完全不受影响。
        Physics2D.queriesHitTriggers = false;

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

        SetupBackground(bike);

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

        BikeDamageSystem damageSystem = systems.AddComponent<BikeDamageSystem>();
        damageSystem.ApplySettings(FindSettings<BikeDamageSettings>());
        damageSystem.Initialize(bike, crashDetector);

        RunManager runManager = SetupRunManagerUI();
        runManager.Initialize(bike.transform, damageSystem, bike);

        LandingDetector landingDetector = systems.AddComponent<LandingDetector>();
        landingDetector.ApplySettings(FindSettings<LandingDetectorSettings>());
        landingDetector.Initialize(bike);

        TrickSystem trickSystem = systems.AddComponent<TrickSystem>();
        trickSystem.ApplySettings(FindSettings<TrickSystemSettings>());
        trickSystem.Initialize(bike, landingDetector);

        ComboSystem comboSystem = systems.AddComponent<ComboSystem>();
        comboSystem.ApplySettings(FindSettings<ComboSystemSettings>());
        comboSystem.Initialize(landingDetector, obstacleSpawner, crashDetector);

        runManager.InitializeFeedback(trickSystem, comboSystem, obstacleSpawner);

        SetupCamera(bike, damageSystem, landingDetector);
    }

    // UI 现在是手动在 Editor 里搭的 Assets/prefab/EndlessRunCanvas.prefab，实例化出来之后
    // 直接把 RunManager 挂到它根节点上——RunManager.Awake() 会按名字把预制体里的子物体
    // (DistanceText/SpeedText/.../HpBarBackground/HpBarFill) 找出来，改预制体视觉不用碰这份代码。
    static RunManager SetupRunManagerUI()
    {
        GameObject canvasPrefab = FindPrefab("EndlessRunCanvas");
        if (canvasPrefab == null)
        {
            Debug.LogError("EndlessRunBootstrap: 找不到 Assets/prefab/EndlessRunCanvas.prefab，局内 UI 不会显示。");
            return new GameObject("RunManager (missing UI prefab)").AddComponent<RunManager>();
        }

        GameObject canvasInstance = Object.Instantiate(canvasPrefab);
        canvasInstance.name = canvasPrefab.name;
        return canvasInstance.AddComponent<RunManager>();
    }

    static void SetupBackground(BikeController bike)
    {
        Sprite backgroundSprite = FindBackgroundSprite();
        if (backgroundSprite == null) return;

        GameObject backgroundGO = new GameObject("Background");
        BackgroundScroller scroller = backgroundGO.AddComponent<BackgroundScroller>();
        scroller.backgroundSprite = backgroundSprite;

        // 跟摄像机而不是车身:Cinemachine 的 Follow 阻尼本来就会把车身物理位置的抖动
        // 平滑掉,背景跟摄像机走等于免费继承这份平滑,不需要另外搭一个"平滑锚点"。
        // 摄像机的 look-ahead 偏移也会跟着算进去,背景对齐视野中心反而更准。
        Camera mainCamera = Camera.main;
        scroller.trackTarget = mainCamera != null ? mainCamera.transform : bike.transform;
    }

    static void SetupCamera(BikeController bike, BikeDamageSystem damageSystem, LandingDetector landingDetector)
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
        cameraDirector.Initialize(bike, damageSystem, impulseSource, landingDetector);
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

    // 跟 FindSettings<T> 同一个思路:优先在编辑器里按名字搜整个 Assets(bg1.jpeg 放在
    // Assets/Art 下,不要求在 Resources 目录);找不到就退回 Resources.Load,给打包后的版本留一条路。
    static Sprite FindBackgroundSprite()
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("bg1 t:Sprite");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
#endif
        return Resources.Load<Sprite>("bg1");
    }

    // 跟 FindBackgroundSprite 同一个思路:优先在编辑器里按名字搜整个 Assets(预制体放在
    // Assets/prefab 下,不要求在 Resources 目录);找不到就退回 Resources.Load，给打包后的版本留一条路。
    static GameObject FindPrefab(string name)
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{name} t:Prefab");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }
#endif
        return Resources.Load<GameObject>(name);
    }
}
