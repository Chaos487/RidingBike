# RidingBike

一个基于物理的 2D 骑行 endless run 原型。Unity 6 (6000.0.63f1) + URP + 2D 物理,目前是 PC 原型阶段(键盘操作)。

这是游戏概览,只讲"现在有什么、大致怎么运作"。完整的系统细节、当前进度核实记录、
已知 bug、以及往后开发新功能要参考的设计方向,都在 [`RidingBike_Design_v2.md`](RidingBike_Design_v2.md)
——**后续新功能开发以那份文档为准**,这份 README 只做高层索引,不重复维护细节。

## 核心玩法

骑车向右无限前进,地形持续生成、有上下坡和障碍物。玩家需要在高速前进的同时控制姿态、
跳跃、空翻,追求"贴身擦过障碍物"和"高质量落地"带来的连击和分数,姿态彻底失控会摔车。

**操作**:
- `A` / `D`(或方向键左右):加速 / 减速倒车
- `Shift`(左右皆可):加速键,按住时用更大扭矩更快提速,最高速度不变,按行驶距离充能
- `Space`:触地时短按跳跃;空中长按触发 360° 空翻
- `R`:摔车结算后重开(重新加载场景)

## 已实现的系统

以下按类别列出现状,每项的实现细节在设计文档对应章节(括号内是章节号)。

**骑行物理**(`BikeController.cs`,3.1 节)
`Rigidbody2D` + 两个 `WheelJoint2D` 组成的真实物理自行车,后轮电机驱动、前轮被动;
速度上限用真实单位表达(默认 100km/h);自动回正贴合坡面;跳跃力度随车速/下坡角度
动态加成;空中长按空格触发 360° 空翻(不接受方向键控制空中姿态,是设计选择,不是 bug)。

**无限地形生成**(`EndlessTerrainGenerator.cs` + `EndlessRunSettings.cs`,3.2 / 3.11 节)
程序化生成"平地→上坡→下坡→平地→…"循环,阶段间 SmoothStep 过渡不留尖角;上下坡对
速度的影响完全来自物理重力,没有脚本化强制加减速;平地结束时还会按概率生成断层
(可配置出现概率/跨度/深度),地形依然是连续曲线,不是真的断开;所有参数抽成 ScriptableObject。

**掉进断层**(`GapFallHandler.cs`,配置在 `BikeDamageSettings.cs` 里,3.12 节)
玩家没跳过断层、掉得比记录的地面高度深过阈值:不管当前还剩多少血,直接判定为致命
摔车,镜头停止跟随定在原地(车身还在物理下坠,不然镜头会跟着一起往看不见的深处跑),
走跟正常摔死一样的结算流程(锁输入、镜头聚焦、显示结算画面),按 R 重开。

**障碍物**(`ObstacleSpawner.cs`,3.3 节)
沿地形按概率放置,上坡不放(留作救车缓冲区);碰撞体带圆角避免高速冲量异常。

**落地质量 / 特技 / 连击 / 贴身险**(`LandingDetector.cs` / `TrickSystem.cs` /
`ComboSystem.cs` / `NearMissDetector.cs`,4.1 / 5 / 6 / 7 节)
落地瞬间按角度偏差、角速度、垂直速度分出 Perfect / Good / Bad;空中转出的角度按
档位换算特技分;贴身擦过障碍物、或 Perfect/Good 落地会累计连击,超时或摔车清零。
这四个系统的分数目前互相独立展示,**还没有一个统一的 ScoreSystem 把它们乘到一起**。

**摔车判定 / 生命值**(`CrashDetector.cs` + `BikeDamageSystem.cs`,3.4 / 3.8 节)
车身触地且倾角超过阈值、持续一小段时间才判定一次"失控";失控不直接结束一局,而是
扣一条 HP 血条(默认能扛 2 次、第 3 次才真的摔车结算),期间给无敌时间和贴图闪烁提示。

**局内 UI / 结算**(`RunManager.cs`,3.5 节)
距离、时速、氮气就绪状态、连击数、HP 血条实时显示;特技/摔车/贴身险弹字提示;
摔车后显示结算信息,`R` 重开。**结算画面目前只有距离,没有分数/最佳记录**,也没有任何
跨局存档。

