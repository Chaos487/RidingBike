using UnityEngine;

/// <summary>
/// 尾气/扬尘粒子效果——挂在车身根节点(Bike)上,实例化 Assets/Resources/ExhaustTrail.prefab。
/// 粒子本身的形状/颜色/大小/生命周期这些视觉参数完全由美术在那份预制体的 ParticleSystem
/// 组件上调,这个脚本只管"什么时候喷、喷多猛"——通过 EmissionModule.enabled/
/// rateOverTimeMultiplier 控制,不直接碰粒子系统的其它任何字段。
///
/// 预制体放在 Assets/Resources/ 而不是 Assets/prefab/ 是因为 EndlessRunBootstrap.FindPrefab
/// 的 Editor 内搜索(AssetDatabase)在设备构建里会被编译掉，只有 Resources.Load 兜底分支能在
/// 真机上生效——这个项目之前踩过一次坑(iOS 空白屏，根因就是资产没放在 Resources 下),
/// 所以现在新加的资产统一走 Resources 这条路，Editor 里也一样能用。
///
/// 找不到预制体就直接不生成，不影响其它系统——纯装饰，没有这个效果游戏照常玩。
/// </summary>
public class BikeExhaust : MonoBehaviour
{
    float minSpeedKmhForEmission = 3f;
    float minEmissionMultiplier = 0.3f;
    float maxEmissionMultiplier = 1.5f;
    float boostEmissionMultiplier = 2.2f;
    Vector3 localOffset = new Vector3(-0.5f, 0.05f, 0f);

    BikeController bike;
    ParticleSystem particles;
    ParticleSystem.EmissionModule emission;

    public void ApplySettings(BikeExhaustSettings settings)
    {
        if (settings == null) return;

        minSpeedKmhForEmission = settings.minSpeedKmhForEmission;
        minEmissionMultiplier = settings.minEmissionMultiplier;
        maxEmissionMultiplier = settings.maxEmissionMultiplier;
        boostEmissionMultiplier = settings.boostEmissionMultiplier;
        localOffset = settings.localOffset;
    }

    public void Initialize(BikeController bikeController, GameObject exhaustPrefab)
    {
        bike = bikeController;
        if (exhaustPrefab == null) return;

        GameObject instance = Instantiate(exhaustPrefab, bike.transform);
        instance.transform.localPosition = localOffset;
        instance.transform.localRotation = Quaternion.identity;

        particles = instance.GetComponent<ParticleSystem>();
        if (particles == null) particles = instance.GetComponentInChildren<ParticleSystem>();
        if (particles != null) emission = particles.emission;
    }

    void Update()
    {
        if (bike == null || particles == null) return;

        float speedMs = Mathf.Abs(bike.bikeRigidbody.linearVelocity.x);
        bool shouldEmit = bike.IsWheelGrounded && speedMs * 3.6f > minSpeedKmhForEmission;
        emission.enabled = shouldEmit;

        if (!shouldEmit) return;

        float speedRatio = Mathf.Clamp01(speedMs / Mathf.Max(bike.MaxLinearSpeed, 0.01f));
        emission.rateOverTimeMultiplier = bike.IsBoosting
            ? boostEmissionMultiplier
            : Mathf.Lerp(minEmissionMultiplier, maxEmissionMultiplier, speedRatio);
    }
}
