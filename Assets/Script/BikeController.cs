using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody2D))]
public class BikeController : MonoBehaviour
{
    [Header("Joints & Bodies")]
    public WheelJoint2D backWheelJoint;
    public WheelJoint2D frontWheelJoint;
    public Rigidbody2D bikeRigidbody;

    [Header("Drive (自动巡航，玩家不再手动控制前进/后退)")]
    [Tooltip("最大电机角速度 (deg/s)。绝对值越大极速越高，需要大于\"按轮径换算出 maxSpeedKmh 所需的角速度\"，否则电机转速会先于车速封顶。电机始终朝这个转速全力驱动，实际车速由地形坡度和下面的保底/封顶共同决定。")]
    public float maxMotorSpeed = 2800f;
    [Tooltip("电机角加速度 (deg/s^2)，控制起步/爬坡时电机转速追赶目标值的平滑度。")]
    public float cruiseMotorAcceleration = 3000f;
    [Tooltip("巡航扭矩，决定电机能扛住多陡的坡、多快追回保底速度。过大会让车头翘起、轮子甩飞。")]
    public float cruiseTorque = 2000f;
    [Tooltip("电机方向，+1 或 -1。如果车反而往左开请改成 -1。")]
    public float driveDirection = -1f;

    [Header("保底前进速度")]
    [Tooltip("车速永远不会低于这个值 (km/h)——不管坡多陡，ClampVelocities 里会直接把速度钳在这个值以上。")]
    public float baselineSpeedKmh = 25f;
    [Tooltip("车速低于保底时，每秒最多把速度往回拉这么多 (m/s²)。不能直接每步瞬间钉死在保底值——" +
             "物理算出来的实际速度经常在保底附近小幅波动，硬 Clamp 会导致有的步什么都不做、有的步瞬间拉回一截，" +
             "这种来回横跳就是前进感觉\"一卡一卡\"的根源；改成限速追赶后，追回保底的过程本身也是平滑的。")]
    public float floorRecoverAcceleration = 60f;

    [Header("Boost (Shift 氮气加速，一次性瞬间加速+随时间衰减，按行驶距离充能)")]
    [Tooltip("充能一次需要行驶多远 (米)。从上次使用/游戏开始算起，累计前进这么远才能再按 Shift。")]
    public float boostRechargeDistance = 150f;
    [Tooltip("触发瞬间给保底速度叠加多少 (km/h)，之后按 boostDecayPerSecondKmh 逐渐衰减回 0。")]
    public float boostSpeedBonusKmh = 40f;
    [Tooltip("boost 加成每秒衰减多少 (km/h/s)。")]
    public float boostDecayPerSecondKmh = 30f;

    [Header("Speed / Stability Limits")]
    [Tooltip("车身速度上限 (km/h)，模拟现实骑行速度，达到后车速不再增加，boost 也不能突破这个封顶。")]
    public float maxSpeedKmh = 100f;
    [Tooltip("车身最大角速度 (deg/s)，防止失控空翻。")]
    public float maxAngularSpeed = 400f;

    /// <summary>车身速度上限，换算成物理用的 m/s。</summary>
    public float MaxLinearSpeed => maxSpeedKmh / 3.6f;

    [Header("Balance")]
    [Tooltip("自动回正强度，车身倾斜时把它拉回水平。0 = 关闭。")]
    public float autoBalanceTorque = 25f;
    [Tooltip("回正阻尼，抑制摇摆震荡。")]
    public float autoBalanceDamping = 4f;
    [Tooltip("地面检测距离（从车身中心向下）。车身静止时车身中心到地面实测大约 0.95 米，这个值必须明显大于它，不然 IsGrounded 永远判定为空中——跳跃、坡度贴合、摔车判定、落地质量全都依赖这个方法。")]
    public float groundCheckDistance = 1.2f;
    public LayerMask groundLayer = ~0;

    [Header("Center Of Mass")]
    [Tooltip("调低重心可以避免车头一加速就翘起。")]
    public Vector2 centerOfMass = new Vector2(0f, -0.5f);

