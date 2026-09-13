using System;
using UnityEngine;

/// <summary>
/// 判定"刚落地"这一刻的质量:车身角度是否贴合当地坡度、角速度/垂直速度是否够小、
/// 前后轮谁先触地。只覆盖"正常落地"的场景——摔到判定摔车的程度由 CrashDetector
/// 独立处理,两者不重叠(LandingDetector 只在 BikeController.IsGrounded 从假变真的
/// 那一帧采样一次,不持续判定)。
/// </summary>
public class LandingDetector : MonoBehaviour
{
    public enum Quality { Perfect, Good, Bad }
    public enum ContactOrder { Simultaneous, FrontFirst, BackFirst }

    float perfectMaxAngleError = 12f;
    float perfectMaxAngularSpeed = 90f;
    float perfectMaxVerticalSpeed = 4f;

    float goodMaxAngleError = 30f;
    float goodMaxAngularSpeed = 200f;
    float goodMaxVerticalSpeed = 8f;

    float simultaneousContactWindow = 0.05f;

    BikeController bike;
    WheelContactSensor frontSensor;
    WheelContactSensor backSensor;

    bool wasGrounded = true;

    /// <summary>每次判定出一次"刚落地"时触发,带质量分级和前后轮接触顺序。</summary>
    public event Action<Quality, ContactOrder> OnLanded;

    public void Initialize(BikeController bikeController, WheelContactSensor front, WheelContactSensor back)
    {
        bike = bikeController;
        frontSensor = front;
        backSensor = back;
        wasGrounded = bike.IsGrounded();
    }

    public void ApplySettings(LandingDetectorSettings settings)
    {
        if (settings == null) return;

        perfectMaxAngleError = settings.perfectMaxAngleError;
        perfectMaxAngularSpeed = settings.perfectMaxAngularSpeed;
        perfectMaxVerticalSpeed = settings.perfectMaxVerticalSpeed;
        goodMaxAngleError = settings.goodMaxAngleError;
        goodMaxAngularSpeed = settings.goodMaxAngularSpeed;
        goodMaxVerticalSpeed = settings.goodMaxVerticalSpeed;
        simultaneousContactWindow = settings.simultaneousContactWindow;
    }

    void Update()
    {
        if (bike == null) return;

        bool grounded = bike.IsGrounded();
        if (!wasGrounded && grounded)
        {
            EvaluateLanding();
        }
        wasGrounded = grounded;
    }

    void EvaluateLanding()
    {
        Rigidbody2D rb = bike.bikeRigidbody;

        float targetAngle = bike.GetGroundSlopeAngle();
        float angleError = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, targetAngle));
        float angularSpeed = Mathf.Abs(rb.angularVelocity);
        float verticalSpeed = Mathf.Abs(rb.linearVelocity.y);

        Quality quality;
        if (angleError <= perfectMaxAngleError && angularSpeed <= perfectMaxAngularSpeed && verticalSpeed <= perfectMaxVerticalSpeed)
        {
            quality = Quality.Perfect;
        }
        else if (angleError <= goodMaxAngleError && angularSpeed <= goodMaxAngularSpeed && verticalSpeed <= goodMaxVerticalSpeed)
        {
            quality = Quality.Good;
        }
        else
        {
            quality = Quality.Bad;
        }

        ContactOrder order = DetermineContactOrder();

        // 临时验证用:后面接了 UI/ScoreSystem 展示这个结果之后可以删掉。
        Debug.Log($"[LandingDetector] {quality} order={order} angleError={angleError:0.0} angularSpeed={angularSpeed:0.0} verticalSpeed={verticalSpeed:0.0}");

        OnLanded?.Invoke(quality, order);
    }

    ContactOrder DetermineContactOrder()
    {
        if (frontSensor == null || backSensor == null) return ContactOrder.Simultaneous;

        float diff = frontSensor.LastGroundedTime - backSensor.LastGroundedTime;
        if (Mathf.Abs(diff) <= simultaneousContactWindow) return ContactOrder.Simultaneous;
        return diff < 0f ? ContactOrder.FrontFirst : ContactOrder.BackFirst;
    }
}
