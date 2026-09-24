using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundPlayOrder { Random, Sequential }

/// <summary>
/// 一个可配置的音效槽位：多个候选音频 + 挑选顺序(随机/按列表顺序循环) + 触发后延迟多少秒才播放
/// + 是否循环。Loop 打开时占住自己的 AudioSource 循环播放,直到被 Stop；关闭时按一次性音效播放
/// (PlayOneShot),同一个槽位被连续触发多次会自然叠加、不会互相打断。
/// </summary>
[Serializable]
public class SoundSlot
{
    public List<AudioClip> clips = new List<AudioClip>();
    public SoundPlayOrder playOrder = SoundPlayOrder.Random;
    [Tooltip("触发之后延迟多少秒才真正播放，0 = 立即播放。")]
    public float delay = 0f;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("勾上=循环播放(占住这个槽位自己的 AudioSource，直到被 Stop 为止)；" +
             "不勾=一次性播放一遍(可以叠加，多次触发互不打断)。")]
    public bool loop = false;

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

/// <summary>Sounds(SFX)/Music 两条音量总线——沿用 SettingsTabUI 的分类:bgm/ambient 算
/// Music,ride/landing/boost/crash 算 Sounds。</summary>
public enum SoundCategory { Music, Sounds }

/// <summary>
/// 全局音频管理器。直接挂在场景里一个独立的空物体上手动配置(不是 EndlessRunBootstrap 运行时生成的)，
/// 每个槽位对应一类游戏事件，音频列表/随机顺序播放/延迟时间/是否循环全在 Inspector 里配，不用改代码。
///
/// 其他游戏系统(BikeController/LandingDetector/CrashDetector)都是 EndlessRunBootstrap 在运行时
/// 生成的，没法在 Inspector 里互相直接拖引用，所以这里做成单例，运行时用 AudioManager.Instance
/// 拿到实例调用播放方法——具体在哪些事件上调用，接在 EndlessRunBootstrap.SetupAudio() 里。
/// </summary>
public class AudioManager : MonoBehaviour
{
    const string SoundsVolumeKey = "RidingBike_SoundsVolume";
    const string MusicVolumeKey = "RidingBike_MusicVolume";

    public static AudioManager Instance { get; private set; }

    [Header("BGM(默认循环，进场景自动播放)")]
    public SoundSlot bgm = new SoundSlot { loop = true };
    [Header("环境音(默认循环，进场景自动播放)")]
    public SoundSlot ambient = new SoundSlot { loop = true };
    [Header("骑行中(默认循环，一局开始时播放、摔车时停止)")]
    public SoundSlot ride = new SoundSlot { loop = true };
    [Header("起跳落地(默认一次性)")]
    public SoundSlot landing = new SoundSlot { loop = false };
    [Header("Shift 加速(默认一次性)")]
    public SoundSlot boost = new SoundSlot { loop = false };
    [Header("摔车(默认一次性)")]
    public SoundSlot crash = new SoundSlot { loop = false };

    AudioSource bgmSource;
    AudioSource ambientSource;
    AudioSource rideSource;
    AudioSource landingSource;
    AudioSource boostSource;
    AudioSource crashSource;

    Coroutine bgmDelayRoutine;
    Coroutine ambientDelayRoutine;
    Coroutine rideDelayRoutine;
    Coroutine landingDelayRoutine;
    Coroutine boostDelayRoutine;
    Coroutine crashDelayRoutine;

    public float SoundsVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;

    void Awake()
    {
        Instance = this;

        SoundsVolume = PlayerPrefs.GetFloat(SoundsVolumeKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);

        bgmSource = CreateSource();
        ambientSource = CreateSource();
        rideSource = CreateSource();
        landingSource = CreateSource();
        boostSource = CreateSource();
        crashSource = CreateSource();
    }

    void Start()
    {
        Play(bgm, bgmSource, ref bgmDelayRoutine, SoundCategory.Music);
        Play(ambient, ambientSource, ref ambientDelayRoutine, SoundCategory.Music);
    }

    AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    public void SetSoundsVolume(float volume)
    {
        SoundsVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SoundsVolumeKey, SoundsVolume);
        PlayerPrefs.Save();
        ApplyVolumeToLoopingSources();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.Save();
        ApplyVolumeToLoopingSources();
    }

    /// <summary>一次性音效(landing/boost/crash)下次触发时自然会用最新的音量总线重新算，
    /// 不用管；但 bgm/ambient/ride 这三个是循环槽位，播放中途拖动滑条要立刻听到变化，
    /// 得主动把已经在播的 AudioSource.volume 改掉。</summary>
    void ApplyVolumeToLoopingSources()
    {
        if (bgmSource != null && bgmSource.isPlaying) bgmSource.volume = bgm.volume * MusicVolume;
        if (ambientSource != null && ambientSource.isPlaying) ambientSource.volume = ambient.volume * MusicVolume;
        if (rideSource != null && rideSource.isPlaying) rideSource.volume = ride.volume * SoundsVolume;
    }

    public void PlayBgm() => Play(bgm, bgmSource, ref bgmDelayRoutine, SoundCategory.Music);
    public void StopBgm() => Stop(bgmSource, ref bgmDelayRoutine);
    public void PlayAmbient() => Play(ambient, ambientSource, ref ambientDelayRoutine, SoundCategory.Music);
    public void StopAmbient() => Stop(ambientSource, ref ambientDelayRoutine);

    /// <summary>开始骑行槽位——一局开始时调用。</summary>
    public void PlayRide() => Play(ride, rideSource, ref rideDelayRoutine, SoundCategory.Sounds);
    /// <summary>停止骑行槽位——摔车时调用。对一次性(非 Loop)槽位没意义，只用来打断循环。</summary>
    public void StopRide() => Stop(rideSource, ref rideDelayRoutine);

    public void PlayLanding() => Play(landing, landingSource, ref landingDelayRoutine, SoundCategory.Sounds);
    public void PlayBoost() => Play(boost, boostSource, ref boostDelayRoutine, SoundCategory.Sounds);
    public void PlayCrash() => Play(crash, crashSource, ref crashDelayRoutine, SoundCategory.Sounds);

    /// <summary>统一播放入口，Loop 与否由 slot.loop 决定，跟槽位类别无关；category 决定
    /// 这个槽位吃 Sounds 还是 Music 那条音量总线(见 SettingsTabUI)。</summary>
    void Play(SoundSlot slot, AudioSource source, ref Coroutine delayRoutine, SoundCategory category)
    {
        if (!slot.HasClips) return;

        if (delayRoutine != null)
        {
            StopCoroutine(delayRoutine);
            delayRoutine = null;
        }

        if (slot.delay > 0f)
        {
            delayRoutine = StartCoroutine(DelayedPlay(slot, source, category));
        }
        else
        {
            FirePlay(slot, source, category);
        }
    }

    IEnumerator DelayedPlay(SoundSlot slot, AudioSource source, SoundCategory category)
    {
        yield return new WaitForSeconds(slot.delay);
        FirePlay(slot, source, category);
    }

    void FirePlay(SoundSlot slot, AudioSource source, SoundCategory category)
    {
        AudioClip clip = slot.PickClip();
        if (clip == null) return;

        float scaledVolume = slot.volume * (category == SoundCategory.Music ? MusicVolume : SoundsVolume);

        if (slot.loop)
        {
            source.clip = clip;
            source.volume = scaledVolume;
            source.loop = true;
            source.Play();
        }
        else
        {
            // 一次性播放不占用 source.clip/loop 状态，同一个 AudioSource 上可以叠加多次触发。
            source.PlayOneShot(clip, scaledVolume);
        }
    }

    void Stop(AudioSource source, ref Coroutine delayRoutine)
    {
        if (delayRoutine != null)
        {
            StopCoroutine(delayRoutine);
            delayRoutine = null;
        }
        source.Stop();
    }
}