**开始画面 / 主菜单**(`RunManager.cs` 的开始 gate + `MainMenuController.cs`)
开始前是"tap to start"——屏幕背后能看到骑行场景(地形/车,只是暂停了),没有单独一个
不透明的主菜单画面,全屏幕任意位置点一下就开始(参考 Alto's Odyssey 的开始画面手感)。
左上角一个 Menu 入口,点开是 Goals/Settings/Language/Stats 四个 Tab 切换的面板,布局照抄
参考图的交互(顶部横排 Tab + 内容区 + 右下角 Back),**目前每个 Tab 内容都只是占位文字,
不接任何真实数据**(存档进度、音量/暂停位置这些设置项、多语言切换、跑分统计都还没做)。
Menu 入口只在"开始前"这个阶段有意义,骑行真正开始后会自动收起来。

**骑行中暂停**(`RunManager.cs` 的 `TogglePause()` + `PauseController.cs`)
左下角一个暂停按钮(手机端点它),桌面端 `Esc` 键效果相同,两条路径最终都走
`RunManager.TogglePause()`(逻辑跟开始前的 gate 一样:`Time.timeScale = 0` + 禁用
`BikeController`)。暂停面板照参考图做成左右分屏——左边 Home/Restart/Resume 三个按钮
(Home 和 Restart 现在是同一个行为:直接重新加载场景,项目没有单独的主菜单场景,重开
自然会落回 tap to start;Photo Mode 这次不做),右边是跟开始画面 Menu **完全同一套**
Goals/Settings/Language/Stats Tab 逻辑(抽成了 `TabGroupController.cs` 给两处复用,内容依然
占位)。摔车结算后暂停入口会跟着收起来。

**开场引入动画**(`CameraDirector.EnterIntroFraming`/`PlayIntroReveal`)
tap to start 画面车不直接可见——把镜头前瞻偏移(跟满速时"往前看"用的是同一个
`CinemachinePositionComposer.TargetOffset.x`)一次性顶到很大,车就被推出画面外;点击后
用 DOTween 把这个偏移缓动回 0,车从画面外滑进来正好接上正常骑行(参考 Alto's Odyssey
开场的手感)。纯镜头技巧,车身实际位置/物理状态完全没动。

**速度反应式镜头**(`CameraDirector.cs` + `CameraDirectorSettings.cs`,3.6 节)
基于 Cinemachine 跟拍,车速越快镜头越拉远、越慢/摔车越聚焦,落地按质量给回弹反馈,
摔车瞬间接管镜头。

**多层视差背景**(`BackgroundScroller.cs`,3.9 节)
`Assets/prefab/Background.prefab` 下挂多层、每层独立的滚动速度做出纵深感,用真实
craftpix 像素美术,层数可以在预制体里自由加减,不用改代码。

**前景剪影层**(`GroundForegroundLayer.cs`)
屏幕底部常驻一条深色剪影,纯装饰、不参与碰撞,营造"比真地形还近"的纵深感(参考 Alto's
Odyssey 的沙丘剪影)。不用 `BackgroundScroller` 的摄像机视差公式(那套 `parallaxFactor = 1`
已经是"跟真地形一样快"的上限)——靠的是遮挡关系(`sortingOrder` 比车/地形都高)+ 常驻屏幕
底部 + 颜色更深这三个线索,不是滚动速度。每个采样点的基准高度直接查
`EndlessTerrainGenerator.TryGetHeightAt` 拿真实地形高度(不是贴摄像机 Y),车起跳/落地/
镜头缩放都不会带着这层一起跳动,而且天然保证任何位置都比真地形低 `sinkDepth` 米,不会
意外"浮"到真地形上面;在此基础上叠加一层独立的低频 Perlin Noise 起伏,X 方向跟随车身,
范围(`halfWidth`)明显小于地形的生成/回收窗口。整个 Mesh 每帧根据车身当前位置重新生成,
天然跟着车一直往前铺,不需要额外的延伸/回收逻辑。**`sinkDepth`/颜色这些数值目前是占位,
需要在 Play 模式里实际盯着调。**

**尾气/扬尘粒子效果**(`BikeExhaust.cs` + `BikeExhaustSettings.cs`)
挂在车身根节点上,实例化 `Assets/Resources/ExhaustTrail.prefab`(一个 `ParticleSystem`,视觉
参数——形状/颜色/大小/生命周期——完全由美术在预制体上调,脚本只管"什么时候喷、喷多猛"：
接地且车速超过阈值才喷,喷发强度按车速插值、Boost 时额外拉满)。**这个预制体现在还没有人
做**,`EndlessRunBootstrap` 找不到就跳过、只打一条 Warning,不影响其它系统——纯装饰功能。

**齿轮(游戏内货币)**(`GearManager.cs` 管持久化 + `GearSpawner.cs` 生成 + `GearPickup.cs` 拾取 +
`GearSettings.cs` 配置)
骑行沿途生成可拾取的齿轮(`Assets/Resources/Gear.prefab`,单张 sprite),车身碰到即拾取,
数量立刻存 `PlayerPrefs`(货币比"最远距离"这种纯记录更经不起丢,不等结算才存)并实时刷新
右上角 UI。生成用跟 `StationMarkerSpawner` 一样"轮询地形高度"的手法,每个生成点放一组
(数量在 `minGroupSize`~`maxGroupSize` 间随机,组内间距 `intraGroupSpacing`,组与组之间的
间隔/概率才是 `minSpawnInterval`/`maxSpawnInterval`/`spawnChance` 管的),不跟
`ObstacleSpawner` 共用采样点;断层、Station 安全区、还没生成到的位置只跳过组里命中的那
几个,不影响同一组其他位置。旋转视觉是经典的单图假 3D
手法——只缩放 X 轴按 cos 曲线挤压(`1 → 0 → -1 → 0` 循环,不需要 sprite sheet),转到"背面"
(缩放为负)时顺带调暗颜色模拟光照角度变化,外加一点上下浮动。**现在只有"加"没有"花",
花的机制留给以后的 Roguelike 局外商店(GitHub #3 存档设计提过的方向)。**

**音频框架**(`AudioManager.cs`,3.10 节)
挂在场景里的一个独立物体上,六个可配置槽位(BGM/环境音/骑行中/落地/加速/摔车),
每个槽位支持多音频随机或顺序播放、延迟触发、是否循环。**目前只是空的架子,场景里
还没有挂任何实际音频文件,运行是静音的。**

**自动装配**(`EndlessRunBootstrap.cs`,3.7 节)
场景加载(含重开)时自动找到 Bike、停用场景里的静态地面、实例化并接好上述所有系统,
不依赖手动编辑 `.unity` 场景文件。

**Roguelike Station 三选一**(`NodeManager.cs` 调度 + `DecisionCurve.cs`/`NodeChoicePool.cs`/
`NodeEffectSystem.cs`/`NodeChoiceUI.cs` 分职责 + `NodeSettings.cs` 配置 + `StationMarkerSpawner.cs`
呈现层,存档设计见 [GitHub #3](https://github.com/Chaos487/RidingBike/issues/3) 和后续
Station/Pit Stop 讨论)
骑行一定距离后物理化"进站":先进入 Approaching(弹"即将进站"提示,车速平滑降到站内低速,
玩家依然能控制跳跃/空翻),到站后才真正暂停(`Time.timeScale = 0` + 挂起 `BikeController`)、
弹出三选一、选中→确认两步生效,随后 Exiting(车速平滑加速回正常封顶)才回到正常骑行——
不是原来那种瞬间暂停。减速/加速复用 `BikeController` 现成的限速追赶逻辑,新增一个
`externalSpeedCapKmh`(可空临时封顶)跟 Node 效果永久改的 `maxSpeedKmh` 分开,互不覆盖。
安全区覆盖"预警减速开始→出站加速结束"整段,`EndlessTerrainGenerator` / `ObstacleSpawner`
通过反向查询跳过这段范围内的断层/障碍物生成。`StationMarkerSpawner` 在世界里贴地摆一个
占位旗子标记 Station 位置。**暂停时的背景虚化暂时搁置**:`Assets/Shaders/StationBlur.shader` +
`StationBlurFeature.cs` 这套真实屏幕空间模糊(URP Renderer Feature,手写 HLSL)代码还在,
但在这个项目实际用的 Render Graph 渲染路径下跑不起来(`ScriptableRenderPass.Execute` 是
Compatibility Mode 专用的老 API,Render Graph 模式下整个 Pass 不生效),`NodeManager` 已经不再
调用它,`NodePanel` 暂时还是用原来那层半透明黑色遮罩顶着——以后要么把 Pass 重写成
`RecordRenderGraph` 新 API,要么在 Player Settings 里切到 Compatibility Mode。
`DecisionCurve` 按"第几个 Station"把进度换算成早/中/后期档位;`NodeChoicePool` 按 Choice 的
`Min Stage`(从这档开始一直到后面所有档都可能出现,不是只在对应档出现一次)过滤、按 `Weight`
加权抽取,并保证呈现的三个选项尽量覆盖低/中/高 `Risk Level`,不是纯随机抽奖;`NodeEffectSystem`
把选中的 Effects 应用到 `BikeController`/`BikeDamageSystem` 的既有数值字段(白名单式,不是
通用效果引擎);`NodeChoiceUI` 只管面板显示/选中态/Confirm 按钮,不知道游戏逻辑。**Station
世界标记和 Choice 池的具体数值/文案仍是占位,后续按需要再替换正式美术。**

## 技术栈

- Unity 6000.0.63f1,Universal Render Pipeline (2D)
- 2D 物理(`Rigidbody2D` / `WheelJoint2D`),`Fixed Timestep` 调到 0.005(200Hz)以支撑高速下的悬挂稳定性
- Cinemachine 3.1.6(镜头跟拍)
- DOTween(Demigiant,`Assets/Plugins/Demigiant/DOTween`)——所有镜头缓动用它做

## 还没做的 / 已知问题

只列要点,完整清单、优先级排序(P0→P3)、每一项的具体欠账见设计文档第 0 / 23 节:

- 没有统一 ScoreSystem,Combo 不真正影响分数,摔车结算画面信息不全,没有跨局存档
- [GitHub #1](https://github.com/Chaos487/RidingBike/issues/1)、
  [GitHub #2](https://github.com/Chaos487/RidingBike/issues/2) 两个已知 bug,状态见设计文档第 0 节
- Event/Landmark 地形、Speed Risk、难度曲线、Roguelike 三选一/Build 构筑、局外 Meta 进度:均未开始
- 正式美术(地形/障碍物仍是纯色网格和色块)、音频内容(架子已搭,没有素材)、移动端输入:均未完成
