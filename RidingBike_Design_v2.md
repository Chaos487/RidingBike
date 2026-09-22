# RidingBike

一个基于物理的 2D 骑行 endless run 原型。\
Unity 6 (`6000.0.63f1`) + URP + 2D 物理，目前是 PC
原型阶段（键盘操作）。

本项目的核心目标不是做一个"自行车皮肤的普通无限跑酷"，而是把**自行车的速度、姿态、空中控制、落地和风险控制**本身做成主要玩法。

------------------------------------------------------------------------

# 0. 当前进度快照（2026-09-17）

按第 23 节的 P0→P3 顺序、逐个对照当前代码库核实了一遍（读了全部 `Assets/Script/*.cs`，不是只看提交记录）：

**P0 —— 核心体验**

-   [x] Landing Quality —— 已实现，见 4.1 节（2026-09-22 已重构，改成只看前后轮 Δt，不再是原来的多信号判定，细节见本节末尾补充）
-   [x] Trick Score —— 已实现，`TrickSystem.cs` + `TrickSystemSettings`（2026-09-22 已重构，改成按完整圈数计分，不再是原来的精确角度档位，细节见本节末尾补充）
-   [x] ~~Combo~~ —— **已移除**：实现过一版（`ComboSystem.cs`，纯计数器，落地质量/贴身险触发 `comboCount++`，超时或摔车清零），2026-09-22 评估后认为太复杂、没有真正接入分数（见下面这条欠账），直接砍掉，不再是 P0 范围内的系统；第 6 节的 Combo 设计仍留着作为历史记录，但已经不在当前实现计划内
-   [x] Near Miss —— 已实现，`NearMissDetector.cs`（挂在 `ObstacleSpawner` 生成的每个障碍物上）

P0 三个系统（Landing Quality / Trick Score / Near Miss）本身都已经闭环（`EndlessRunBootstrap` 已接好），但还有几块明确的欠账，暂不算已完成：

