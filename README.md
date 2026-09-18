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

**速度反应式镜头**(`CameraDirector.cs` + `CameraDirectorSettings.cs`,3.6 节)
基于 Cinemachine 跟拍,车速越快镜头越拉远、越慢/摔车越聚焦,落地按质量给回弹反馈,
摔车瞬间接管镜头。

**多层视差背景**(`BackgroundScroller.cs`,3.9 节)
`Assets/prefab/Background.prefab` 下挂多层、每层独立的滚动速度做出纵深感,用真实
craftpix 像素美术,层数可以在预制体里自由加减,不用改代码。

**音频框架**(`AudioManager.cs`,3.10 节)
挂在场景里的一个独立物体上,六个可配置槽位(BGM/环境音/骑行中/落地/加速/摔车),
每个槽位支持多音频随机或顺序播放、延迟触发、是否循环。**目前只是空的架子,场景里
还没有挂任何实际音频文件,运行是静音的。**

**自动装配**(`EndlessRunBootstrap.cs`,3.7 节)
场景加载(含重开)时自动找到 Bike、停用场景里的静态地面、实例化并接好上述所有系统,
不依赖手动编辑 `.unity` 场景文件。

**Roguelike Node 三选一**(`NodeManager.cs` + `NodeSettings.cs`,存档设计见
[GitHub #3](https://github.com/Chaos487/RidingBike/issues/3))
骑行一定距离触发一次:真正暂停(`Time.timeScale = 0` + 挂起 `BikeController`)、弹出三个
Choice、选中→确认两步生效后恢复。触发点前后是安全区,`EndlessTerrainGenerator` /
`ObstacleSpawner` 通过反向查询跳过断层/障碍物生成,不会让玩家因为暂停/恢复意外摔车。
Decision Curve 按"第几个 Node"分早/中/后期档位,越往后正面强化越大、代价越明显。
Effect 第一版是白名单(改 `BikeController`/`BikeDamageSystem` 的既有数值字段),不是
通用效果引擎。**目前是纯数值原型,没有背景虚化/正式美术,Choice 池的具体数值/文案
都是占位,后续按需要在 `NodeSettings` 资产里调。**

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