    [Header("Slope Probe")]
    [Tooltip("坡度探测点相对车身中心的前后偏移距离，大致等于轮距的一半。")]
    public float slopeProbeOffset = 0.8f;
    [Tooltip("坡度探测射线的最大距离。")]
    public float slopeProbeDistance = 2f;

    [Header("Wheel Visual Spin")]
    [Tooltip("前轮贴图变换。留空的话会自动从 frontWheelJoint 连接的车轮读取，不需要手动拖。")]
    public Transform frontWheelVisual;
    [Tooltip("后轮贴图变换。留空的话会自动从 backWheelJoint 连接的车轮读取，不需要手动拖。")]
    public Transform backWheelVisual;
    [Tooltip("轮子贴图的旋转方向，如果转起来是反的就改成 -1。这个只影响视觉，不影响驱动物理。")]
    public float wheelSpinDirection = 1f;

    [Header("Jump / Spin")]
    [Tooltip("起跳基础冲量(静止、平地起跳时的力度)。这个值算出来的跳跃高度必须明显超过 groundCheckDistance，不然 IsGrounded 全程判定为触地，跳跃等于没发生。")]
    public float jumpForce = 18f;
    [Tooltip("车速对起跳力度的加成上限，满速起跳时在基础值上加这么多——车越快，跳得越高。")]
    public float speedJumpBonus = 8f;
    [Tooltip("下坡起跳的额外加成上限，坡度达到/超过 maxDownhillAngleForBonus 时在(基础值+车速加成)上再加这么多——模拟冲下坡道被\"弹\"得更高更远、滞空更久的感觉；水平速度本来就是保留的，跳得越高滞空越久，自然就冲得越远。")]
    public float downhillJumpBonus = 6f;
    [Tooltip("下坡角度(度)达到这个值就算\"满额\"下坡加成，0 到这个值之间线性插值；只在下坡时生效，平地/上坡起跳没有这份加成。")]
    public float maxDownhillAngleForBonus = 20f;
    [Tooltip("空中按住空格多久后触发 360 度旋转（秒）。")]
    public float spinHoldThreshold = 0.15f;
    [Tooltip("旋转时的角速度 (deg/s)，越大转得越快。")]
    public float spinAngularSpeed = 720f;

    [Header("Invulnerability Flash")]
    [Tooltip("摔车扣血后的无敌期间，车身贴图一亮一灭切换一次的间隔(秒)，越小闪得越快。")]
    public float invulnerabilityFlashInterval = 0.1f;

    /// <summary>Shift 加速真正触发的那一刻抛出(冷却中按 Shift 不会触发)，给音效/特效订阅用。</summary>
    public event Action OnBoost;

    float currentMotorSpeed;
    float input;

    float currentBoostBonusKmh;
    float xAtLastBoost;

    bool actionHeld;
    float spaceHoldTime;
    bool isSpinning;
    bool hasSpunThisAirtime;
    bool hasJumpedThisAirtime;
    float spinDirection;
    float spinAccumulatedDeg;
    bool wasGroundedForSpin = true;

    float frontWheelRadius;
    float backWheelRadius;
    float frontWheelSpinDeg;
    float backWheelSpinDeg;
    Transform frontSpinVisual;
    Transform backSpinVisual;

    readonly List<SpriteRenderer> flashRenderers = new List<SpriteRenderer>();
    Coroutine flashCoroutine;

    /// <summary>是否正在执行主动触发的空中 360 旋转。外部系统(比如摔车判定)据此排除这种合法的高倾角状态。</summary>
    public bool IsSpinning => isSpinning;

    /// <summary>boost 加成是否还没衰减完。外部系统(比如镜头)据此做出反应。</summary>
    public bool IsBoosting => currentBoostBonusKmh > 0.01f;

    /// <summary>从上次触发 boost(或游戏开始)到现在，车身净前进了多少米。</summary>
    public float DistanceSinceLastBoost => bikeRigidbody != null ? bikeRigidbody.position.x - xAtLastBoost : 0f;

    /// <summary>boost 是否已经充能完毕，可以再次触发。</summary>
    public bool IsBoostReady => DistanceSinceLastBoost >= boostRechargeDistance;

