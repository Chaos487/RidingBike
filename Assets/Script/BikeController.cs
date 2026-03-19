using UnityEngine;

public class BikeController : MonoBehaviour
{
    public WheelJoint2D frontWheelJoint; 
    public WheelJoint2D backWheelJoint;
    public float speed = -1500f; // 马达速度
    public float torque = 10000f; // 马达扭矩


    private void FixedUpdate()
    {
        float movement = Input.GetAxisRaw("Horizontal");
        // 设置前轮马达
        JointMotor2D frontMotor = frontWheelJoint.motor;
        frontMotor.motorSpeed = movement * speed; // 根据输入调整速度
        frontMotor.maxMotorTorque = torque; // 设置最大扭矩
        frontWheelJoint.motor = frontMotor; // 应用马达设置

        // 设置后轮马达
        JointMotor2D backMotor = backWheelJoint.motor;
        backMotor.motorSpeed = movement * speed; // 根据输入调整速度
        backMotor.maxMotorTorque = torque; // 设置最大扭矩
        backWheelJoint.motor = backMotor; // 应用马达设置
    }
}