using DG.Tweening;
using UnityEngine;

/// <summary>
/// 速度反应式镜头的可调参数,做成资产方便在编辑器里手调。
/// 用法:Project 窗口右键 Create > RidingBike > Camera Director Settings 创建一份资产,
/// 放在 Assets 下任意位置都行,启动时会自动被找到并套用;不创建的话就用脚本里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "CameraDirectorSettings", menuName = "RidingBike/Camera Director Settings")]
public class CameraDirectorSettings : ScriptableObject
{
    [Header("Zoom 范围 (Orthographic Size,越小越放大)")]
    [Tooltip("最大限度放大时的尺寸(低速/静止时的基准，比这更小的只有摔车)。")]
    public float minOrthoSize = 2.4f;
    [Tooltip("满速/加速键按住时拉到最远的尺寸。")]
    public float maxOrthoSize = 4.2f;
    [Tooltip("摔车瞬间聚焦用的尺寸，通常比 minOrthoSize 更小，突出冲击感。")]
    public float crashOrthoSize = 1.8f;
    [Tooltip("空中额外再拉远多少(叠加在速度对应的目标值上)。")]
    public float airborneZoomOutBonus = 0.6f;

    [Header("Zoom 缓动 - 缩小/聚焦(减速、松开加速键)")]
    [Tooltip("镜头变小(放大画面)时的缓动时长，应该短促。")]
    public float zoomInDuration = 0.18f;
    public Ease zoomInEase = Ease.OutQuad;

    [Header("Zoom 缓动 - 放大/拉远(加速、按住 Shift)")]
    [Tooltip("镜头变大(拉远画面)时的缓动时长，应该丝滑。")]
    public float zoomOutDuration = 1.1f;
    public Ease zoomOutEase = Ease.OutSine;

    [Header("摔车冲击")]
    public float crashZoomDuration = 0.12f;
    public Ease crashZoomEase = Ease.OutBack;
    [Tooltip("摔车瞬间叠加的镜头抖动强度(传给 CinemachineImpulseSource.GenerateImpulse 的 force)。建议偏小，太猛会让玩家看不清发生了什么，具体的抖动时长/频率在 Impulse Source 组件自己的默认曲线上调。")]
    public float crashShakeAmplitude = 0.6f;
    [Tooltip("部分损毁(掉零件但没结束这一局)时的镜头抖动强度，建议明显小于 crashShakeAmplitude，突出\"这次比真摔车轻\"。")]
    public float partialDamageShakeAmplitude = 0.25f;

    [Header("落地回弹")]
    [Tooltip("非摔车的正常落地，给一个很小的镜头回弹表示冲击。")]
    public float landingPunchAmount = 0.12f;
    public float landingPunchInDuration = 0.08f;
    public float landingPunchOutDuration = 0.22f;
    [Tooltip("Perfect 落地的回弹幅度倍率，建议远小于 1，突出\"稳\"。")]
    public float perfectLandingPunchScale = 0.2f;
    [Tooltip("Good 落地的回弹幅度倍率。")]
    public float goodLandingPunchScale = 1f;
    [Tooltip("Not Bad 落地(但还没到摔车)的回弹幅度倍率，建议大于 1。")]
    public float notBadLandingPunchScale = 1.8f;

    [Header("前瞻偏移 (Look-ahead)")]
    [Tooltip("满速时镜头目标点相对车身往前偏移多少米。")]
    public float maxLookaheadOffset = 1.6f;
    public float lookaheadDuration = 0.6f;
    public Ease lookaheadEase = Ease.OutSine;

    [Header("更新节流")]
    [Tooltip("速度/状态变化多大才重新设定一次缓动目标，避免每帧都重启缓动导致画面发抖。")]
    public float retargetThreshold = 0.03f;

    [Header("开场引入 (tap to start 时车藏在画面外，点击后滑入)")]
    [Tooltip("tap to start 画面时镜头目标点相对车身往前偏移多少米，需要大于半屏宽(约 orthoSize×宽高比)才能真的把车推出画面，默认值按 minOrthoSize≈2.4、16:9 估算留了余量。")]
    public float introOffsetX = 7f;
    [Tooltip("点击 tap to start 之后，镜头从引入偏移量缓动回 0(车滑入画面)的时长。")]
    public float introRevealDuration = 1.1f;
    public Ease introRevealEase = Ease.OutSine;
}
