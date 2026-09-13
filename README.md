# RidingBike

一个基于物理的 2D 骑行 endless run 原型。Unity 6 (6000.0.63f1) + URP + 2D 物理,目前是 PC 原型阶段(键盘操作),还没有做正式的美术、音效或 roguelike 局内/局外养成系统。

## 核心玩法

骑车向右无限前进,地形随机生成、有上下坡和障碍物,姿态失控会摔车结束本局。

**操作**:
- `A` / `D`(或方向键左右):加速 / 减速倒车
- `Shift`(左右皆可):加速键,按住时用更大扭矩更快提速,最高速度不变
- `Space`:触地时短按跳跃;空中长按触发 360° 空翻
- `R`:摔车结算后重开(重新加载场景)

## 已实现的系统

### 骑行物理(`BikeController.cs`)
- `Rigidbody2D` + 两个 `WheelJoint2D` 组成的真实物理自行车,后轮电机驱动、前轮被动(和真实自行车一样,链条只带后轮)
- 速度上限用真实单位表达(`maxSpeedKmh`,默认 100km/h),按轮子实际世界半径反推电机转速上限,保证能转到匹配的物理速度
- 按住 Shift 时驱动扭矩/电机加速度更大,但最高速度不变,统一由 `maxSpeedKmh` 封顶
- 自动回正(PD 弹簧):触地时把车身角度拉向"当地地面坡度"而不是死磕水平,贴合坡面骑行;空中退回水平,方便落地时姿态可控
- 空中按方向键可以压头/抬头
- 跳跃 + 空中长按空格触发的 360° 空翻(不会被摔车判定误伤)
- 轮子的视觉转速和物理完全解耦:按实际车速换算出转多少度,转的是一个单独的、没有任何物理组件的子物体,不会反过来干扰驱动轮靠摩擦力驱动车身的机制(早期版本在这里踩过坑——直接转物理轮子的 Transform 会把上坡的车顶停)

### 无限地形生成(`EndlessTerrainGenerator.cs` + `EndlessRunSettings.cs`)
- 程序化生成"平地 → 上坡 → 下坡 → 平地 → ..."循环的地形,不是随机拼接直线段
- 平地/上坡/下坡的长度范围、坡的高度差都能独立配置,阶段之间用 SmoothStep 过渡(两端导数为 0),没有尖角,不会有物理"打架"
- 上下坡对速度的影响完全来自物理(重力沿坡面分量),没有任何脚本化的强制加减速
- 玩家前方持续生成、身后自动回收,保持点数恒定
- 所有参数抽成了 `EndlessRunSettings`(ScriptableObject),Project 窗口 `Create > RidingBike > Endless Run Settings` 建一份放在 `Assets` 任意位置就能在 Inspector 里调,不建就用脚本默认值;仓库里已经有一份 `Assets/EndlessRunSettings.asset`

### 障碍物(`ObstacleSpawner.cs`)
- 沿地形按概率放置,上坡不放(留作低速缓冲/救车区间)
- 碰撞体带圆角,避免高速经过时在直角上被物理引擎解算出过大冲量

### 摔车判定(`CrashDetector.cs`)
- 车身触地且倾角超过阈值、并持续一小段时间后才判定摔车(给救车余地),不会误判主动做的空翻
- 摔车后锁定输入、冻结后轮电机,交给 `RunManager` 结算

### 局内 UI / 结算(`RunManager.cs`)
- 左上角实时显示距离和时速(km/h)
- 摔车后显示结算文字,按 R 重开

### 速度反应式镜头(`CameraDirector.cs` + `CameraDirectorSettings.cs`)
- 基于场景里已有的 Cinemachine(`CinemachineCamera` + `CinemachinePositionComposer`)跟拍,所有缓动用 DOTween 驱动
- 车速越快镜头越拉远,越慢/摔车越放大聚焦;放大用短促的 ease,拉远用丝滑的 ease,两个方向的反差是同一套系统里缓动方向不同造成的
- 按住 Shift 时镜头目标立刻拉满,不等物理车速真的追上去
- 空中额外拉远一点,落地一个短促的镜头回弹
- 车速越快,镜头目标点越往车头前方偏移(look-ahead)
- 摔车瞬间无条件接管:镜头快速聚焦 + 通过 Cinemachine Impulse 触发一次幅度可调的短暂抖动
- 参数抽成了 `CameraDirectorSettings`,用法和 `EndlessRunSettings` 一样(`Create > RidingBike > Camera Director Settings`)

### 自动装配(`EndlessRunBootstrap.cs`)
- 场景加载(含重开)时自动找到 Bike,停用场景里原本那块静态地面,接上上述所有系统
- 不依赖手动编辑 `.unity` 场景文件,不需要在 Inspector 里手动拖引用

## 技术栈

- Unity 6000.0.63f1,Universal Render Pipeline (2D)
- 2D 物理(`Rigidbody2D` / `WheelJoint2D`),`Fixed Timestep` 调到 0.005(200Hz)以支撑高速下的悬挂稳定性
- Cinemachine 3.1.6(镜头跟拍)
- DOTween(Demigiant,`Assets/Plugins/Demigiant/DOTween`)——所有镜头缓动用它做

## 还没做的

- 局外 meta 进度、自行车部件构筑、生命值/护盾系统(设计阶段讨论过,还没写代码)
- 局内三选一增益卡之类的 roguelike build 系统
- 正式美术(现在地形是纯色网格、障碍物是彩色方块、车是占位精灵)、音效/音乐
- 移动端输入适配(现在只有键盘)
- 存档/进度持久化
