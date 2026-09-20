using UnityEngine;

/// <summary>
/// 挂在生成出来的每个齿轮实例上:用单张图的 cos 缩放挤压做假 3D 旋转(经典 2D 游戏金币旋转手法,
/// 不需要 sprite sheet)——scale.x 按 cos(time) 变化,1 → 0(侧面)→ -1(镜像,相当于转到背面)
/// → 0 → 1 循环,转到背面时顺带把颜色调暗一点模拟光照角度变化。车身(轮子或车架,凡是挂了
/// WheelContactSensor 的碰撞体,跟 NearMissDetector 识别车身是同一套方式)碰到触发区就算拾取,
/// 通知 GearManager 加钱、销毁自己。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GearPickup : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    GearManager gearManager;

    float spinSpeed = 3f;
    bool darkenBackFace = true;
    float backFaceBrightness = 0.6f;
    float bobAmplitude = 0.15f;
    float bobSpeed = 2f;

    float baseY;
    float baseScaleX;
    float spinPhase;
    Color normalColor;
    Color darkColor;
    bool collected;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseY = transform.position.y;
        baseScaleX = Mathf.Abs(transform.localScale.x);
        if (baseScaleX <= 0f) baseScaleX = 1f;

        // 随机相位,避免同一时刻生成的多个齿轮转/浮动完全同步，显得死板。
        spinPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void Initialize(GearManager manager, float spin, bool darkenBack, float backBrightness, float bob, float bobSpd)
    {
        gearManager = manager;
        spinSpeed = spin;
        darkenBackFace = darkenBack;
        backFaceBrightness = backBrightness;
        bobAmplitude = bob;
        bobSpeed = bobSpd;

        normalColor = spriteRenderer.color;
        darkColor = normalColor * backFaceBrightness;
        darkColor.a = normalColor.a;
    }

    void Update()
    {
        float t = Time.time * spinSpeed + spinPhase;
        float scaleX = Mathf.Cos(t);

        Vector3 scale = transform.localScale;
        scale.x = baseScaleX * scaleX;
        transform.localScale = scale;

        if (darkenBackFace)
        {
            spriteRenderer.color = scaleX < 0f ? darkColor : normalColor;
        }

        if (bobAmplitude > 0f)
        {
            Vector3 pos = transform.position;
            pos.y = baseY + Mathf.Sin(Time.time * bobSpeed + spinPhase) * bobAmplitude;
            transform.position = pos;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (other.GetComponent<WheelContactSensor>() == null) return;

        collected = true;
        gearManager?.AddGear(1);
        Destroy(gameObject);
    }
}
