using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Station 暂停时给整个画面加一层真实的屏幕空间模糊,替换掉之前 NodePanel 上那层半透明遮罩。
/// UI(EndlessRunCanvas)是 Screen Space - Overlay,不吃 URP 的渲染管线,只会模糊到摄像机
/// 渲染出来的游戏世界画面,UI 本身依然清晰——不需要额外处理 UI 那一层。
///
/// 用经典的 CommandBuffer.GetTemporaryRT + Blit 写(不是 URP 较新版本的 Blitter/RTHandle API),
/// 换 3x3 tent 模糊核反复 Blit 几次(Kawase 风格,每次采样间距递增)换出比单次大核模糊更柔和、
/// 更便宜的效果——这套写法从 URP 早期版本一直到现在的 Compatibility Mode 都稳定可用。
///
/// 接线方式:Unity 编辑器里打开当前用的 Universal Renderer Data 资产 → Add Renderer Feature →
/// 选 Station Blur——不需要额外拖引用,Shader 通过 Reset()/Create() 里的 Shader.Find 自动找到,
/// 也可以在 Inspector 里手动把 Assets/Shaders/StationBlur.shader 拖进 Blur Shader 那个槽位。
/// 默认关闭,只有 NodeManager 在 Station 暂停期间会调 StationBlurFeature.Instance.SetActive(true)。
/// </summary>
public class StationBlurFeature : ScriptableRendererFeature
{
    [Tooltip("模糊用的 Shader，留空的话 Create() 会自动用 Shader.Find(\"Custom/StationBlur\") 找一次。")]
    public Shader blurShader;
    [Tooltip("模糊强度基准，数值越大每次采样的像素间距越大、糊得越狠。")]
    public float blurSize = 2f;
    [Tooltip("反复 Blit 的次数，越多越糊、开销也越高，3~4 次通常够用。")]
    [Range(1, 6)]
    public int iterations = 3;

    /// <summary>URP 加载这份 Renderer Data 时会自动调用 Create() 生成实例——运行时其他脚本
    /// 通过这个静态引用拿到它、调 SetActive() 开关模糊，不需要在场景里手动挂引用。</summary>
    public static StationBlurFeature Instance { get; private set; }

    Material blurMaterial;
    StationBlurPass blurPass;

    void Reset()
    {
        blurShader = Shader.Find("Custom/StationBlur");
    }

    public override void Create()
    {
        Instance = this;

        if (blurShader == null) blurShader = Shader.Find("Custom/StationBlur");
        if (blurShader == null)
        {
            Debug.LogError("StationBlurFeature: 找不到 Custom/StationBlur shader，模糊不会生效。");
            return;
        }

        blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
        blurPass = new StationBlurPass(blurMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
        };

        SetActive(false); // 默认关闭，只在 Station 暂停时由 NodeManager 打开
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (blurMaterial == null || blurPass == null) return;

        blurPass.Setup(blurSize, iterations);
        renderer.EnqueuePass(blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(blurMaterial);
        if (Instance == this) Instance = null;
    }

    class StationBlurPass : ScriptableRenderPass
    {
        static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
        static readonly int TempId1 = Shader.PropertyToID("_StationBlurTemp1");
        static readonly int TempId2 = Shader.PropertyToID("_StationBlurTemp2");

        readonly Material material;
        float blurSize;
        int iterations;

        public StationBlurPass(Material blurMaterial)
        {
            material = blurMaterial;
        }

        public void Setup(float size, int iterationCount)
        {
            blurSize = size;
            iterations = Mathf.Max(1, iterationCount);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("StationBlur");

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            cmd.GetTemporaryRT(TempId1, desc);
            cmd.GetTemporaryRT(TempId2, desc);

            RenderTargetIdentifier cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            cmd.Blit(cameraTarget, TempId1);

            int src = TempId1;
            int dst = TempId2;

            for (int i = 0; i < iterations; i++)
            {
                material.SetFloat(BlurSizeId, blurSize * (i + 1));
                cmd.Blit(src, dst, material);
                (src, dst) = (dst, src);
            }

            cmd.Blit(src, cameraTarget);

            cmd.ReleaseTemporaryRT(TempId1);
            cmd.ReleaseTemporaryRT(TempId2);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
