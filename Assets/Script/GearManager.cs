using System;
using UnityEngine;

/// <summary>
/// 齿轮(游戏内货币)的持久化管理:开局从 PlayerPrefs 读取已有数量,每次拾取立刻增加并存盘——
/// 货币比"最远距离"这种纯记录更经不起丢,不等到摔车结算才存,每次拾取都存一次。
/// 现在只有"加"没有"花"：花的机制留给以后的 Roguelike 局外商店(GitHub #3 存档设计提过的方向)。
/// </summary>
public class GearManager : MonoBehaviour
{
    const string GearCountKey = "RidingBike_GearCount";

    public int CurrentGearCount { get; private set; }

    /// <summary>齿轮数量变化时触发(包括开局读档那一次),参数是变化后的总数。供 UI 更新显示。</summary>
    public event Action<int> OnGearCountChanged;

    public void Initialize()
    {
        CurrentGearCount = PlayerPrefs.GetInt(GearCountKey, 0);
        OnGearCountChanged?.Invoke(CurrentGearCount);
    }

    public void AddGear(int amount = 1)
    {
        if (amount <= 0) return;

        CurrentGearCount += amount;
        PlayerPrefs.SetInt(GearCountKey, CurrentGearCount);
        PlayerPrefs.Save();

        OnGearCountChanged?.Invoke(CurrentGearCount);
    }
}