-   没有统一的 `ScoreSystem`——Distance、Trick 分数各自独立显示；Trick Score 已经累计成一个右上角实时显示的 Score（`RunManager.score`），但这只是 Trick 自己的累计值，不是真正把 Distance/Trick 揉到一起的统一分数系统
-   **已补上**"PERFECT!"落地专属弹字——2026-09-22 给 `RunManager` 接了 `LandingDetector.OnLanded`，落地会弹 `PERFECT!`/`GOOD`/`NOT BAD`（独立的 `ToastText`），转出特技的话下面还会纵向堆叠一行 `Backflip x{圈数}`（独立的 `TrickToastText`），两者互不打断；仍然欠账的是**音效**分级——`AudioManager` 的 `landing` 槽位现在对三档质量播的还是同一个音效
-   摔车结算画面信息不全——`RunManager.HandleCrash()` 目前只拼 `距离 {distance} m`，Score / Best Distance / Best Trick 都还没有（Combo 已移除，不需要再要 Highest Combo 了）；也完全没有任何跨局的最佳记录持久化（`bestDistance` 除外，那个已经用 `PlayerPrefs` 存了）
-   已知 bug，仍然 OPEN，本次没有实机验证条件、只做了代码核对：
    -   [GitHub #1](https://github.com/Chaos487/RidingBike/issues/1)（跳跃偶发不生效）：代码里 `groundCheckDistance` 确实已经是修复后的 `1.2`（issue 描述的修复已经落进当前代码），但 issue 本身写明"还没有实机验证过、用户要求先搁置"，本次没有条件复测，状态维持 OPEN，不要当成已解决
    -   [GitHub #2](https://github.com/Chaos487/RidingBike/issues/2)（空中无法触发旋转 / A、D 无法控制空中姿态）：**这个 issue 的后半段已经不是 bug 了，是设计变了**——`BikeController.ApplyBalance()` 现在的注释明确写着"空中不再响应方向键……方向键在空中彻底不影响车身角度"，也就是说"空中用 A/D 压头抬头"这个预期行为本身被主动拿掉了，改成完全交给自动回正 + 空格旋转两条路径，不是还没修好。前半段"长按空格触发不了空中旋转"本次没有条件实机验证，`HandleJumpAndSpin()`/`StartSpin()` 代码逻辑读起来是完整的，但读代码不能代替实机测试，issue 继续保持 OPEN

**生命值/损毁系统**（`BikeDamageSystem.cs`，不在原设计文档范围内，玩法上的额外改动）：**跟这份文档 2026-09-14 快照里记的不一样，这里订正一下**——实际实现是一条纯 HP 血条（默认 `maxHp=100`，`damagePerCrash=35`，扛得住 2 次、第 3 次才死），**不是**"第 1 次卸前轮飞出去、第 2 次卸 rack"这种部件真实掉落的机制；每次摔车扣血后车身自动回正一部分（`recoveryUprightBlend`）、给一段无敌时间（默认 5 秒，`CrashDetector.Recover()`）、车身贴图闪烁提示（`BikeController.PlayInvulnerabilityFlash`），血量归零才真的触发 `OnFinalCrash` 走摔车结算。本次会话多轮实机 Play 测试里能看到 HP 条正常渲染、扣血 toast 正常弹出，但没有专门验证过"血量正好耗到 0 触发最终摔车结算"这条边界路径。

**新增系统（不在 2026-09-14 快照里，本次确认新增）：**

-   **多层视差背景**（`BackgroundScroller.cs`，挂在 `Assets/prefab/Background.prefab`）：见 3.9 节，已实现并接入 `EndlessRunBootstrap`
-   **音频框架**（`AudioManager.cs`，直接挂在 `SampleScene` 里的 `AudioManager` 空物体上）：见 3.10 节。**只是结构，不是内容**——`bgm`/`ambient`/`ride`/`landing`/`boost`/`crash` 六个槽位、随机或顺序播放、延迟、循环开关都已经能用，但目前场景里六个槽位都还没拖入任何音频 clip，游戏实际运行是静音的。且这六个槽位只覆盖第 19 节音频清单里的一部分（清单里的 Jump、Trick、Near Miss、UI selection 还没有对应槽位，需要照 `SoundSlot` 同样的模式自己加）

**P1 —— 内容与风险**：Event/Landmark Chunk、障碍物组合、Speed Risk、难度曲线，均未开始。上次讨论定了 Chunk 地形的方向（`TerrainChunkData` 复用现有阶段原语拼接；缺口用假谷代替，不做真断开），但还没写代码。

**P2 —— Roguelike**：三选一 Upgrade、Speed/Trick/Control Build、自行车部件构筑、局外 Meta Progression，均未开始。

**P3 —— Presentation**：正式美术依然只有地形/障碍物是纯色网格、色块，`Bike.prefab` 现在用的是真实自行车线稿图（不再是纯色占位方块，但也不是最终成品美术）；背景美术已经是真的 craftpix 像素美术（见上面"多层视差背景"）。音效框架已搭（见上）但无内容。音乐、UI Polish、移动端输入、存档均未开始。

**HP / Shield**：第 14 节当时的决定是"不做"，但实际已经做了一版简化的纯 HP 系统（见上），跟第 14 节的讨论结论不一致，这算是本文档和代码之间最大的一处分歧，后续要不要正式改第 14 节的结论、把 HP 系统扶正成正式设计，需要单独拍板。

**这次额外做的、不在原设计文档范围内的修复/调整：**

-   轮子转速视觉与物理解耦，修掉了上坡被顶停的问题
-   100km/h 提速相关的一系列物理再校准（电机转速上限、驱动扭矩、悬挂稳定性、Fixed Timestep 提到 200Hz）
-   跳跃力度不够高、车速/坡度对起跳的加成（现在起跳力度会随车速和下坡角度动态变化，见 3.1 节）
-   `groundCheckDistance` 从 0.8 提到 1.2（对应 GitHub #1，细节见上）
-   摔车判定从距离射线换成前后轮真实物理接触，且要求两轮都触地才判定
-   `WheelContactSensor` 从 Enter/Exit 配对计数改成按物理步判定，避免地形碰撞体频繁重建（约每 0.4m 一次）导致触地状态卡死
-   轮子 `CircleCollider2D` 用的 `WheelMaterial.physicsMaterial2D` 弹性系数(`bounciness`)一直是 0.1，导致车身即便静止不动也会持续小幅弹跳/俯仰震荡——已归零，同时把一个越界的 `m_BounceCombine` 枚举值(4，Unity 合法范围是 0~3)顺手改回合法值
-   `EndlessRunBootstrap.FindPrefab()` 原来用 `AssetDatabase.FindAssets` 的模糊文本搜索按名字找 `Assets/prefab` 下的预制体，"Ground" 会模糊命中同目录下的 "Background.prefab"（Back-**Ground**），导致地形一度被错误实例化成背景预制体——改成精确文件名匹配

**2026-09-21 补充（不在 09-17 快照核实范围内，是之后新增的）：**

-   **齿轮（游戏内货币）**（`GearManager.cs`/`GearSpawner.cs`/`GearPickup.cs`/`GearSettings.cs`）：已实现并接入 `EndlessRunBootstrap`，见 3.13 节。只有"加"没有"花"，花的机制留给以后的局外商店
-   **开始画面 / 主菜单**（`MainMenuController.cs`）：已实现并接入 `EndlessRunBootstrap`，见 3.14 节。Menu 入口下的 Goals/Settings/Language/Stats 四个 Tab **目前都只是占位文字**，没有接任何真实数据/逻辑
-   **骑行中暂停**（`PauseController.cs`，`RunManager.TogglePause()`）：已实现并接入 `EndlessRunBootstrap`，见 3.15 节。左下角按钮/`Esc` 键触发，面板复用开始画面 Menu 同一套 `TabGroupController`（这次连带把 Tab 逻辑抽出来给两处共用了）；Home/Restart 现在都是"重开场景"，Photo Mode 没做
-   **开场引入动画**（`CameraDirector.EnterIntroFraming`/`PlayIntroReveal`）：已实现，见 3.6 节末尾补充的一条。tap to start 画面车藏在镜头外，点击后车从画面外滑进来接上正常骑行，纯镜头偏移技巧，车身物理状态没被动过
-   **尾气/扬尘粒子效果**（`BikeExhaust.cs`/`BikeExhaustSettings.cs`）：代码已接入 `EndlessRunBootstrap`，见 3.16 节。**`Assets/Resources/ExhaustTrail.prefab` 这份粒子预制体还没有人做**，找不到就跳过、只打一条 Warning，等美术把预制体放上去就能直接看到效果，不用再改代码
-   **弹窗背景真实模糊**（`ScreenBlurFeature.cs`/`ScreenBlur.shader`/`BlurredPanelBackground.shader`）：**没做成，已搁置**，详细排查记录见 [GitHub #4](https://github.com/Chaos487/RidingBike/issues/4)。`NodePanel`/`MenuPanel`/`PausePanel` 背景现在挂的是 `BlurredPanelBackground.mat`，理论上接的是这套 Render Graph 多趟降采样/升采样算出来的模糊贴图，但视觉上看不出模糊效果，原因还没定位——下一个 session 要么继续查（建议先用 Frame Debugger 逐帧核实每一趟 Pass 的实际输出），要么换路线

**2026-09-22 订正**：第 24 节"目前尚未完成"清单里好几条已经过时——**Roguelike Station 三选一（`NodeManager.cs`）其实早就做完了**，清单上一直写着"未完成"没人去掉，导致新开一个 session 只看这份文档、没去核对代码库，直接把这条当成事实转述给了用户。顺带查的时候发现"正式美术"（背景/车身已经是真实美术，只有地形/障碍物还是占位）、"音效/音乐"（`bgm`/`ambient` 两个槽位已经接了真实音频文件，见 3.10 节这次一起订正）、"存档/进度持久化"（最远距离、齿轮数量早就用 `PlayerPrefs` 存了）这三条也都写得比实际情况悲观。24 节已经改成准确状态。**教训**：这种"尚未完成"清单是会跟实际进度分叉的，不能只看它判断某个功能做没做，做完/做一半都要随手回来划掉或者订正，不要攒着等下次大审计。

**2026-09-22 补充（同一天晚些时候，Landing Quality / Trick / Combo 三个系统重构）：**

-   **Landing Quality 正式简化**：判据从"车身角度偏差 + 角速度 + 垂直速度"三项综合，改成**只看前后轮有效接地的时间差（Δt）**——不再读坡度/角度/角速度/垂直速度，三档也从 Perfect/Good/Bad 改名成 Perfect/Good/**Not Bad**（最低档改叫 Not Bad，强调"完成了一次有效落地"，不是失败）。默认阈值 `perfectThreshold=0.02s`/`goodThreshold=0.05s`，超过 `goodThreshold` 还等不到第二只轮子(`landingTimeout=0.15s`)直接判 Not Bad。`ContactOrder`（Simultaneous/FrontFirst/BackFirst）跟 Quality 完全解耦，只是共用同一个 `goodThreshold` 当"够不够接近"的边界。`LandingDetector.cs` 内部改成显式状态机（Airborne/Pending/Grounded）。
-   **Trick Score 正式简化**：判据从"精确旋转角度档位"（90/180/360/540/720°）改成**按完整转了几圈**计分——`laps = Floor(Abs(SpinAccumulatedDegrees) / 360)`，不足一圈不计分，默认每圈分数 `50/150/300/500/750`（`TrickSystemSettings.scorePerLap`，超过 5 圈沿用最后一档）。**跟 Landing Quality 彻底解耦**：`TrickSystem` 完全不读 `LandingDetector.Quality`，好落地/差落地转出同样的圈数拿一样的分（旧版"转够但落地差、奖励归零"的规则已经拿掉，`OnTrickFailed` 事件也跟着删了）。
-   **`BikeController.SpinAccumulatedDegrees` 改成读真实物理旋转**：不再是"按住空格的时长 × 固定角速度"，改成持续追踪 `bikeRigidbody.rotation` 相对离地那一刻的差值——松手后惯性/自动回正带着车身继续转的那一截也算进去，不然玩家视觉上转完了一圈，计分却在松手那一刻提前停了。过程中还修了两个更隐蔽的 bug：① 圈数计算之前在任意一只轮子先触地时就冻结，但 `LandingDetector` 要等两只轮子都触地才真正判定落地，中间那段窗口的旋转被漏记，导致前后轮谁先落地会读出不同圈数；② 腾空途中蹭一下地面（单帧假触地）会被当成"真的落地"，把同一次连续转体腰斩成两段——`BikeController` 新增 `landingConfirmTime`（默认 0.05s）触地防抖 + `IsConfirmedGrounded`，`LandingDetector` 的 Pending 状态也改成"先触地那只轮子自己又弹开、另一只轮子没跟上"就取消判定、不再傻等超时。**这三个问题修完之后 Trick 判定仍然偶尔感觉不够稳定**，已经建了 [GitHub #5](https://github.com/Chaos487/RidingBike/issues/5) 留到后面继续查，`TrickSystem.cs` 里还留着一段调试用的 `Debug.Log`（打腾空开始/每圈完成/落地结算），方便下次继续用同样的方法定位。
-   **UI**：落地质量和 Trick 结果不再抢同一个 Toast，`EndlessRunCanvas.prefab` 里新增了独立的 `TrickToastText`（纵向堆叠在原来的 `ToastText` 下面），Trick 弹字从"xxx° +分数"改成"Backflip x{圈数}"；新增右上角常驻 `ScoreText`，累计显示 Trick Score，弹字淡出之后分数才真正计入（参考 Alto's Odyssey 的反馈节奏）。
-   **Combo 连击系统整体移除**：`ComboSystem.cs`/`ComboSystemSettings.cs` 已删除，`EndlessRunCanvas.prefab` 里的 `ComboText` 节点也删了。移除原因是评估后认为这个系统太复杂、且一直没有真正接入分数（纯计数器，不影响 Trick Score/金币），跟"先把 P0 三个核心系统做扎实"的优先级冲突。**第 6 节的 Combo 设计文字还留着**，作为历史设计记录保留，但已经不在当前实现范围内——如果以后要重新考虑连击机制，建议先重新讨论要不要做、怎么接入分数，不要直接照抄第 6 节。第 12/13/16/21/25/26 节里提到 Combo 的地方（Roguelike Build、UI 设计方向、最终设计原则等）暂时没有跟着改，这些是更偏"长期设计愿景"的段落，要不要一并调整没有在这次改动范围内拍板。

------------------------------------------------------------------------

# 1. 核心玩法

骑车向右无限前进，地形持续生成，有上下坡和障碍物。玩家需要在高速前进的同时控制自行车姿态、跳跃、空翻以及落地。

**核心循环：**

> 前进 → 加速/减速 → 处理地形 → 跳跃 → 空中调整姿态 → 空翻/特技 → 落地 →
> 根据落地质量获得反馈与奖励 → 连击 → 继续提高速度和风险 → 摔车 → 结算 →
> 重开

**核心体验目标：**

-   高速骑行的速度感
-   物理自行车的重量感
-   空中调整姿态的操作感
-   "差一点摔车"的紧张感
-   完美落地的满足感
-   高风险特技换取高分的决策感
-   随着距离增加，玩家逐渐主动追求更高风险

------------------------------------------------------------------------

# 2. 操作

-   `A` / `D`（或方向键左右）：加速 / 减速倒车
-   `Shift`：加速键，按住时使用更大扭矩，更快提速，但最高速度不变
-   `Space`：
    -   触地时短按：跳跃
    -   空中长按：触发 360° 空翻
-   `R`：摔车结算后重开

未来如果加入移动端，需要重新设计为触摸操作，但不应该简单把键盘按键直接映射成虚拟按钮。

------------------------------------------------------------------------

# 3. 已实现的系统

## 3.1 骑行物理 `BikeController.cs`

-   `Rigidbody2D` + 两个 `WheelJoint2D` 组成物理自行车
-   后轮电机驱动、前轮被动
-   速度上限使用真实单位表达（`maxSpeedKmh`，默认 100 km/h）
-   根据轮子实际世界半径反推电机转速上限，使物理轮速与实际车速匹配
-   按住 `Shift` 时驱动扭矩/电机加速度更大，但最高速度不变
-   自动回正（PD 弹簧）：
    -   触地时跟随当地地面坡度
    -   空中回到接近水平，方便玩家调整落地姿态
-   空中按方向键可以压头 / 抬头
-   跳跃 + 空中长按空格触发 360° 空翻
-   起跳力度 = 基础值 + 车速加成 + 下坡加成（车越快、起跳时脚下坡度越陡，跳得越高），全部是 `BikeController` 上的公开字段，Inspector 里直接调
-   主动空翻不会被摔车判定误伤
-   轮子的视觉转速和物理完全解耦
-   视觉轮子根据实际车速换算旋转角度，不直接修改物理轮子的 Transform

------------------------------------------------------------------------

## 3.2 无限地形生成 `EndlessTerrainGenerator.cs` + `EndlessRunSettings.cs`

-   程序化生成：
    -   平地
    -   上坡
    -   下坡
    -   平地
    -   循环
-   不是简单随机拼接直线段
-   平地 / 上坡 / 下坡的长度范围独立配置
-   坡的高度差独立配置
-   阶段之间使用 SmoothStep 过渡
-   上下坡对速度的影响完全来自物理重力，不进行脚本化强制加减速
-   玩家前方持续生成
-   身后自动回收
-   保持地形点数恒定
-   参数集中到 `EndlessRunSettings` ScriptableObject
-   项目中已有 `Assets/EndlessRunSettings.asset`

------------------------------------------------------------------------

## 3.3 障碍物 `ObstacleSpawner.cs`

-   沿地形按概率放置障碍物
-   上坡暂不放置障碍物，作为低速缓冲和救车区域
-   碰撞体使用圆角，避免高速撞击直角产生过大的物理冲量

------------------------------------------------------------------------

## 3.4 摔车判定 `CrashDetector.cs`

-   车身触地且倾角超过阈值
-   需要持续一小段时间才判定摔车
-   给玩家短暂的救车机会
-   主动空翻不会被误判
-   `CrashDetector.OnCrash` 触发之后**不直接**锁定输入/结算——中间插了一层 `BikeDamageSystem`（见 3.8 节），扣血/给无敌时间/回正车身，血量真正归零那次才会锁定输入、冻结后轮电机、交给 `RunManager` 结算

------------------------------------------------------------------------

## 3.5 局内 UI / 结算 `RunManager.cs`

-   左上角实时显示：
    -   距离
    -   时速
    -   氮气(Boost)是否就绪，没就绪时显示还差多少米回满
-   右上角实时显示：HP、Best Distance、齿轮数量、Score（Trick Score 累计值）
-   顶部弹字提示：落地质量（`ToastText`：PERFECT!/GOOD/NOT BAD）和 Trick 结果（独立的
    `TrickToastText`：Backflip x{圈数}）纵向堆叠、各自独立淡出，互不打断；Near Miss、
    摔车扣血剩余血量、Station 接近提示走的是跟落地质量共用的 `ToastText`（同一个 Toast，
    新的会打断上一个）
-   HP 血条（`HpBarBackground/HpBarFill`），跟着 `BikeDamageSystem.OnHpChanged` 实时更新
-   摔车结算后显示结算信息（目前只有距离，见第 0 节的欠账记录）
-   `R` 重开（重新加载当前场景）

**Combo（连击）已移除**——原来这里显示"当前连击数"，2026-09-22 评估后认为系统太复杂、
没有真正接入分数，整体砍掉了（见第 0 节 2026-09-22 补充），不再有这项显示。

------------------------------------------------------------------------

## 3.6 速度反应式镜头 `CameraDirector.cs` + `CameraDirectorSettings.cs`

-   使用 Cinemachine `CinemachineCamera` + `CinemachinePositionComposer`
-   使用 DOTween 驱动镜头缓动
-   速度越快，镜头越拉远
-   速度越慢 / 摔车，镜头越放大
-   放大使用短促 ease
-   拉远使用更平滑的 ease
-   `Shift` 时镜头目标立即拉满，不等待实际速度提升
-   空中额外拉远
-   落地产生短促镜头回弹，回弹幅度按 4.1 节的落地质量分级（Perfect 几乎感觉不到，Bad 最明显）
-   速度越快，look-ahead 越明显
-   扣血但没死这一局（`BikeDamageSystem.OnHpChanged`）：只给一次轻微 Impulse 震动，镜头继续跟随/缩放，不接管
-   血量归零真摔车（`BikeDamageSystem.OnFinalCrash`）：镜头才快速聚焦 + 更强的 Impulse 抖动，并停止跟随
-   参数集中到 `CameraDirectorSettings`
-   **开场引入(`EnterIntroFraming`/`PlayIntroReveal`)**：tap to start 画面车不可见——把
    look-ahead 用的同一个 `TargetOffset.x` 一次性顶到 `introOffsetX`(默认 7，需要大于
    半屏宽才能真正把车推出画面），车就被推出画面外；`EnterStartGate` 期间车静止，
    `UpdateLookahead()` 算出来的目标一直是 0，会把这个偏移拉回去，所以额外用
    `introFramingActive` 挡住那部分 `Update()` 逻辑。玩家点击 `tap to start`
    (`RunManager.OnGameStarted`)后用 DOTween 把偏移缓动回 0(`introRevealDuration`/
    `introRevealEase`)，车从画面外滑进来，此时车的物理/输入已经同时恢复
    (`HandleStartClicked` 里 `bikeController.enabled = true`)，车身本身也在往前走，
    两个效果叠加、不冲突。这一步必须在场景加载那一帧渲染前同步调用完
    (`EndlessRunBootstrap.Setup()` 里紧跟着 `SetupCamera` 之后)，不能等某个事件回调，
    不然第一帧可能已经把车渲染出来了；好在 Cinemachine vcam 首次评估
    (`PreviousStateIsValid` 还是 false)本来就是直接摆到目标位置、没有阻尼过渡，
    不会有"镜头飘过去"那一下。纯镜头技巧，车身实际 Transform/物理状态完全没被动过

------------------------------------------------------------------------

## 3.7 自动装配 `EndlessRunBootstrap.cs`

-   场景加载时自动寻找 Bike
-   停用原本场景中的静态地面
-   自动连接所有系统
-   不依赖手动修改 `.unity` 场景文件
-   不需要在 Inspector 中手动拖引用

------------------------------------------------------------------------

## 3.8 生命值/损毁系统 `BikeDamageSystem.cs` + `BikeDamageSettings.cs`

> 不在原设计文档范围内，是开发过程中额外加的玩法调整——第 14 节当时讨论的结论
> 是"不加 HP/Shield"，这里跟那个结论不一致，见第 0 节的说明。

-   插在 `CrashDetector` 和 `RunManager`/`CameraDirector` 中间：`CrashDetector` 判定一次
    "姿态失控"不再直接结束一局
-   纯数值血条：默认 `maxHp=100`，每次摔车扣 `damagePerCrash=35`（默认能扛 2 次，第 3 次才死）
-   扣血但没死：车身角速度清零、朝目标角度（触地贴合坡度/空中回正水平）插值回正一部分
    （`recoveryUprightBlend`），给一段无敌时间（默认 5 秒，期间 `CrashDetector` 直接跳过判定），
    车身贴图同步闪烁（`BikeController.PlayInvulnerabilityFlash`）
-   血量归零：触发 `OnFinalCrash`，交给 `RunManager`/`CameraDirector` 走真正的摔车结算流程
-   `OnHpChanged` 事件供 UI 血条和镜头轻微震动订阅

------------------------------------------------------------------------

## 3.9 多层视差背景 `BackgroundScroller.cs`

-   一个物体只负责一层：挂多份这个组件、每份指定不同 `Sprite` 和 `Parallax Factor`
    就是多层视差，互相独立
-   `Parallax Factor`：1 = 跟地面一样快（世界固定，最快/最近），0 = 完全跟镜头走
    （相对屏幕不动，最远，比如天空）
-   无限横向滚动用"面板按 `deltaX * (1 - parallaxFactor)` 连续漂移 + 漂出覆盖范围就
    重定位到另一端接着用"的算法，不是按镜头位置重新计算网格坐标——后者在
    `parallaxFactor < 1` 时会导致面板世界坐标跟镜头越差越远，长距离 endless run
    必然出问题
-   素材本身不是无缝贴图，靠相邻面板交替水平镜像（`flipX`）拼接消除接缝
-   `Background.prefab`（`Assets/prefab/Background.prefab`）目前配了 4 层，用的是
    `Assets/Nature Backgrounds Pixel Art` 这套 craftpix 像素美术；层数不是写死的，
    `EndlessRunBootstrap` 用 `GetComponentsInChildren<BackgroundScroller>()` 找，
    在预制体里加/删子物体不用改代码
-   每层的 `Scale`/`Vertical Offset` 控制这一层的大小/位置；`Background.prefab` 根节点
    自己的 Transform.Y 会作为所有层共享的整体垂直偏移叠加进去（相当于一个"整体一起挪"
    的总闸），根节点 X 和任何子层自己的 Transform 都不接入计算，改了没用
-   通过 `EndlessRunBootstrap.SetupParallaxBackground()` 在运行时实例化并把
    `trackTarget` 接到主摄像机上（不是车身——车身的物理抖动摄像机的 Cinemachine
    阻尼已经帮忙滤掉了，背景直接继承这份平滑）

------------------------------------------------------------------------

## 3.10 音频框架 `AudioManager.cs`

> **2026-09-22 更新**：`bgm`/`ambient` 两个槽位已经在场景里挂了真实音频文件
> （`Assets/Audio/bgm_main01.mp3`/`sfx_env_main01.mp3`），运行起来背景音乐/环境音是
> 有声音的；`ride`/`landing`/`boost`/`crash` 这四个槽位依然是空的（`Assets/Audio/` 下
> 还有一个 `sfx_bike_land.mp3` 文件，但目前没挂到 `landing` 槽位上，属于素材已经在但
> 还没接的状态）。这份改动是在这个项目另一个并行的 Editor session 里做的，不是这次
> 改的，之前"目前场景里没有挂任何音频 clip"这句已经过时。

-   直接挂在 `SampleScene` 里一个独立的空物体（`AudioManager`）上手动配置，不是
    `EndlessRunBootstrap` 运行时生成的——单例（`AudioManager.Instance`），因为
    `BikeController`/`CrashDetector`/`LandingDetector` 这些系统都是运行时才生成的，
    没法在 Inspector 里手动拖引用
-   六个槽位，每个槽位配置完全独立：`bgm`、`ambient`（环境音）、`ride`（骑行中）、
    `landing`（落地，不分 Perfect/Good/Bad）、`boost`（Shift 加速真正触发时）、
    `crash`（每次 `CrashDetector.OnCrash`，不是只在最终摔死那次）
-   每个槽位共用同一个可配置结构（`SoundSlot`）：
    -   音频列表（可配多个）
    -   挑选顺序：`Random`（每次独立随机）/ `Sequential`（按列表顺序循环）
    -   触发后延迟多少秒才播放
    -   是否循环（勾上占用这个槽位自己的 `AudioSource` 循环播放到被 Stop 为止；
        不勾则是一次性播放、可以叠加、互不打断）
-   `bgm`/`ambient` 进场景自动播放，`ride` 在 `EndlessRunBootstrap.SetupAudio()`
    里一局开始时播放、摔车时停止
-   目前只覆盖第 19 节音频清单的一部分，清单里的 Jump / Trick / Near Miss /
    UI selection 还没有对应槽位，需要的话照 `SoundSlot` 同样的模式加

------------------------------------------------------------------------

## 3.11 断层 `EndlessTerrainGenerator.cs`

> 9.1 节 "Broken Bridge" landmark 的一个简化的、纯随机版本——不是脚本化的固定
> 场景，只是给正常的平地→上坡→下坡循环加了一个"平地结束时可能改成断层"的分支。

-   每次平地阶段结束时（原本要开始一个新的上坡阶段那一刻），按 `gapChance` 的概率
    不生成小山坡、改成生成一次断层，两者二选一，不会叠加
-   断层本身是 陡降(`GapDrop`) -> 谷底(`GapFloor`) -> 陡升(`GapRise`) 三段，跟正常的
    上坡/下坡复用同一套 `SampleHeight()` SmoothStep 插值，只是高度差更大、坡长更短，
    地形依然是一条连续曲线，`EdgeCollider2D`/网格都不需要真的断开——对应第 0/10 节
    之前讨论定的方向："缺口用假谷代替，不做真断开"
-   谷底(`GapFloor`)那一段的长度就是断层的跨度，玩家必须全程在空中飞过这段距离，
    否则会掉进谷底——具体怎么处理见 3.12 节 `GapFallHandler`
-   `ObstacleSpawner` 会跳过断层范围内的所有采样点（新增的 `SlopeDirection.Gap`），
    不会有障碍物生成在谷底或陡坡上
-   五个参数（`gapChance`、`minGapSpan`/`maxGapSpan`、`gapDepth`、`gapEdgeLength`）
    都在 `EndlessRunSettings` 里，用法跟其它生成参数一样；`gapDepth` 必须明显比
    3.12 节 `BikeDamageSettings.gapFallThreshold` 更深，不然玩家会在"掉进虚空"流程
    触发之前就先摔到谷底的实心地面上，穿帮
-   **还没做的**：断层目前是纯随机的，不保证"断层前有没有足够加速距离"、也不检查
    "按当前配置玩家是否有可能跳不过去"——如果调得太宽/太频繁，理论上可能生成一段
    实际过不去的地形，这个正是第 10 节 Chunk-based 生成想解决的问题，目前还没实现，
    调参时留意别把跨度/概率调得太离谱

------------------------------------------------------------------------

## 3.12 掉进断层 `GapFallHandler.cs`

> 处理"玩家没跳过断层"这个具体后果，跟 3.11 节的地形生成是两个独立系统。

-   不用 Collider2D/触发区判定——这个项目已经在 `WheelContactSensor` 上踩过一次坑
    （地形是持续重建的 `EdgeCollider2D`，跨越断层的触发区在地形频繁重建时 Enter/Exit
    不保证严格配对），改成纯数据查表:`EndlessTerrainGenerator` 生成每段断层时就精确
    记录 `[起点X, 终点X, 掉下去之前的地面高度]`，`GapFallHandler` 每帧拿车身当前 X 去
    查(`TryGetGapAt`)，比地面高度记录低过 `gapFallThreshold` 就判定"掉进虚空"，
    **不看 `IsWheelGrounded`**——只认深度，掉得够深就无条件判定，不管当前是不是被
    判定为"触地"（下面这条 bug 修好之前，触地判定会被断层峭壁污染，靠它短路会漏判）
-   **实测过的 bug，已修复**:轮子贴着断层峭壁(陡降/陡升两侧、接近垂直)蹭的时候，
    `WheelContactSensor` 原来只看碰撞层、不看接触点法线方向，把撞墙也算成了"贴地"，
    车能顺着峭壁一路"爬"上去（`ApplyBalance` 把峭壁当坡面回正、驱动轮摩擦力再往上
    推一把），完全绕开了摔车判定。现在 `WheelContactSensor` 额外检查接触点法线跟
    正上方的夹角，超过 `maxGroundAngle`(默认 70°，地形最陡的坡大约 63°，断层峭壁
    接近 90°，中间留了余量)就不算"贴地"，只算撞墙
-   判定的一刻:`BikeController` 整体禁用(反正整局已经结束，输入没有意义)、
    `CameraDirector.DetachFollow()` 停止跟随(镜头 Follow 清空，定在当前位置不动)，
    再调用 `BikeDamageSystem.ForceFinalCrash()`——**不管当前还剩多少血**，一律判定为
    致命摔车，交给已有的摔车结算流程(`RunManager` 锁输入/显示结算画面、`CameraDirector`
    自己的 `HandleFinalCrash` 接管镜头聚焦/震动，跟正常摔死一样，按 R 重开)
-   镜头为什么要单独处理:判死之后 `BikeController` 只是被禁用，车身的 `Rigidbody2D`
    还带着物理速度继续往看不见的深处掉(没有特意冻结)，如果不停跟随，镜头会一直跟着
    车身往下跑，跟已经弹出来的结算画面一起显得很怪。`DetachFollow` 只负责停，没有配套
    的"重新开始跟随"——判死之后是终局，不会再需要接回去
-   `gapFallThreshold` 一个参数，并进了 `BikeDamageSettings`(`断层 (GapFallHandler)`
    分组)，不单开 Settings 资产——逻辑上也是摔车判定的一部分，跟 `damagePerCrash`
    放在一起配置
-   这套之前还做过"扣血但不死、按 Space 原地复活继续骑"的版本，后来改成直接判死——
    那一版专用的 `RunManager` 的 `VoidPromptText` 提示、`CameraDirector.ReattachFollow`
    (复活后重新接回跟随)、`BikeDamageSystem.ApplyDamage`(非致命扣血)都已经删掉；
    `DetachFollow` 留下来了，但语义变了(不再是"暂停跟随等复活"，是"停止跟随到此为止")

------------------------------------------------------------------------

## 3.13 齿轮（游戏内货币）`GearManager.cs` + `GearSpawner.cs` + `GearPickup.cs` + `GearSettings.cs`

-   沿赛道随机生成可拾取的齿轮(`Assets/Resources/Gear.prefab`，单张 sprite，不是 sprite
    sheet)，车身碰到(`WheelContactSensor`)即拾取
-   生成用跟 `StationMarkerSpawner` 一样"轮询地形高度生成到目标 X"的手法；每个生成点不是
    放单个齿轮，而是放一组，组内数量(`minGroupSize`~`maxGroupSize`)、组内间距
    (`intraGroupSpacing`)可调，组与组之间的间隔/出现概率才是
    `minSpawnInterval`/`maxSpawnInterval`/`spawnChance` 管的
-   断层、Station 安全区、已生成的障碍物附近(`obstacleAvoidMargin`，反向查询
    `ObstacleSpawner.IsNearObstacle`)都会跳过——只跳过组里命中的那几个齿轮，不影响同一组
    其他位置正常生成，也不会卡住整条生成链
-   拾取数量立刻存 `PlayerPrefs`(`RidingBike_GearCount`)——不等结算才存，货币比"最远距离"
    这种纯记录更经不起丢
-   右上角 UI 实时显示(`GearText`)，`GearManager.OnGearCountChanged` 事件驱动，
    `RunManager` 在 `Initialize()` 时先读一次当前值再订阅事件(`GearManager.Initialize()`
    在 `RunManager` 创建之前就已经从存档读完、广播过一次事件了，那次广播 `RunManager`
    接不到)
-   视觉是经典单图假 3D 旋转——只缩放 X 轴按 cos 曲线挤压(`1 → 0 → -1 → 0` 循环，不需要
    sprite sheet)，转到"背面"(缩放为负)时顺带把颜色调暗一点模拟光照角度变化，外加一点
    上下浮动
-   现在只有"加"没有"花"，花的机制留给以后的局外商店(见 17 节局外 Meta Progression、
    [GitHub #3](https://github.com/Chaos487/RidingBike/issues/3))

------------------------------------------------------------------------

## 3.14 开始画面 / 主菜单 `MainMenuController.cs`

-   开始前不是单独一个不透明的主菜单画面，是把原来的"Start"按钮换成铺满全屏、完全透明
    的点击层("tap to start")——背后的骑行场景(地形/车)还是能看到，只是暂停着，参考
    Alto's Odyssey 开始画面的手感
-   左上角一个 Menu 入口，点开是一个几乎不透明的面板，顶部横排 Goals/Settings/
    Language/Stats 四个 Tab 切换(选中的加粗变白，其余灰色)，右下角 Back 按钮退回开始
    画面
-   **四个 Tab 目前都只是占位文字**，没有接任何真实数据/逻辑——存档进度(Goals)、
    音量/暂停位置这些设置项(Settings)、多语言切换(Language)、跑分统计(Stats)都还没做
-   Menu 入口只在"开始前"这个阶段有意义：`RunManager.OnGameStarted` 一触发(玩家点了
    "tap to start")就自动把 Menu 入口和面板一起收起来
-   跟 `RunManager` 一样按名字在 `EndlessRunCanvas.prefab` 里 `Find` 子物体，改预制体
    层级/改物体名字的话这个脚本里对应的路径也要跟着改；两个脚本一起挂在 Canvas 根节点上
    (`EndlessRunBootstrap.SetupRunManagerUI`)
-   Goals/Settings/Language/Stats 四个 Tab 之间的切换逻辑本身抽成了独立的
    `TabGroupController`(不是 `MonoBehaviour`，纯逻辑类)，跟 3.15 节的骑行中暂停面板
    共用同一份，以后要改 Tab 内容/加真实数据只用改这一处

------------------------------------------------------------------------

## 3.15 骑行中暂停 `PauseController.cs`

-   左下角一个暂停按钮(手机端点它)，桌面端 `Esc` 键效果相同，两条触发路径最终都调用
    `RunManager.TogglePause()`——保证按钮和快捷键不会导致两边状态不同步
-   真正的"暂停"复用了开始前 `EnterStartGate` 那一套机制：`Time.timeScale = 0` +
    显式禁用 `BikeController`(光靠 `timeScale` 挡不住 `BikeController.Update()` 里的按键
    判定)。`RunManager` 新增 `IsPaused`/`CanPause`(开始前、结算画面都不允许暂停)两个
    只读属性和 `OnPauseStateChanged`(bool 参数：true=刚暂停，false=刚恢复)事件，
    `PauseController` 只订阅事件同步 UI，不自己维护一份"是否暂停"
-   面板布局照参考图(Alto's Odyssey 的暂停画面)做成左右分屏：左边 `ActionList` 竖排
    Home/Restart/Resume 三个按钮，右边 `TabArea` 装的是跟开始画面 Menu 面板结构完全一致的
    `TabBar`/`ContentArea`(`TabGroupController` 认的就是这两个相对路径)，四个 Tab 内容
    同样是占位文字
-   Home 和 Restart 现在是同一个行为——项目没有单独的主菜单场景，"回到主菜单"就是
    `SceneManager.LoadScene` 重新加载当前场景，自然会落回 `EnterStartGate` 的
    tap to start 画面；跟摔车结算画面已有的"R / 点屏幕重开"走的是同一条重载场景的路径，
    区别只是这里允许骑行中途、没摔车也能直接重开。重开前会先把 `Time.timeScale` 显式
    复位成 1(它是全局静态值，`LoadScene` 不会自动重置，虽然新场景的 `EnterStartGate`
    后面也会设一次，这里显式复位更保险)
-   Photo Mode(参考图左侧列表里的第一项)这次没做，项目没有对应的拍照/回放功能
-   摔车结算(`RunManager.OnRunEnded`，新增事件，`HandleCrash` 判死那一刻触发)会把暂停
    按钮和面板一起收起来——正常情况下摔车不可能发生在暂停中(暂停时物理和输入都停了)，
    这里只是保险

------------------------------------------------------------------------

## 3.16 尾气/扬尘粒子效果 `BikeExhaust.cs` + `BikeExhaustSettings.cs`

-   挂在车身根节点(`Bike`)上，实例化 `Assets/Resources/ExhaustTrail.prefab`——一个
    `ParticleSystem`，视觉参数(形状/颜色/大小/生命周期)完全由美术在预制体上调，脚本只管
    "什么时候喷、喷多猛"，只碰 `EmissionModule.enabled`/`rateOverTimeMultiplier` 这两个字段
-   挂点是车身根节点而不是后轮——后轮是真实物理体，转动很快，粒子系统的发射方向会跟着
    乱转；车身根节点转得慢得多(只有上下坡带来的姿态变化)，喷口方向更稳定，效果上也更
    合理(排气管本来就是装在车架上，不是装在轮子上)
-   触发条件：`BikeController.IsWheelGrounded` 且车速超过 `minSpeedKmhForEmission`(默认
    3km/h)——静止/腾空都不喷
-   强度：按车速在 `minEmissionMultiplier`~`maxEmissionMultiplier` 之间插值，Boost 时直接
    覆盖成 `boostEmissionMultiplier`(不等车速真的追上去，按下那一下就该有反应，跟
    `CameraDirector.UpdateZoom` 里 Boost 直接拉满目标是同一个思路)
-   **预制体现在还没有人做**——找不到就在 `EndlessRunBootstrap` 里打一条 `Debug.LogWarning`
    直接跳过，不影响其它系统。预制体要放在 `Assets/Resources/`（不是 `Assets/prefab/`），
    因为 `FindPrefab` 的 Editor 内 `AssetDatabase` 搜索在真机构建里会被编译掉，只有
    `Resources.Load` 兜底分支在真机上生效——这个项目之前在 iOS 上因为资产没放
    Resources 下出过一次空白屏的坑（见第 0 节），这次直接按已经验证过的路子走

------------------------------------------------------------------------

# 4. 核心玩法增强系统

以下系统是下一阶段的重点。

## 4.1 Landing Quality：落地质量系统

> **已实现，2026-09-22 正式重构过一版** —— `LandingDetector.cs` + `WheelContactSensor.cs`，
> 参数集中在 `LandingDetectorSettings`（ScriptableObject，Inspector 可调）。
>
> 判据从最初"车身角度偏差 + 角速度 + 垂直速度"三项综合，**简化成只看前后轮有效接地的
> 时间差（Δt）**——不再读坡度/角度/角速度/垂直速度。三档也改名成
> **Perfect / Good / Not Bad**（不再叫 Bad，强调"完成了一次有效落地"）：
>
> -   `Δt <= perfectThreshold`（默认 0.02s）→ Perfect
> -   `Δt <= goodThreshold`（默认 0.05s）→ Good
> -   超过 `goodThreshold`，或等到 `landingTimeout`（默认 0.15s）另一只轮子还没落地 → Not Bad
>
> `ContactOrder`（`Simultaneous`/`FrontFirst`/`BackFirst`）跟 Quality 是两个独立结果，
> 但共用 `goodThreshold` 当"够不够接近"的边界。`LandingDetector.cs` 内部是一个显式状态机
> （Airborne → Pending → Grounded），一次腾空只判定一次，落地这一刻结果就固定了。
>
> `CameraDirector` 订阅了这个判定结果：质量越差，落地回弹镜头越明显。`RunManager` 也已经
> 接了 `OnLanded`，会弹 `PERFECT!`/`GOOD`/`NOT BAD`（独立的 `ToastText`，见 3.5 节）。
>
> **还没做的**：`AudioManager` 的 `landing` 音效槽位（3.10 节）现在对三档播的还是同一个
> 音效，没有分级；三档阈值是估的第一版数字，还没有经过大量实机测试微调手感。
> **Combo 加成已经不需要了**——Combo 系统本身已经整体移除，见第 0 节 2026-09-22 补充。

这是目前最重要的玩法增强。

自行车落地不应该只有：

> 落地 / 摔车

而应该根据落地瞬间的状态计算质量。

### 结果

-   **Perfect Landing**
-   **Good Landing**
-   **Bad Landing**
-   **Crash**

### 判断因素

可以综合：

-   前后轮接触顺序
-   车身与地面坡度的夹角
-   落地瞬间角速度
-   垂直速度
-   落地时速度
-   是否在空中完成特技
-   是否接近危险姿态

### 玩家反馈

Perfect Landing 应该明显比普通落地更有反馈：

-   UI "PERFECT!"
-   分数增加
-   Combo 增加
-   轻微镜头回弹
-   短促音效
-   屏幕轻微反馈

目标是让玩家产生：

> "我刚才那个 360 落得特别漂亮。"

这应该成为游戏最核心的正反馈之一。

------------------------------------------------------------------------

# 5. Trick / 空中特技系统

> **已实现，2026-09-22 正式改成按圈数计分**——`TrickSystem.cs`，最初是照下面 5.1 节的
> 精确角度档位（90/180/360/540/720°）实现的，重构后改成**只看完整转了几圈**：
> `laps = Floor(Abs(SpinAccumulatedDegrees) / 360)`，不足一圈不计分，默认每圈
> `50/150/300/500/750` 分（`TrickSystemSettings.scorePerLap`）。**"720° 但落地失败、
> 奖励归零"这条规则已经拿掉**——`TrickSystem` 现在完全不读 `LandingDetector.Quality`，
> 好落地/差落地转出同样的圈数拿一样的分，两个系统彻底解耦。下面 5.1/5.2 节的精确角度档位
> 设计已经不是实现依据了，留着作为历史记录。

目前的 360° 空翻需要从"一个操作功能"升级成"风险---奖励系统"。

## 5.1 旋转计分（已被上面的圈数计分取代，仅作历史记录）

根据实际旋转角度给予不同奖励：

-   90°：基础动作
-   180°：小奖励
-   360°：主要特技
-   540°：高风险
-   720°：高风险高奖励

只有**成功落地**后才正式结算奖励。

如果玩家在空中完成 720°，但落地失败：

> 奖励归零 + 摔车

这样才能形成真正的 Risk / Reward。（**这条"落地失败奖励归零"的规则已经不再生效**，见上方状态说明。）

## 5.2 Trick Chain

连续完成特技可以形成：

-   360 → Perfect Landing
-   360 → 360 → Perfect Landing
-   540 → Near Miss → Perfect Landing

逐渐形成更高的 Trick Score。

未来可以加入：

-   Backflip
-   Frontflip
-   Wheelie
-   Air Rotation
-   Near Miss
-   Perfect Landing

------------------------------------------------------------------------

# 6. Combo 连击系统

> **已移除，不在当前实现范围内**（2026-09-22）：实现过一版（`ComboSystem.cs`，纯计数器，
> Perfect/Good 落地或 Near Miss 触发 `comboCount++`，超时或摔车清零），评估后认为太复杂、
> 又一直没有真正落地这一节要求的"真正影响 Score Multiplier/Trick Score/金币"，直接砍掉了，
> 详见第 0 节 2026-09-22 补充。下面的设计内容留着作为历史记录——以后要重新考虑连击机制，
> 先重新拍板要不要做、打算怎么接入分数，不要直接照抄这里重新实现一版纯计数器。

Combo 是连接"物理系统"和"分数系统"的重要桥梁。

可以通过以下行为增加 Combo：

-   Perfect Landing
-   完成空翻
-   连续特技
-   Near Miss
-   Wheelie
-   高速骑行
-   连续成功跳跃

Combo 应该有一个短暂的持续时间。

例如：

> 成功动作 → Combo ×2 → 继续动作 → Combo ×3 → 失败或长时间没有动作 →
> Combo 清零

Combo 不应该只影响 UI，而应该真正影响：

-   Score Multiplier
-   Trick Score
-   金币 / 资源
-   后期升级收益

------------------------------------------------------------------------

# 7. Near Miss 系统

玩家高速擦过障碍物时，不应该永远只有"没撞到"。

如果距离障碍物非常近：

> Near Miss

给予：

-   Score
-   Combo
-   特效
-   音效
-   少量镜头反馈

这样可以鼓励玩家：

> "我不是在躲障碍，我是在故意贴着障碍过去。"

这会显著提高游戏的风险感。

------------------------------------------------------------------------

# 8. Speed Risk：速度风险系统

速度不应该只是一个 HUD 数字。

应该形成：

> **速度越高 → 收益越高 → 操作越难 → 风险越高**

高速状态可以提高：

-   Score Gain
-   Combo Gain
-   Near Miss Reward
-   Trick Reward

但同时：

-   落地容错降低
-   障碍反应时间缩短
-   车身惯性更加明显
-   高速摔车更加危险

暂时不要用简单的"超过某速度就强制减速"解决问题。

优先让玩家通过物理系统自己承担高速风险。

------------------------------------------------------------------------

# 9. Event / Landmark Terrain：事件型地形

目前地形主要是：

> 平地 → 上坡 → 下坡 → 平地

后续需要从"随机地形"升级到"随机地形 + 设计好的事件"。

不要完全依赖随机生成。

## 9.1 Landmark Examples

### Big Jump

连续下坡 → 大型跳台 → 长距离飞跃

目标：

-   高速
-   长时间空中
-   多次旋转
-   Perfect Landing

### Rock Garden

多个小型障碍连续出现。

目标：

-   控制速度
-   小幅跳跃
-   精确落地

### Broken Bridge

出现一个较大的缺口。

目标：

-   保持高速
-   跳跃
-   空中调整姿态

### Downhill Trap

高速下坡后突然进入复杂障碍区。

目标：

-   迫使玩家提前减速
-   测试玩家对速度的管理

### Trick Ramp

专门用于特技的跳台。

目标：

-   给玩家一个主动做高风险 Trick 的机会

------------------------------------------------------------------------

# 10. Chunk-based 无限生成

未来地形建议逐渐从：

> 单纯随机生成曲线

升级为：

> **Chunk → Event → Chunk → Event**

每一个 Chunk 可以拥有自己的：

-   长度
-   坡度
-   障碍组合
-   跳台
-   风险等级
-   环境主题
-   推荐速度

这样可以保证：

-   随机性
-   可控性
-   可设计性
-   难度递增

同时避免出现"随机生成了一段实际上根本无法通过的地形"。

------------------------------------------------------------------------

# 11. 难度曲线

随着距离增加，游戏应该逐渐增加：

### Early Game

-   平缓地形
-   少量障碍
-   较高落地容错
-   主要让玩家学习操作

### Mid Game

-   更多坡度变化
-   更高速度
-   障碍组合
-   开始出现 Landmark
-   玩家开始主动做 Trick

### Late Game

-   高速
-   大型跳跃
-   连续障碍
-   Near Miss
-   更低的容错
-   高价值 Combo

难度应该主要通过**场景复杂度和玩家决策压力**增加，而不是简单提高移动速度。

------------------------------------------------------------------------

# 12. Roguelike 局内三选一系统

暂时不要做复杂的 RPG。

每经过一定距离，例如：

> 500m / 1000m

弹出 3 个随机升级，让玩家选择一个。

## 示例

### Aerodynamic

-   Max Speed

### Soft Suspension

-   Landing Forgiveness

### Turbo

-   Shift Acceleration

### Trick Master

-   Trick Score

### Daredevil

-   Near Miss Reward

### Balance Control

-   空中姿态控制

### Combo Master

-   Combo 持续时间

------------------------------------------------------------------------

# 13. Build 构筑方向

后期可以自然形成三个主要 Build。

## Speed Build

核心：

-   Max Speed
-   Acceleration
-   Downhill Performance

玩法：

> 高速 + 高风险

------------------------------------------------------------------------

## Trick Build

核心：

-   Trick Score
-   Air Control
-   Combo Multiplier
-   Landing Bonus

玩法：

> 跳跃 + 空翻 + Perfect Landing

------------------------------------------------------------------------

## Control Build

核心：

-   Landing Forgiveness
-   Stability
-   Suspension
-   Crash Resistance

玩法：

> 稳定、长距离、生存

三种 Build 应该改变玩家的游戏方式，而不只是增加数字。

------------------------------------------------------------------------

# 14. 是否加入 HP / Shield

当前版本建议：

> **暂时不要加入 HP / Shield。**

目前：

> Crash = Run End

反而比较干净。

因为游戏的核心是：

> "我能不能控制住这辆自行车？"

如果加入大量 HP / Shield：

> 撞一下 → 掉血 → 继续跑

可能会削弱物理风险感。

以后如果需要，可以把 HP / Shield 作为特殊 Build
或局外系统，而不是基础规则。

------------------------------------------------------------------------

# 15. Camera 反馈增强

现有 CameraDirector 已经具备很好的基础。

下一阶段可以让镜头更明确地参与游戏反馈。

## Perfect Landing

-   短暂镜头 Punch
-   微弱 zoom
-   UI 强反馈

## 大型 Trick

-   空中逐渐拉远
-   落地瞬间回弹
-   根据旋转角度增强反馈

## Near Miss

-   极短镜头震动
-   音效
-   UI 提示

## High Combo

-   镜头轻微动态变化
-   HUD 强化

注意：

> Camera feedback 应该服务于玩法，而不是变成持续的视觉噪音。

------------------------------------------------------------------------

# 16. UI 设计方向

目前：

-   Distance
-   Speed

后续可以增加：

### 左上角

-   Distance
-   Speed

### 中上方

-   Combo
-   Score Multiplier

### 动作发生时

显示：

-   PERFECT LANDING
-   360
-   540
-   NEAR MISS
-   COMBO x5

### Crash Screen

显示：

-   Distance
-   Score
-   Best Distance
-   Highest Combo
-   Best Trick
-   Restart

------------------------------------------------------------------------

# 17. 局外 Meta Progression

当核心玩法稳定后，再加入局外成长。

例如：

-   新自行车
-   新车架
-   新轮胎
-   新悬挂
-   新车轮
-   新技能
-   Cosmetic

不要让局外成长直接破坏基础操作。

优先考虑：

> 改变玩法风格，而不是单纯 +10% / +20% 数值。

------------------------------------------------------------------------

# 18. 自行车部件构筑

可以把自行车拆成：

-   Frame
-   Wheels
-   Suspension
-   Brakes
-   Drivetrain

每个部件影响不同属性。

例如：

### Lightweight Frame

-   Acceleration\
-   Stability

### Heavy Frame

-   Acceleration\
-   Stability

### Off-road Tires

-   Landing Stability\
-   Max Speed

### Racing Tires

-   Max Speed\
-   Off-road Control

这样可以进一步强化 Speed / Trick / Control 三种 Build。

------------------------------------------------------------------------

# 19. 视觉与音频

目前：

-   地形：纯色网格（顶点色渐变+噪声做了一点明暗层次，仍然是程序化生成，不是贴图）
-   障碍物：彩色方块
-   自行车：已经换成真实的自行车线稿贴图（`Assets/prefab/Bike.prefab`），不再是纯色占位方块，
    但也不是最终成品美术
-   背景：已经是正式的多层视差像素美术（`Assets/Nature Backgrounds Pixel Art`，见 3.9 节），
    不是占位

下一阶段再进入正式美术（主要是地形/障碍物）。

音频框架已经搭好（见 3.10 节 `AudioManager.cs`），但场景里还没有挂任何实际音频文件，
下面这份清单里的类别，跟 `AudioManager` 现有槽位不是一一对应的关系。

## Visual Direction

优先考虑：

-   清晰的自行车轮廓
-   明确的地形轮廓
-   高对比障碍物
-   强烈的速度反馈
-   简洁但有风格的背景

不要让正式美术破坏物理判定的可读性。

## Audio

优先制作：

1.  Bicycle chain / motor
2.  Tire rolling
3.  Jump
4.  Landing
5.  Perfect Landing
6.  Trick
7.  Near Miss
8.  Crash
9.  UI selection
10. Background music

------------------------------------------------------------------------

# 20. 技术栈

-   Unity `6000.0.63f1`
-   Universal Render Pipeline 2D
-   `Rigidbody2D`
-   `WheelJoint2D`
-   Fixed Timestep `0.005`（200Hz）
-   Cinemachine `3.1.6`
-   DOTween
-   `ScriptableObject` 配置系统

------------------------------------------------------------------------

# 21. 当前技术原则

## 物理优先

速度、上下坡加减速等尽量来自物理。

不要为了"看起来更快"而直接：

> transform.position += ...

## Physics / Visual 分离

物理组件和视觉组件继续保持解耦。

尤其不要直接修改物理轮子的 Transform。

## Data-driven

继续使用：

-   `EndlessRunSettings`
-   `CameraDirectorSettings`

未来升级系统也应该尽量采用 ScriptableObject。

例如：

-   `BikePartData`
-   `UpgradeData`
-   `TerrainChunkData`
-   `ObstacleData`

## 系统解耦

建议保持：

-   BikeController
-   CrashDetector
-   LandingDetector
-   TrickSystem
-   ~~ComboSystem~~（已实现又整体移除，见第 0/6 节 2026-09-22 的记录）
-   ScoreSystem
-   UpgradeSystem
-   TerrainGenerator
-   ObstacleSpawner
-   CameraDirector
-   RunManager

之间职责清晰。

------------------------------------------------------------------------

# 22. 建议新增代码系统

下一阶段建议新增：

``` text
LandingDetector.cs        [已完成]
WheelContactSensor.cs     [已完成，原文档未列出，LandingDetector 的前置依赖]
TrickSystem.cs            [已完成]
ComboSystem.cs            [已实现又整体移除，2026-09-22，见第 0/6 节]
ScoreSystem.cs
NearMissDetector.cs
TerrainChunkData.cs
TerrainEventSystem.cs
UpgradeSystem.cs
UpgradeData.cs
```

职责：

### LandingDetector

检测：

-   两轮接触
-   车身角度
-   角速度
-   垂直速度
-   Landing Quality

### TrickSystem

记录：

-   Rotation Angle
-   Trick Type
-   Trick Score

### ComboSystem（已移除，见第 0/6 节，以下负责范围仅作历史记录）

负责：

-   Combo Count
-   Multiplier
-   Combo Timer
-   Combo Reset

### ScoreSystem

统一计算：

-   Distance Score
-   Trick Score
-   Landing Score
-   Near Miss Score
-   ~~Combo Multiplier~~（Combo 已移除，不需要这一项了）

### NearMissDetector

检测：

-   玩家与障碍物距离
-   相对速度
-   是否成功擦过

### TerrainChunkData

定义一个可重复使用的地形事件：

-   Chunk Length
-   Height
-   Obstacles
-   Jump Ramp
-   Recommended Speed
-   Difficulty

------------------------------------------------------------------------

# 23. 开发优先级

## P0 --- 核心体验

优先完成：

1.  ~~Landing Quality~~ **已完成**
2.  ~~Trick Score~~ **已完成**
3.  ~~Combo~~ **已实现又整体移除**（2026-09-22，太复杂、没真正接入分数，见第 0/6 节）
4.  ~~Near Miss~~ **已完成**

目标：

> 让玩家因为"骑得漂亮"获得奖励。

------------------------------------------------------------------------

## P1 --- 内容与风险

然后完成：

5.  Event / Landmark Chunks
6.  Obstacle Combinations
7.  Speed Risk
8.  难度曲线

目标：

> 让玩家主动选择风险。

------------------------------------------------------------------------

## P2 --- Roguelike

然后完成：

9.  三选一局内 Upgrade
10. Speed / Trick / Control Build
11. 自行车部件
12. 局外 Meta Progression

目标：

> 让每一局有不同玩法。

------------------------------------------------------------------------

## P3 --- Presentation

最后完成：

13. 正式美术
14. 音效
15. 音乐
16. UI Polish
17. Camera Polish
18. 移动端输入
19. Save / Persistence

------------------------------------------------------------------------

# 24. 暂未完成 / Roadmap

已完成（见 0 节、4.1 节）：

-   Landing Quality
-   Trick Score
-   ~~Combo~~（已实现又整体移除，2026-09-22，见第 0/6 节）
-   Near Miss

P0 范围内仍欠账（见 0 节展开）：

-   统一的 ScoreSystem（把 Distance/Trick 揉到一起；Combo 已移除，不用再考虑它的 Multiplier 了）
-   ~~"PERFECT!" 落地专属弹字~~ —— **已完成**（2026-09-22，见第 0/4.1 节）
-   摔车结算画面的 Score / Best Distance / Best Trick（Combo 已移除，不用再要 Highest Combo 了）

目前尚未完成：

-   Event / Landmark Terrain
-   Speed Risk
-   ~~Roguelike 三选一 Upgrade~~ —— **已完成**（`NodeManager.cs` 调度，见 3.x 节，README
    有详细说明），这条从"未完成"移出去了；这里错过一次没更新，导致 2026-09-22 有新开的
    session 看着这份文档以为它还没做，教训是这种列表跟实现进度分叉了要随手改，不要攒着
-   Speed / Trick / Control Build（把三选一的选项归类成"流派"、给流派搭配加成的那一层，
    不是三选一本身——这层确实还没做）
-   自行车部件构筑
-   局外 Meta Progression（花齿轮/永久解锁——`GearManager` 现在只有 `AddGear`，没有
    `SpendGear`）
-   正式美术（**部分完成**：背景`Background.prefab`已经是真实 craftpix 像素美术、车身
    `Bike.prefab`已经是真实线稿图，地形`EndlessTerrainGenerator`/障碍物`ObstacleSpawner`
    依然是纯色网格和色块）
-   音效 / 音乐（**部分完成**：`AudioManager` 的 `bgm`/`ambient` 两个槽位已经接了真实音频
    文件`Assets/Audio/bgm_main01.mp3`/`sfx_env_main01.mp3`，运行起来有背景音乐/环境音；
    `ride`/`landing`/`boost`/`crash` 四个槽位还是空的）
-   移动端输入（核心操作——A/D 加速、Space 跳跃/空翻——依然是键盘专属，只有 Boost/暂停
    这两个按钮本来就是 UI 按钮，天然兼容触屏点击）
-   存档 / 进度持久化（**部分完成**：`RunManager` 的最远距离、`GearManager` 的齿轮数量都
    已经用 `PlayerPrefs` 存了，没有更复杂的存档/解锁状态需要存，因为还没有能花齿轮换的东西）

已知问题（跟踪在 GitHub Issues）：

-   [#1](https://github.com/Chaos487/RidingBike/issues/1) `IsGrounded()` 地面检测射线不够长，车身静止时误判为空中——已修复并实机验证，issue 待手动关闭
-   [#2](https://github.com/Chaos487/RidingBike/issues/2) 空中长按空格无法触发旋转，A/D 也无法控制空中姿态——待排查
-   [#4](https://github.com/Chaos487/RidingBike/issues/4) `NodePanel`/`MenuPanel`/`PausePanel` 背景的真实屏幕模糊一直没有视觉效果——先后试过三版技术路线(手写 Renderer Feature 用错 API、Shader Graph 单 Pass 采样有天花板、Render Graph 新 API 多趟降采样/升采样),最新这版排查掉了黑屏/Render Graph 报错/Editor Scene 视图摄像机抢占共享贴图这几个问题之后依然没有模糊效果，原因待查，已搁置
-   [#5](https://github.com/Chaos487/RidingBike/issues/5) Trick System 圈数判定基本可用，但仍偶尔不稳定——2026-09-22 连续修了三个 bug（惯性旋转没算进去、前后轮谁先触地导致漏记旋转、腾空途中蹭地被误判成落地），用户复测后表示主要问题已解决但仍偶发不稳定，具体触发条件待继续排查

------------------------------------------------------------------------

# 25. MVP 目标

第一版真正可玩的 MVP 不需要所有系统。

只需要：

-   自行车物理
-   无限地形
-   障碍物
-   跳跃
-   360
-   Landing Quality
-   Trick Score
-   Combo
-   Near Miss
-   Distance
-   Score
-   Crash / Restart

如果这一版已经让玩家产生：

> "再来一次，我这次要跑得更远。"

并且：

> "刚才那个 360 我落得太漂亮了。"

那么核心玩法已经成立。

------------------------------------------------------------------------

# 26. 最终设计原则

**RidingBike 不应该是一个"有自行车的 endless runner"。**

它真正应该成为：

> **一个以自行车物理、速度管理、空中姿态、特技和落地质量为核心的 endless
> stunt game。**

玩家的主要决策不是：

> "我要不要躲开这个东西？"

而是：

> "我要保持多快的速度？"\
> "我要不要跳？"\
> "我要不要冒险做一个 360？"\
> "我要不要再转一圈？"\
> "我能不能完美落地？"\
> "我要不要为了 Combo 冒险贴着障碍过去？"

最终的核心体验应该是：

> **Speed → Risk → Trick → Landing → Reward → Combo → Higher Risk**

这条循环比单纯增加更多地图、更多障碍、更多数值升级更加重要。
