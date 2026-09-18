/// <summary>
/// 把 Choice 的 Effects 应用到实际游戏状态——第一版收窄成白名单,直接改
/// BikeController/BikeDamageSystem 的现有数值字段,不做通用效果引擎(见 NodeSettings.EffectType)。
/// 纯静态方法,不持有状态,新增 EffectType 只需要在这里加一个 case,不影响 Choice/Pool/UI 那几层。
/// </summary>
public static class NodeEffectSystem
{
    public static void ApplyChoice(ChoicePreset choice, BikeController bike, BikeDamageSystem damageSystem)
    {
        foreach (EffectEntry effect in choice.effects)
        {
            ApplyEffect(effect, bike, damageSystem);
        }
    }

    /// <summary>预判"选了这个 Choice 会不会把玩家直接扣死"——只加总它带的 MaxHpFlat 效果,
    /// 不实际改动任何状态。给 NodeChoiceUI 用来把这种选项的卡片标红提醒玩家,但依然可选。</summary>
    public static bool WouldBeLethal(ChoicePreset choice, BikeDamageSystem damageSystem)
    {
        float hpDelta = 0f;
        foreach (EffectEntry effect in choice.effects)
        {
            if (effect.type == EffectType.MaxHpFlat) hpDelta += effect.value;
        }

        return damageSystem.CurrentHp + hpDelta <= 0f;
    }

    static void ApplyEffect(EffectEntry effect, BikeController bike, BikeDamageSystem damageSystem)
    {
        switch (effect.type)
        {
            case EffectType.MaxSpeedPercent:
                bike.maxSpeedKmh *= 1f + effect.value / 100f;
                break;
            case EffectType.JumpForceFlat:
                bike.jumpForce += effect.value;
                break;
            case EffectType.MaxHpFlat:
                damageSystem.ModifyMaxHp(effect.value);
                break;
            case EffectType.AccelerationPercent:
                bike.cruiseMotorAcceleration *= 1f + effect.value / 100f;
                bike.cruiseTorque *= 1f + effect.value / 100f;
                break;
            case EffectType.BoostRechargePercent:
                bike.boostRechargeDistance = UnityEngine.Mathf.Max(20f, bike.boostRechargeDistance * (1f - effect.value / 100f));
                break;
        }
    }
}
