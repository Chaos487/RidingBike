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

        SetupParallaxBackground(bike);

        GameObject systems = new GameObject("EndlessRunSystems");

        EndlessTerrainGenerator terrain = SetupGroundGenerator();
        terrain.trackTarget = bike.transform;
        terrain.ApplySettings(runSettings);

        ObstacleSpawner obstacleSpawner = systems.AddComponent<ObstacleSpawner>();
        obstacleSpawner.ApplySettings(runSettings);
        terrain.OnGroundSampled += obstacleSpawner.HandleGroundSampled;

        terrain.Initialize(startPoint);

        CrashDetector crashDetector = systems.AddComponent<CrashDetector>();
        crashDetector.bikeRigidbody = bike.bikeRigidbody != null ? bike.bikeRigidbody : bike.GetComponent<Rigidbody2D>();
        crashDetector.bikeController = bike;

        BikeDamageSettings damageSettings = FindSettings<BikeDamageSettings>();
        BikeDamageSystem damageSystem = systems.AddComponent<BikeDamageSystem>();
        damageSystem.ApplySettings(damageSettings);
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

        CameraDirector cameraDirector = SetupCamera(bike, damageSystem, landingDetector);
        SetupGapFallHandler(systems, bike, terrain, damageSystem, damageSettings, cameraDirector);
        SetupAudio(bike, crashDetector, landingDetector, runManager);
        SetupNodeSystem(systems, bike, damageSystem, runManager, terrain, obstacleSpawner);
    }

    // Roguelike Node 三选一系统(GitHub Issue #3 存档方案,现在以物理化 Station 呈现)——
    // 触发/安全区/减速进站/加速出站/选择/生效全部交给 NodeManager,这里只负责接线:
    // 把 Station 世界标记的地形引用、安全区反向查询接到断层/障碍物生成器上,
    // 把开始/结束两个时机点(OnGameStarted/OnFinalCrash)接到 NodeManager 对应的方法上。
    static void SetupNodeSystem(GameObject systems, BikeController bike, BikeDamageSystem damageSystem,
        RunManager runManager, EndlessTerrainGenerator terrain, ObstacleSpawner obstacleSpawner)
    {
        StationMarkerSpawner markerSpawner = systems.AddComponent<StationMarkerSpawner>();
        markerSpawner.Initialize(terrain);

        NodeManager nodeManager = systems.AddComponent<NodeManager>();
        nodeManager.ApplySettings(FindSettings<NodeSettings>());
        nodeManager.Initialize(bike, damageSystem, runManager.transform, runManager, markerSpawner);

        terrain.overlapsSafeZone = nodeManager.OverlapsSafeZone;
        obstacleSpawner.isInSafeZone = nodeManager.IsInSafeZone;

        runManager.OnGameStarted += nodeManager.BeginRun;
        damageSystem.OnFinalCrash += nodeManager.HandleFinalCrash;
    }

    // 音频是直接挂在场景里的 AudioManager(不是这里生成的，运行时只拿它的单例来接线)。
    // BikeController/CrashDetector/LandingDetector 都是本方法运行时才生成的，没法在 Inspector 里
    // 互相拖引用，所以在这里把它们各自的事件接到 AudioManager 对应的槽位上。
    static void SetupAudio(BikeController bike, CrashDetector crashDetector, LandingDetector landingDetector, RunManager runManager)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            Debug.LogWarning("EndlessRunBootstrap: 场景里找不到 AudioManager，音效不会播放。");
            return;
        }

        bike.OnBoost += audio.PlayBoost;
        landingDetector.OnLanded += (quality, order) => audio.PlayLanding();
        crashDetector.OnCrash += () =>
        {
            audio.PlayCrash();
            audio.StopRide();
        };

        // 不在这里直接 PlayRide()——这时候开始界面(RunManager.EnterStartGate)还在静止着，
        // 骑行音效循环得等玩家点了 Start、真正开始骑行才响。
        runManager.OnGameStarted += audio.PlayRide;
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

    // 地形几何体是运行时按曲线生成的，没法预先摆好，但视觉(材质/贴图)可以在 Editor 里调——
    // 实例化 Assets/prefab/Ground.prefab，把 EndlessTerrainGenerator 挂在它上面，
    // 脚本只管生成 Mesh 数据，MeshRenderer 用哪个材质完全交给预制体。
    static EndlessTerrainGenerator SetupGroundGenerator()
    {
        GameObject groundPrefab = FindPrefab("Ground");
        if (groundPrefab == null)
        {
            Debug.LogError("EndlessRunBootstrap: 找不到 Assets/prefab/Ground.prefab，用纯代码兜底生成地形(没有自定义材质)。");
            return new GameObject("EndlessTerrainGenerator (missing Ground prefab)").AddComponent<EndlessTerrainGenerator>();
        }

        GameObject groundInstance = Object.Instantiate(groundPrefab);
        groundInstance.name = groundPrefab.name;

        EndlessTerrainGenerator terrain = groundInstance.GetComponent<EndlessTerrainGenerator>();
        if (terrain == null) terrain = groundInstance.AddComponent<EndlessTerrainGenerator>();
        return terrain;
    }

    // 多层视差背景:实例化 Assets/prefab/Background.prefab，里面有几层(BackgroundScroller
    // 组件)、每层用哪张图、视差系数多少，全部交给预制体决定，这里只管把 trackTarget 接上去——
    // 用 GetComponentsInChildren 找,不写死"必须是 3 层",以后在预制体里加/删层不用改代码。
    static void SetupParallaxBackground(BikeController bike)
    {
        GameObject backgroundPrefab = FindPrefab("Background");
        if (backgroundPrefab == null)
        {
            Debug.LogError("EndlessRunBootstrap: 找不到 Assets/prefab/Background.prefab，不显示视差背景。");
            return;
        }

        GameObject backgroundInstance = Object.Instantiate(backgroundPrefab);
        backgroundInstance.name = backgroundPrefab.name;

        // 跟摄像机而不是车身:Cinemachine 的 Follow 阻尼本来就会把车身物理位置的抖动
        // 平滑掉,背景跟摄像机走等于免费继承这份平滑,不需要另外搭一个"平滑锚点"。
        // 摄像机的 look-ahead 偏移也会跟着算进去,背景对齐视野中心反而更准。
        Camera mainCamera = Camera.main;
        Transform trackTarget = mainCamera != null ? mainCamera.transform : bike.transform;

        BackgroundScroller[] layers = backgroundInstance.GetComponentsInChildren<BackgroundScroller>();
        foreach (BackgroundScroller layer in layers)
        {
            layer.trackTarget = trackTarget;
        }
    }

    static CameraDirector SetupCamera(BikeController bike, BikeDamageSystem damageSystem, LandingDetector landingDetector)
    {
        CinemachineCamera cmCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        if (cmCamera == null) return null;

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
        return cameraDirector;
    }

    // 掉进断层(3.11 节)的专门处理——直接判定为致命摔车，交给 BikeDamageSystem/RunManager
    // 已有的结算流程；cameraDirector 为空(场景里没挂 CinemachineCamera)就跳过镜头脱离
    // 那一步，不影响摔车判定本身。
    static void SetupGapFallHandler(GameObject systems, BikeController bike, EndlessTerrainGenerator terrain,
        BikeDamageSystem damageSystem, BikeDamageSettings damageSettings, CameraDirector cameraDirector)
    {
        GapFallHandler gapFallHandler = systems.AddComponent<GapFallHandler>();
        gapFallHandler.ApplySettings(damageSettings);
        gapFallHandler.Initialize(bike, terrain, damageSystem, cameraDirector);
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

    // 优先在编辑器里按名字搜——限定在 Assets/prefab 目录下,不搜整个 Assets。项目里导入的
    // 美术素材包经常自带同名的 Prefab(比如 Nature Backgrounds Pixel Art 这个包自己就有一份
    // Ground.prefab、一堆 Background_1.."8".prefab),不限定目录的话很容易搜到别人的东西。
    // AssetDatabase.FindAssets 的文本搜索是模糊子串匹配,不是精确文件名匹配——哪怕限定了目录,
    // "Ground" 也会命中同目录下的 "Background.prefab"(Back-Ground 本身就包含这个子串),
    // 之前就因为这个把地形错误地实例化成了 Background 预制体。所以这里手动按精确文件名过滤,
    // 不依赖搜索结果的顺序。找不到就退回 Resources.Load，给打包后的版本留一条路。
    static GameObject FindPrefab(string name)
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }
#endif
        return Resources.Load<GameObject>(name);
    }
}