    /// <summary>距离下一次 boost 充能完毕还差多少米，已就绪时为 0。外部系统(比如 UI 提示)据此显示。</summary>
    public float DistanceUntilBoostReady => Mathf.Max(0f, boostRechargeDistance - DistanceSinceLastBoost);

    /// <summary>前 / 后轮各自的触地传感器，供落地质量判定读取接触顺序等信息。</summary>
    public WheelContactSensor FrontWheelContact { get; private set; }
    public WheelContactSensor BackWheelContact { get; private set; }

    /// <summary>前后轮是否有任意一个真的物理接触到地面。跳跃/旋转的滞空状态机、落地判定、摔车判定都应该用这个，
    /// 不要用下面 IsGrounded() 的距离射线——射线只代表"车身中心离地面够近"，滞空高度不够高时
    /// 射线在还没真正腾空/落地的时候就会先报"触地"，导致旋转被提前打断、摔车在空中被误判。</summary>
    public bool IsWheelGrounded =>
        (FrontWheelContact != null && FrontWheelContact.IsGrounded) ||
        (BackWheelContact != null && BackWheelContact.IsGrounded);

    /// <summary>车架本身(不是轮子)有没有真的物理接触到东西。供 CrashDetector 判断车身是不是
    /// 已经倒地/打滑——车翻倒的时候轮子经常翘空、凑不齐"两轮都触地"，但车架会真的贴到地面。</summary>
    public WheelContactSensor BodyContact { get; private set; }

    /// <summary>本次滞空期间已经累计转了多少度(持续按空格会一直累加，松手或落地才停)。外部系统(比如特技计分)据此判定转出了哪一档。</summary>
    public float SpinAccumulatedDegrees => spinAccumulatedDeg;

    void Reset()
    {
        bikeRigidbody = GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        // 这里必须用 Awake 而不是 Start——EndlessRunBootstrap 是在
        // [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 里读 FrontWheelContact/BackWheelContact 的，
        // 这个回调发生在 Awake 之后、但不保证在 Start 之前，用 Start 的话可能读到还没赋值的 null。
        if (bikeRigidbody == null) bikeRigidbody = GetComponent<Rigidbody2D>();
        bikeRigidbody.centerOfMass = centerOfMass;
        xAtLastBoost = bikeRigidbody.position.x;

        // 没有手动指定的话，直接从关节连接的车轮上取，不需要在 Inspector 里额外拖引用。
        if (frontWheelVisual == null && frontWheelJoint != null && frontWheelJoint.connectedBody != null)
            frontWheelVisual = frontWheelJoint.connectedBody.transform;
        if (backWheelVisual == null && backWheelJoint != null && backWheelJoint.connectedBody != null)
            backWheelVisual = backWheelJoint.connectedBody.transform;

        frontWheelRadius = GetWheelRadius(frontWheelVisual);
        backWheelRadius = GetWheelRadius(backWheelVisual);

        // 转速视觉不能直接改物理轮子自己的 Transform——Rigidbody2D 会把它当成瞬移同步回内部状态，
        // 干扰轮子和地面之间靠摩擦力驱动的滚动，上坡时扭矩本来就紧张，一点干扰就可能把车憋停。
        // 所以另起一个只挂贴图、没有物理组件的子物体，只转它，物理轮子的旋转完全不碰。
        frontSpinVisual = CreateSpinVisual(frontWheelVisual);
        backSpinVisual = CreateSpinVisual(backWheelVisual);

        FrontWheelContact = AttachContactSensor(frontWheelVisual);
        BackWheelContact = AttachContactSensor(backWheelVisual);

        SetupBodyContact();
        CollectFlashRenderers();
    }

