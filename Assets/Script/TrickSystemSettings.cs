using System;
using UnityEngine;

/// <summary>
/// 特技计分的可调参数，做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Trick System Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "TrickSystemSettings", menuName = "RidingBike/Trick System Settings")]
public class TrickSystemSettings : ScriptableObject
{
    [Serializable]
    public struct Tier
    {
        [Tooltip("本次滞空累计旋转角度达到这个值(度)才算这一档。")]
        public float minDegrees;
        public int score;
    }

    [Tooltip("按累计旋转角度分档给分，取满足条件里最高的一档；转的角度比最小一档还少就不计分。")]
    public Tier[] tiers = new[]
    {
        new Tier { minDegrees = 90f, score = 50 },
        new Tier { minDegrees = 180f, score = 100 },
        new Tier { minDegrees = 360f, score = 250 },
        new Tier { minDegrees = 540f, score = 500 },
        new Tier { minDegrees = 720f, score = 1000 },
    };
}
