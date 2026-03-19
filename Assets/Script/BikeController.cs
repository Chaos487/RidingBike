using UnityEngine;

public class BikeController : MonoBehaviour
{
    public WheelJoint2D backWheelJoint;
    public Rigidbody2D bikeRigidbody; // 拖入 Rack 的 Rigidbody2D
    
    public float speed = -800f; 
    public float torque = 2500f; 
    public float leanTorque = 15f; // 空中平衡/压车头的力度

    void Start()
    {
        // 关键点 1：人为调低重心
        // (0, -0.5) 表示重心在物体中心点下方 0.5 米
        bikeRigidbody.centerOfMass = new Vector2(0, -0.5f);
    }

    void Update()
    {
        float movement = Input.GetAxisRaw("Horizontal");
        
        // --- 驱动逻辑 ---
        JointMotor2D motor = backWheelJoint.motor;
        if (movement != 0) {
            motor.motorSpeed = movement * speed;
            motor.maxMotorTorque = torque;
            backWheelJoint.useMotor = true;
        } else {
            backWheelJoint.useMotor = false;
        }
        backWheelJoint.motor = motor;

        // --- 平衡逻辑 (关键点 2) ---
        // 当你在地面骑行或在空中时，按住左右键会给车身施加旋转力矩
        // 这能让你在起步仰翻时通过按反方向键“压”回来
        if (movement != 0)
        {
            // 注意：这里给刚体加力矩，-movement 是为了按 D 时车头下压（顺时针），按 A 时抬头
            bikeRigidbody.AddTorque(-movement * leanTorque);
        }
    }
}