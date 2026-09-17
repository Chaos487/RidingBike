using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundPlayOrder { Random, Sequential }

/// <summary>
/// 一个可配置的音效槽位：多个候选音频 + 挑选顺序(随机/按列表顺序循环) + 触发后延迟多少秒才播放。
/// 循环槽位(BGM/环境音/骑行音)每次 Play 只在开始时挑一个 clip 循环到 Stop 为止；
/// 一次性槽位(落地/加速/摔车)每次触发都重新挑一个 clip 播放一遍，多次触发之间互不打断、可以叠加。
/// </summary>
[Serializable]
public class SoundSlot
{
    public List<AudioClip> clips = new List<AudioClip>();
    public SoundPlayOrder playOrder = SoundPlayOrder.Random;
    [Tooltip("触发之后延迟多少秒才真正播放，0 = 立即播放。")]
    public float delay = 0f;
    [Range(0f, 1f)] public float volume = 1f;

    int nextSequentialIndex;

    public bool HasClips => clips != null && clips.Count > 0;

    /// <summary>按配置的顺序策略挑一个 clip：Random 每次独立随机，Sequential 按列表顺序循环播放。</summary>
    public AudioClip PickClip()
    {
        if (!HasClips) return null;
        if (playOrder == SoundPlayOrder.Random)
        {
            return clips[UnityEngine.Random.Range(0, clips.Count)];
        }

        AudioClip clip = clips[nextSequentialIndex % clips.Count];
        nextSequentialIndex = (nextSequentialIndex + 1) % clips.Count;
        return clip;
    }
}

/// <summary>
/// 全局音频管理器。直接挂在场景里一个独立的空物体上手动配置(不是 EndlessRunBootstrap 运行时生成的)，
/// 每个槽位对应一类游戏事件，音频列表/随机顺序播放/延迟时间全在 Inspector 里配，不用改代码。
///
/// 其他游戏系统(BikeController/LandingDetector/CrashDetector)都是 EndlessRunBootstrap 在运行时
/// 生成的，没法在 Inspector 里互相直接拖引用，所以这里做成单例，运行时用 AudioManager.Instance
/// 拿到实例调用播放方法——具体在哪些事件上调用，接在 EndlessRunBootstrap.SetupAudio() 里。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM(循环，进场景自动播放)")]
    public SoundSlot bgm;
    [Header("环境音(循环，进场景自动播放)")]
    public SoundSlot ambient;
    [Header("骑行中(循环，一局开始时播放、摔车时停止)")]
    public SoundSlot ride;
    [Header("起跳落地(一次性)")]
    public SoundSlot landing;
    [Header("Shift 加速(一次性)")]
    public SoundSlot boost;
    [Header("摔车(一次性)")]
    public SoundSlot crash;

    AudioSource bgmSource;
    AudioSource ambientSource;
    AudioSource rideSource;
    AudioSource oneShotSource;

    Coroutine bgmDelayRoutine;
    Coroutine ambientDelayRoutine;
    Coroutine rideDelayRoutine;

    void Awake()
    {
        Instance = this;

        bgmSource = CreateLoopingSource();
        ambientSource = CreateLoopingSource();
        rideSource = CreateLoopingSource();

        // 一次性音效共用一个 AudioSource，靠 PlayOneShot 天然支持叠加播放(比如落地音还没放完
        // 又摔车了)，不需要为每次触发单独开一个 AudioSource。
        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
    }

    void Start()
    {
        PlayLoop(bgm, bgmSource, ref bgmDelayRoutine);
        PlayLoop(ambient, ambientSource, ref ambientDelayRoutine);
    }

    AudioSource CreateLoopingSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        return source;
    }

    /// <summary>开始骑行循环音效——一局开始时调用。</summary>
    public void PlayRide() => PlayLoop(ride, rideSource, ref rideDelayRoutine);

    /// <summary>停止骑行循环音效——摔车时调用。</summary>
    public void StopRide()
    {
        if (rideDelayRoutine != null)
        {
            StopCoroutine(rideDelayRoutine);
            rideDelayRoutine = null;
        }
        rideSource.Stop();
    }

    public void PlayLanding() => PlayOneShot(landing);
    public void PlayBoost() => PlayOneShot(boost);
    public void PlayCrash() => PlayOneShot(crash);

    void PlayLoop(SoundSlot slot, AudioSource source, ref Coroutine delayRoutine)
    {
        if (!slot.HasClips) return;

        if (delayRoutine != null) StopCoroutine(delayRoutine);

        if (slot.delay > 0f)
        {
            delayRoutine = StartCoroutine(DelayedLoop(slot, source));
        }
        else
        {
            StartLoop(slot, source);
        }
    }

    IEnumerator DelayedLoop(SoundSlot slot, AudioSource source)
    {
        yield return new WaitForSeconds(slot.delay);
        StartLoop(slot, source);
    }

    void StartLoop(SoundSlot slot, AudioSource source)
    {
        AudioClip clip = slot.PickClip();
        if (clip == null) return;
        source.clip = clip;
        source.volume = slot.volume;
        source.Play();
    }

    void PlayOneShot(SoundSlot slot)
    {
        if (!slot.HasClips) return;

        if (slot.delay > 0f) StartCoroutine(DelayedOneShot(slot));
        else FireOneShot(slot);
    }

    IEnumerator DelayedOneShot(SoundSlot slot)
    {
        yield return new WaitForSeconds(slot.delay);
        FireOneShot(slot);
    }

    void FireOneShot(SoundSlot slot)
    {
        AudioClip clip = slot.PickClip();
        if (clip == null) return;
        oneShotSource.PlayOneShot(clip, slot.volume);
    }
}
