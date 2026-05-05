using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BikeController : MonoBehaviour
{
    [Header("Joints & Bodies")]
    public WheelJoint2D backWheelJoint;
    public WheelJoint2D frontWheelJoint;
    public Rigidbody2D bikeRigidbody;

    [Header("Drive")]
    [Tooltip("最大电机角速度 (deg/s)。绝对值越大极速越高。")]
    public float maxMotorSpeed = 1200f;
    [Tooltip("电机角加速度 (deg/s^2)，控制起步/加速的平滑度。")]
    public float motorAcceleration = 2400f;
    [Tooltip("驱动时电机最大扭矩。过大会让车头翘起、轮子甩飞。")]
    public float driveTorque = 800f;
    [Tooltip("松开按键时的刹车扭矩，让车滑行减速而不是猛停。")]
    public float brakeTorque = 400f;
    [Tooltip("电机方向，+1 或 -1。如果按 D 反而向左请改成 -1。")]
    public float driveDirection = -1f;

    [Header("Speed / Stability Limits")]
    [Tooltip("车身水平速度上限 (m/s)。")]
    public float maxLinearSpeed = 7f;
    [Tooltip("车身最大角速度 (deg/s)，防止失控空翻。")]
    public float maxAngularSpeed = 400f;

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

    float currentMotorSpeed;
    float input;

    void Reset()
    {
        bikeRigidbody = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (bikeRigidbody == null) bikeRigidbody = GetComponent<Rigidbody2D>();
        bikeRigidbody.centerOfMass = centerOfMass;
    }

    void Update()
    {
        input = Input.GetAxisRaw("Horizontal");
    }

    void FixedUpdate()
    {
        DriveBackWheel();
        ApplyBalance();
        ClampVelocities();
    }

    void DriveBackWheel()
    {
        if (backWheelJoint == null) return;

        float targetSpeed = input * driveDirection * maxMotorSpeed;

        // 已经到达水平极速时不再加速 —— 否则轮子继续狂转、车身被限速，
        // 二者速度不匹配会把 WheelJoint 的悬挂拉到极限，视觉上轮子飞出去。
        float bikeSpeed = bikeRigidbody.linearVelocity.x;
        if (Mathf.Abs(input) > 0.01f && Mathf.Abs(bikeSpeed) >= maxLinearSpeed
            && Mathf.Sign(bikeSpeed) == Mathf.Sign(input * driveDirection))
        {
            targetSpeed = currentMotorSpeed; // 维持当前转速，不再往上加
        }

        // 平滑过渡到目标转速，避免瞬时冲击让轮子飞出
        currentMotorSpeed = Mathf.MoveTowards(
            currentMotorSpeed,
            targetSpeed,
            motorAcceleration * Time.fixedDeltaTime
        );

        JointMotor2D motor = backWheelJoint.motor;
        motor.motorSpeed = currentMotorSpeed;
        motor.maxMotorTorque = Mathf.Abs(input) > 0.01f ? driveTorque : brakeTorque;
        backWheelJoint.motor = motor;
        backWheelJoint.useMotor = true; // 始终保留 motor，无输入时作为刹车
    }

    void ApplyBalance()
    {
        bool grounded = IsGrounded();

        // 空中按方向键 → 压头 / 抬头
        if (!grounded && Mathf.Abs(input) > 0.01f)
        {
            bikeRigidbody.AddTorque(-input * driveDirection * airLeanTorque);
        }

        // 自动回正（PD 控制：弹簧拉回水平 + 阻尼抑制摆动）
        if (autoBalanceTorque > 0f)
        {
            float angle = Mathf.DeltaAngle(bikeRigidbody.rotation, 0f);
            float spring = angle * autoBalanceTorque;
            float damping = -bikeRigidbody.angularVelocity * autoBalanceDamping;
            bikeRigidbody.AddTorque((spring + damping) * Time.fixedDeltaTime);
        }
    }

    void ClampVelocities()
    {
        Vector2 v = bikeRigidbody.linearVelocity;
        v.x = Mathf.Clamp(v.x, -maxLinearSpeed, maxLinearSpeed);
        bikeRigidbody.linearVelocity = v;

        bikeRigidbody.angularVelocity = Mathf.Clamp(
            bikeRigidbody.angularVelocity,
            -maxAngularSpeed,
            maxAngularSpeed
        );
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
