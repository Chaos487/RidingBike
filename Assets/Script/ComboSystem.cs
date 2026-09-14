using System;
using UnityEngine;

/// <summary>
/// 连接"物理系统"和"分数系统"的连击桥梁:落地质量够好(Perfect/Good)、
/// 或贴身擦过障碍物(Near Miss)都会加连击；超过 comboResetTime 没有新的连击行为、
/// 或者摔车，连击数清零。Bad 落地不加分，但也不会直接打断连击，只是没有新动作而已，
/// 交给超时机制自然清零。
/// </summary>
public class ComboSystem : MonoBehaviour
{
    float comboResetTime = 4f;

    LandingDetector landingDetector;
    ObstacleSpawner obstacleSpawner;
    CrashDetector crashDetector;

    int comboCount;
    float comboTimer;

    /// <summary>连击数变化时触发(包括清零)。</summary>
    public event Action<int> OnComboChanged;

    public int ComboCount => comboCount;

    public void Initialize(LandingDetector landing, ObstacleSpawner obstacles, CrashDetector crash)
    {
        landingDetector = landing;
        obstacleSpawner = obstacles;
        crashDetector = crash;

        landingDetector.OnLanded += HandleLanded;
        obstacleSpawner.OnNearMiss += HandleNearMiss;
        crashDetector.OnCrash += HandleCrash;
    }

    public void ApplySettings(ComboSystemSettings settings)
    {
        if (settings == null) return;
        comboResetTime = settings.comboResetTime;
    }

    void Update()
    {
        if (comboCount <= 0) return;

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f)
        {
            ResetCombo();
        }
    }

    void HandleLanded(LandingDetector.Quality quality, LandingDetector.ContactOrder order)
    {
        if (quality == LandingDetector.Quality.Bad) return;
        AddCombo();
    }

    void HandleNearMiss()
    {
        AddCombo();
    }

    void HandleCrash()
    {
        ResetCombo();
    }

    void AddCombo()
    {
        comboCount++;
        comboTimer = comboResetTime;
        OnComboChanged?.Invoke(comboCount);
    }

    void ResetCombo()
    {
        if (comboCount == 0) return;
        comboCount = 0;
        OnComboChanged?.Invoke(comboCount);
    }

    void OnDestroy()
    {
        if (landingDetector != null) landingDetector.OnLanded -= HandleLanded;
        if (obstacleSpawner != null) obstacleSpawner.OnNearMiss -= HandleNearMiss;
        if (crashDetector != null) crashDetector.OnCrash -= HandleCrash;
    }
}