    /// <summary>收集无敌闪烁要用到的贴图:实际显示出来的是 frontSpinVisual/backSpinVisual
    /// (轮子贴图被拆出来单独转的那个副本，见上面的注释)，不是原本的 Frontwheel/Backwheel——
    /// 那两个原始物体的 SpriteRenderer 已经在 CreateSpinVisual 里被关掉了，闪它们没有视觉效果。
    /// rack 是纯装饰件，没有拆分，直接闪它自己就行。</summary>
    void CollectFlashRenderers()
    {
        flashRenderers.Clear();

        AddFlashRenderer(frontSpinVisual);
        AddFlashRenderer(backSpinVisual);
        Transform rack = transform.Find("rack");
        if (rack != null) AddFlashRenderer(rack);
    }

    void AddFlashRenderer(Transform target)
    {
        if (target == null) return;
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null) flashRenderers.Add(sr);
    }

    /// <summary>摔车扣血后的无敌期间调用:车身贴图一亮一灭闪烁 duration 秒，提示玩家现在打不到。</summary>
    public void PlayInvulnerabilityFlash(float duration)
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine(duration));
    }

    IEnumerator FlashRoutine(float duration)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            SetFlashRenderersVisible(visible);

            yield return new WaitForSeconds(invulnerabilityFlashInterval);
            elapsed += invulnerabilityFlashInterval;
        }

        SetFlashRenderersVisible(true);
        flashCoroutine = null;
    }

    void SetFlashRenderersVisible(bool visible)
    {
        foreach (SpriteRenderer sr in flashRenderers)
        {
            if (sr != null) sr.enabled = visible;
        }
    }

    /// <summary>车架本身的碰撞体不在这里创建——直接在 Scene 里手动加/调，形状比代码里定几个数字直观。
    /// 这里只负责:不管 Inspector 里挂的是什么形状的碰撞体，都关掉它跟自己两个轮子的物理碰撞
    /// (不然车架碰撞体只要跟悬挂行程内的轮子有一点重叠，就会被当成真碰撞去解算，天天跟
    /// WheelJoint2D 的悬挂力打架，把车晃得站不住)，并把 WheelContactSensor 接上去。</summary>
    void SetupBodyContact()
    {
        if (frontWheelJoint != null) frontWheelJoint.enableCollision = false;
        if (backWheelJoint != null) backWheelJoint.enableCollision = false;

        BodyContact = AttachContactSensor(transform);
    }

    WheelContactSensor AttachContactSensor(Transform wheel)
    {
        if (wheel == null) return null;

        WheelContactSensor sensor = wheel.GetComponent<WheelContactSensor>();
        if (sensor == null) sensor = wheel.gameObject.AddComponent<WheelContactSensor>();
        sensor.groundLayer = groundLayer;
        return sensor;
    }

    static Transform CreateSpinVisual(Transform wheel)
    {
        if (wheel == null) return null;

        SpriteRenderer source = wheel.GetComponent<SpriteRenderer>();
        if (source == null) return wheel; // 没有精灵可分离，退回直接转物理轮子本身

        GameObject visual = new GameObject(wheel.name + "_SpinVisual");
        visual.transform.SetParent(wheel, false);

        SpriteRenderer copy = visual.AddComponent<SpriteRenderer>();
        copy.sprite = source.sprite;
        copy.color = source.color;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder;
        copy.drawMode = source.drawMode;
        copy.size = source.size;
        copy.sharedMaterial = source.sharedMaterial;

        source.enabled = false; // 物理轮子自己不再显示，改由这个跟随子物体显示

        return visual.transform;
    }

    void Update()
    {
        // 不再驱动前进/后退，也不再控制空中姿态——现在唯一的用途是决定按住空格触发旋转时
        // 转向哪一边（见 StartSpin），空中姿态完全交给自动回正和主动旋转（Space）。
        input = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            TryTriggerBoost();
        }

        HandleJumpAndSpin();
    }

    /// <summary>没就绪的时候点了也没用(内部会先检查 IsBoostReady)——手机端的氮气按钮
    /// (RunManager 里接的)直接调这个就行，不用在按钮那边自己再判一遍是否就绪。</summary>
    public void TryTriggerBoost()
    {
        if (!IsBoostReady) return;

        currentBoostBonusKmh = boostSpeedBonusKmh;
        xAtLastBoost = bikeRigidbody.position.x;
        OnBoost?.Invoke();
    }

    void LateUpdate()
    {
        // 轮子贴图旋转和物理完全解耦：物理只负责悬挂/驱动，这里单独按实际车速算出该转多少度，
        // 保证前后轮视觉上转速一致，不受电机转速上限或摩擦力是否跟得上的影响。
        // 只转 SpinVisual 子物体，物理轮子自己的 Transform/Rigidbody2D 完全不碰。
        SpinWheelVisual(frontWheelVisual, frontSpinVisual, frontWheelRadius, ref frontWheelSpinDeg);
        SpinWheelVisual(backWheelVisual, backSpinVisual, backWheelRadius, ref backWheelSpinDeg);
    }

    void SpinWheelVisual(Transform wheel, Transform spinVisual, float radius, ref float accumulatedDeg)
    {
        if (wheel == null || spinVisual == null || radius <= 0f) return;

        float rollSpeed = Vector2.Dot(bikeRigidbody.linearVelocity, transform.right);
        float angularSpeedDeg = (rollSpeed / radius) * Mathf.Rad2Deg;
        accumulatedDeg -= angularSpeedDeg * wheelSpinDirection * Time.deltaTime;

        // spinVisual 是 wheel 的子物体，世界旋转 = wheel 的物理旋转 + 这里设的本地旋转，
        // 所以要用本地旋转把 wheel 自己的物理旋转抵消掉，才能让贴图显示的角度完全由 accumulatedDeg 决定。
        float localZ = accumulatedDeg - wheel.eulerAngles.z;
        spinVisual.localRotation = Quaternion.Euler(0f, 0f, localZ);
    }

    static float GetWheelRadius(Transform wheel)
    {
        if (wheel == null) return 0f;
        CircleCollider2D wheelCollider = wheel.GetComponent<CircleCollider2D>();
        return wheelCollider != null ? wheelCollider.radius * wheel.lossyScale.x : 0f;
    }

    void HandleJumpAndSpin()
    {
        // 起跳判定继续用射线：容忍度高，手感响应快，误判方向是"以为还在地上"，最多多给一次跳跃机会，不危险。
        bool groundedForJump = IsGrounded();
        // 旋转/滞空状态机改用真实轮胎接触：这里误判方向必须是"以为还在空中"才安全——
        // 用射线的话，滞空高度不够大时会在真正腾空/落地前就先报"触地"，把旋转提前打断。
        bool groundedForAirtime = IsWheelGrounded;

        if (groundedForAirtime)
        {
            isSpinning = false;
            hasSpunThisAirtime = false;
            hasJumpedThisAirtime = false;
            spaceHoldTime = 0f;
        }
        else if (wasGroundedForSpin)
        {
            // 刚离地，开始新一次滞空——清零上次的旋转计数，避免特技系统读到上一次滞空的残留角度。
            spinAccumulatedDeg = 0f;
        }
        wasGroundedForSpin = groundedForAirtime;

        // hasJumpedThisAirtime 用真实轮胎接触(groundedForAirtime)才清零，不依赖上面那条容忍度很高的
        // 射线——射线在起跳后一小段时间内可能还没读到"离地"，之前只靠它判断"能不能起跳"，
        // 松手再按一下空格就能在同一次滞空里再跳一次；现在必须真正落地一次才能解锁下一次跳跃。
        if (ActionPressed() && groundedForJump && !isSpinning && !hasJumpedThisAirtime)
        {
            Jump();
        }

        actionHeld = ActionHeld();
        if (actionHeld && !groundedForAirtime && !isSpinning && !hasSpunThisAirtime)
        {
            spaceHoldTime += Time.deltaTime;
            if (spaceHoldTime >= spinHoldThreshold)
            {
                StartSpin();
            }
        }
        else if (!actionHeld)
        {
            spaceHoldTime = 0f;
        }
    }

    // 手机端没有 Space 键：点一下屏幕等价于按一下 Space(触地起跳)，按住屏幕等价于按住
    // Space(空中长按触发旋转)，跟键盘输入是"或"的关系，不是替换——PC 端的空格键继续有效。
    // Input.GetMouseButton* 在触屏设备上会直接读到主触点，不需要额外用 Input.touches。
    // 要排除点在 UI(Start/Confirm 按钮这些)上的那次点击，不然点 UI 会被同时读成一次跳跃/旋转输入。
    static bool ActionPressed() => Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && !IsPointerOverUI());
    static bool ActionHeld() => Input.GetKey(KeyCode.Space) || (Input.GetMouseButton(0) && !IsPointerOverUI());

    static bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void Jump()
    {
        hasJumpedThisAirtime = true;

        // 车身完全静止一段时间后 Rigidbody2D 会休眠(Time To Sleep 默认 0.5 秒)，
        // 休眠状态下设置 linearVelocity/AddForce 不一定能可靠唤醒它，导致跳跃冲量没有效果。
        // 显式唤醒一下，不管是不是真的在睡，零开销零副作用。
        bikeRigidbody.WakeUp();

        // 车速越快、下坡越陡，起跳力度越大——水平速度全程保留不变，跳得越高就滞空越久，
        // 同样的水平速度乘上更长的滞空时间，自然就冲得更远，不需要额外再加一套"滞空时间"逻辑。
        float speedRatio = Mathf.Clamp01(Mathf.Abs(bikeRigidbody.linearVelocity.x) / Mathf.Max(MaxLinearSpeed, 0.01f));
        float slopeAngle = GetGroundSlopeAngle(); // 正值=上坡，负值=下坡
        float downhillRatio = Mathf.Clamp01(-slopeAngle / Mathf.Max(maxDownhillAngleForBonus, 0.01f));
        float effectiveJumpForce = jumpForce + speedJumpBonus * speedRatio + downhillJumpBonus * downhillRatio;

        Vector2 v = bikeRigidbody.linearVelocity;
        v.y = 0f;
        bikeRigidbody.linearVelocity = v;
        bikeRigidbody.AddForce(Vector2.up * effectiveJumpForce, ForceMode2D.Impulse);
    }

    void StartSpin()
    {
        isSpinning = true;
        hasSpunThisAirtime = true;
        spinDirection = Mathf.Abs(input) > 0.01f ? -Mathf.Sign(input) * driveDirection : 1f;
    }

    void FixedUpdate()
    {
        DecayBoost();
        DriveBackWheel();
        ApplySpin();
        ApplyBalance();
        ClampVelocities();
    }

    void DecayBoost()
    {
        currentBoostBonusKmh = Mathf.Max(0f, currentBoostBonusKmh - boostDecayPerSecondKmh * Time.fixedDeltaTime);
    }

    void ApplySpin()
    {
        if (!isSpinning) return;

        bikeRigidbody.angularVelocity = spinDirection * spinAngularSpeed;
        spinAccumulatedDeg += spinAngularSpeed * Time.fixedDeltaTime;

        // 持续按住(空格或触屏)就一直转，可以转出 540°/720° 这种高风险档位；松手就停在当前角度，
        // 剩下交给自动回正/玩家手感去调整落地姿态。
        if (!actionHeld)
        {
            isSpinning = false;
        }
    }

    void DriveBackWheel()
    {
        if (backWheelJoint == null) return;

        // 始终全力朝前巡航，不再读玩家输入——实际车速由地形坡度、保底下限、封顶上限共同决定。
        float targetSpeed = driveDirection * maxMotorSpeed;

        // 已经到达速度上限时不再加速 —— 否则轮子继续狂转、车身被限速，
        // 二者速度不匹配会把 WheelJoint 的悬挂拉到极限，视觉上轮子飞出去。
        if (bikeRigidbody.linearVelocity.x >= MaxLinearSpeed)
        {
            targetSpeed = currentMotorSpeed; // 维持当前转速，不再往上加
        }

        // 平滑过渡到目标转速，避免瞬时冲击让轮子飞出
        currentMotorSpeed = Mathf.MoveTowards(
            currentMotorSpeed,
            targetSpeed,
            cruiseMotorAcceleration * Time.fixedDeltaTime
        );

        JointMotor2D motor = backWheelJoint.motor;
        motor.motorSpeed = currentMotorSpeed;
        motor.maxMotorTorque = cruiseTorque;
        backWheelJoint.motor = motor;
        backWheelJoint.useMotor = true;
    }

    void ApplyBalance()
    {
        if (isSpinning) return;

        // 用真实轮胎接触，不用 IsGrounded() 的距离射线——射线容忍度很高，一次普通跳跃的
        // 大半程都可能被它误判成"已经触地"，导致自动回正一直在追地面坡度而不是回正到水平，
        // 等于空中基本没有回正这件事。
        bool grounded = IsWheelGrounded;

        // 空中不再响应方向键——姿态控制只留空格(特技旋转)一种手段，没按空格就交给下面的
        // 自动回正去摆正，方向键在空中彻底不影响车身角度。

        // 自动回正（PD 控制：弹簧拉回目标角度 + 阻尼抑制摆动）。
        // 触地时目标角度是当地坡度，不是死磕水平——不然会跟悬挂的天然贴合坡面打架；
        // 空中没有坡度参考，退回水平，方便落地时姿态可控。
        if (autoBalanceTorque > 0f)
        {
            float targetAngle = grounded ? GetGroundSlopeAngle() : 0f;
            float angle = Mathf.DeltaAngle(bikeRigidbody.rotation, targetAngle);
            float spring = angle * autoBalanceTorque;
            float damping = -bikeRigidbody.angularVelocity * autoBalanceDamping;
            bikeRigidbody.AddTorque((spring + damping) * Time.fixedDeltaTime);
        }
    }

    /// <summary>探测当前车身前后所在的地面坡度角(度)。触地判定不到时返回 0。外部系统(比如落地质量判定)据此复用同一套探测。</summary>
    public float GetGroundSlopeAngle()
    {
        Vector2 origin = bikeRigidbody.position;
        Vector2 forward = transform.right;

        RaycastHit2D backHit = Physics2D.Raycast(origin - forward * slopeProbeOffset, Vector2.down, slopeProbeDistance, groundLayer);
        RaycastHit2D frontHit = Physics2D.Raycast(origin + forward * slopeProbeOffset, Vector2.down, slopeProbeDistance, groundLayer);

        if (backHit.collider == null || frontHit.collider == null) return 0f;

        Vector2 delta = frontHit.point - backHit.point;
        return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
    }

    void ClampVelocities()
    {
        float maxLinearSpeed = MaxLinearSpeed;

        // 保底前进速度是硬下限：不管坡多陡、有没有被撞得一时减速，最终都不会低于这个值。
        // boost 加成叠加在保底之上、一起被 maxLinearSpeed 封顶，衰减到 0 之后自然回落到纯保底速度。
        float floorSpeed = Mathf.Min(((baselineSpeedKmh + currentBoostBonusKmh) / 3.6f), maxLinearSpeed);

        Vector2 v = bikeRigidbody.linearVelocity;
        // 上限直接封顶没问题——很少触发，且是"多余速度被削掉"，不会有回弹感。
        // 下限不能直接 Clamp：物理算出来的速度经常在保底附近小幅波动，硬 Clamp 每步要么不做要么瞬间拉回一截，
        // 这种来回横跳正是前进"一卡一卡"的根源，改成按 floorRecoverAcceleration 限速追赶。
        v.x = Mathf.Min(v.x, maxLinearSpeed);
        if (v.x < floorSpeed)
        {
            v.x = Mathf.MoveTowards(v.x, floorSpeed, floorRecoverAcceleration * Time.fixedDeltaTime);
        }
        bikeRigidbody.linearVelocity = v;

        if (!isSpinning)
        {
            bikeRigidbody.angularVelocity = Mathf.Clamp(
                bikeRigidbody.angularVelocity,
                -maxAngularSpeed,
                maxAngularSpeed
            );
        }
    }

    /// <summary>车轮是否触地。外部系统(比如镜头的空中/落地反应)据此复用同一套判定。</summary>
    public bool IsGrounded()
    {
        if (bikeRigidbody == null) return false;
        RaycastHit2D hit = Physics2D.Raycast(
            bikeRigidbody.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );
        return hit.collider != null;
    }
}
