using System;
using UnityEngine;

/// <summary>
/// 挂在单个障碍物上:比实心碰撞体大一圈的触发区，车轮进入又离开、
/// 期间没有真的撞上那个实心碰撞体，就算一次贴身擦过(Near Miss)。
/// 触发区和实心碰撞体同一个 GameObject，两个碰撞体互不影响——一个 isTrigger，一个不是。
/// </summary>
public class NearMissDetector : MonoBehaviour
{
    /// <summary>触发一次贴身擦过。</summary>
    public event Action OnNearMiss;

    bool wheelInside;
    bool solidHitDuringPass;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsWheel(other)) return;
        wheelInside = true;
        solidHitDuringPass = false;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsWheel(other)) return;
        wheelInside = false;

        if (!solidHitDuringPass)
        {
            OnNearMiss?.Invoke();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (wheelInside) solidHitDuringPass = true;
    }

    static bool IsWheel(Collider2D other) => other.GetComponent<WheelContactSensor>() != null;
}
