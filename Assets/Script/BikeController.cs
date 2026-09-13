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

    /// <summary>是否正在执行主动触发的空中 360 旋转。外部系统(比如摔车判定)据此排除这种合法的高倾角状态。</summary>
    public bool IsSpinning => isSpinning;

    void Reset()
    {
        bikeRigidbody = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (bikeRigidbody == null) bikeRigidbody = GetComponent<Rigidbody2D>();
        bikeRigidbody.centerOfMass = centerOfMass;

        // 没有手动指定的话，直接从关节连接的车轮上取，不需要在 Inspector 里额外拖引用。
        if (frontWheelVisual == null && frontWheelJoint != null && frontWheelJoint.connectedBody != null)
            frontWheelVisual = frontWheelJoint.connectedBody.transform;
        if (backWheelVisual == null && backWheelJoint != null && backWheelJoint.connectedBody != null)
            backWheelVisual = backWheelJoint.connectedBody.transform;

        frontWheelRadius = GetWheelRadius(frontWheelVisual);
        backWheelRadius = GetWheelRadius(backWheelVisual);
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
        SpinWheelVisual(frontWheelVisual, frontWheelRadius, ref frontWheelSpinDeg);
        SpinWheelVisual(backWheelVisual, backWheelRadius, ref backWheelSpinDeg);
    }

    void SpinWheelVisual(Transform wheel, float radius, ref float accumulatedDeg)
    {
        if (wheel == null || radius <= 0f) return;

        float rollSpeed = Vector2.Dot(bikeRigidbody.linearVelocity, transform.right);
        float angularSpeedDeg = (rollSpeed / radius) * Mathf.Rad2Deg;
        accumulatedDeg -= angularSpeedDeg * wheelSpinDirection * Time.deltaTime;

        wheel.rotation = Quaternion.Euler(0f, 0f, accumulatedDeg);
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

    float GetGroundSlopeAngle()
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

    bool IsGrounded()
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
