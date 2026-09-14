using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BikeController : MonoBehaviour
{
    [Header("Joints & Bodies")]
    public WheelJoint2D backWheelJoint;
    public WheelJoint2D frontWheelJoint;
    public Rigidbody2D bikeRigidbody;

    [Header("Drive")]
    [Tooltip("最大电机角速度 (deg/s)。绝对值越大极速越高，需要大于\"按轮径换算出 maxSpeedKmh 所需的角速度\"，否则电机转速会先于车速封顶。")]
    public float maxMotorSpeed = 2800f;
    [Tooltip("电机角加速度 (deg/s^2)，控制起步/加速的平滑度。")]
    public float motorAcceleration = 3000f;
    [Tooltip("驱动时电机最大扭矩，决定按住前进键时的加速快慢。过大会让车头翘起、轮子甩飞。")]
    public float driveTorque = 2000f;
    [Tooltip("松开按键时的刹车扭矩，让车滑行减速而不是猛停。")]
    public float brakeTorque = 400f;
    [Tooltip("电机方向，+1 或 -1。如果按 D 反而向左请改成 -1。")]
    public float driveDirection = -1f;

    [Header("Boost (Shift 加速)")]
    [Tooltip("按住加速键(Shift)时使用的驱动扭矩，应明显大于 driveTorque，让加速比平时更快；最高速度不受影响，统一由 maxSpeedKmh 封顶。")]
    public float boostDriveTorque = 3600f;
    [Tooltip("按住加速键时的电机角加速度，通常也要比 motorAcceleration 大，避免电机转速追不上多出来的扭矩。")]
    public float boostMotorAcceleration = 6000f;

    [Header("Speed / Stability Limits")]
    [Tooltip("车身速度上限 (km/h)，模拟现实骑行速度，达到后车速不再增加。")]
    public float maxSpeedKmh = 100f;
    [Tooltip("车身最大角速度 (deg/s)，防止失控空翻。")]
    public float maxAngularSpeed = 400f;

    /// <summary>车身速度上限，换算成物理用的 m/s。</summary>
    public float MaxLinearSpeed => maxSpeedKmh / 3.6f;

    [Header("Balance")]
    [Tooltip("空中按 A/D 时给车身施加的压头/抬头力矩。")]
    public float airLeanTorque = 30f;
    [Tooltip("自动回正强度，车身倾斜时把它拉回水平。0 = 关闭。")]
    public float autoBalanceTorque = 25f;
    [Tooltip("回正阻尼，抑制摇摆震荡。")]
    public float autoBalanceDamping = 4f;
    [Tooltip("地面检测距离（从车身中心向下）。")]
    public float groundCheckDistance = 0.8f;
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
    [Tooltip("跳跃瞬间冲量。")]
    public float jumpForce = 8f;
    [Tooltip("空中按住空格多久后触发 360 度旋转（秒）。")]
    public float spinHoldThreshold = 0.15f;
    [Tooltip("旋转时的角速度 (deg/s)，越大转得越快。")]
    public float spinAngularSpeed = 720f;

    float currentMotorSpeed;
    float input;
    bool boostHeld;

    bool spaceHeld;
    float spaceHoldTime;
    bool isSpinning;
    bool hasSpunThisAirtime;
    float spinRemaining;
    float spinDirection;

    float frontWheelRadius;
    float backWheelRadius;
    float frontWheelSpinDeg;
    float backWheelSpinDeg;
    Transform frontSpinVisual;
    Transform backSpinVisual;

    /// <summary>是否正在执行主动触发的空中 360 旋转。外部系统(比如摔车判定)据此排除这种合法的高倾角状态。</summary>
    public bool IsSpinning => isSpinning;

    /// <summary>是否按住加速键(Shift)。外部系统(比如镜头)据此做出反应。</summary>
    public bool IsBoosting => boostHeld;

    /// <summary>前 / 后轮各自的触地传感器，供落地质量判定读取接触顺序等信息。</summary>
    public WheelContactSensor FrontWheelContact { get; private set; }
    public WheelContactSensor BackWheelContact { get; private set; }

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
        input = Input.GetAxisRaw("Horizontal");
        boostHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        HandleJumpAndSpin();
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
        bool grounded = IsGrounded();

        if (grounded)
        {
            isSpinning = false;
            hasSpunThisAirtime = false;
            spaceHoldTime = 0f;
        }

        if (Input.GetKeyDown(KeyCode.Space) && grounded && !isSpinning)
        {
            Jump();
        }

        spaceHeld = Input.GetKey(KeyCode.Space);
        if (spaceHeld && !grounded && !isSpinning && !hasSpunThisAirtime)
        {
            spaceHoldTime += Time.deltaTime;
            if (spaceHoldTime >= spinHoldThreshold)
            {
                StartSpin();
            }
        }
        else if (!spaceHeld)
        {
            spaceHoldTime = 0f;
        }
    }

    void Jump()
    {
        // 车身完全静止一段时间后 Rigidbody2D 会休眠(Time To Sleep 默认 0.5 秒)，
        // 休眠状态下设置 linearVelocity/AddForce 不一定能可靠唤醒它，导致跳跃冲量没有效果。
        // 显式唤醒一下，不管是不是真的在睡，零开销零副作用。
        bikeRigidbody.WakeUp();

        Vector2 v = bikeRigidbody.linearVelocity;
        v.y = 0f;
        bikeRigidbody.linearVelocity = v;
        bikeRigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    void StartSpin()
    {
        isSpinning = true;
        hasSpunThisAirtime = true;
        spinRemaining = 360f;
        spinDirection = Mathf.Abs(input) > 0.01f ? -Mathf.Sign(input) * driveDirection : 1f;
    }

    void FixedUpdate()
    {
        DriveBackWheel();
        ApplySpin();
        ApplyBalance();
        ClampVelocities();
    }

    void ApplySpin()
    {
        if (!isSpinning) return;

        bikeRigidbody.angularVelocity = spinDirection * spinAngularSpeed;
        spinRemaining -= spinAngularSpeed * Time.fixedDeltaTime;

        if (spinRemaining <= 0f)
        {
            isSpinning = false;
        }
    }

    void DriveBackWheel()
    {
        if (backWheelJoint == null) return;

        float targetSpeed = input * driveDirection * maxMotorSpeed;

        // 已经到达速度上限时不再加速 —— 否则轮子继续狂转、车身被限速，
        // 二者速度不匹配会把 WheelJoint 的悬挂拉到极限，视觉上轮子飞出去。
        float maxLinearSpeed = MaxLinearSpeed;
        float bikeSpeed = bikeRigidbody.linearVelocity.x;
        if (Mathf.Abs(input) > 0.01f && Mathf.Abs(bikeSpeed) >= maxLinearSpeed
            && Mathf.Sign(bikeSpeed) == Mathf.Sign(input * driveDirection))
        {
            targetSpeed = currentMotorSpeed; // 维持当前转速，不再往上加
        }

        // 按住加速键(Shift)时用更大的扭矩/电机加速度，跑得更快到达同一个速度上限。
        float accel = boostHeld ? boostMotorAcceleration : motorAcceleration;
        float torque = boostHeld ? boostDriveTorque : driveTorque;

        // 平滑过渡到目标转速，避免瞬时冲击让轮子飞出
        currentMotorSpeed = Mathf.MoveTowards(
            currentMotorSpeed,
            targetSpeed,
            accel * Time.fixedDeltaTime
        );

        JointMotor2D motor = backWheelJoint.motor;
        motor.motorSpeed = currentMotorSpeed;
        motor.maxMotorTorque = Mathf.Abs(input) > 0.01f ? torque : brakeTorque;
        backWheelJoint.motor = motor;
        backWheelJoint.useMotor = true; // 始终保留 motor，无输入时作为刹车
    }

    void ApplyBalance()
    {
        if (isSpinning) return;

        bool grounded = IsGrounded();

        // 空中按方向键 → 压头 / 抬头
        if (!grounded && Mathf.Abs(input) > 0.01f)
        {
            bikeRigidbody.AddTorque(-input * driveDirection * airLeanTorque);
        }

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
        Vector2 v = bikeRigidbody.linearVelocity;
        v.x = Mathf.Clamp(v.x, -MaxLinearSpeed, MaxLinearSpeed);
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
