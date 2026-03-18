using UnityEngine;

public class BikeController : MonoBehaviour
{
    public WheelJoint2D backWheelJoint; // 拖入后轮的关节
    public float speed = -1500f; // 马达速度
    public float torque = 10000f; // 马达扭矩

    void Update()
    {
        // 获取水平输入（A/D 或 左右键）
        float movement = Input.GetAxisRaw("Horizontal");
        
        JointMotor2D motor = backWheelJoint.motor;
        
        if (movement != 0) {
            motor.motorSpeed = movement * speed;
            motor.maxMotorTorque = torque;
            backWheelJoint.useMotor = true;
        } else {
            backWheelJoint.useMotor = false;
        }
        
        backWheelJoint.motor = motor;
    }
}