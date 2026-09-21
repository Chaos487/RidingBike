using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// 全屏模糊,给 NodePanel/MenuPanel/PausePanel 这类弹窗背景用。真正的多趟
/// 降采样→模糊→升采样(Dual Kawase Blur,见 Assets/Shaders/ScreenBlur.shader),
/// 不是之前那版"单 pass 里采样几个点取平均"——那种放大半径只会让画面"变灰变浑浊"，
/// 到不了真正柔和的模糊，这次换成真正的多趟渲染。
///
/// 用 Unity 6 Render Graph 的新 API(RecordRenderGraph)写，不是旧版 Compatibility Mode
/// 专用的 Execute()——这个项目之前 StationBlurFeature 那次就是因为用了旧 API，在这个项目
/// 实际跑的 Render Graph 管线下完全不生效，这次直接按新 API 写。
///
/// 模糊结果写进一个自己手动分配、跨帧持续存在的 RenderTexture(不是 Render Graph 内部那种
/// 每帧临时/池化的资源)，再用最朴素的 Shader.SetGlobalTexture 注册成全局贴图
/// (_ScreenBlurTexture)。一开始用的是 Render Graph 自己的 SetGlobalTextureAfterPass，
/// 实测 UI Canvas(走的是完全独立于这个 Render Graph 执行的 Canvas 渲染路径)看不到那个
/// 全局绑定——换成这种"手动持有 RenderTexture + 最原始的全局绑定 API"之后，任何地方
/// (包括 UI 的 CanvasRenderer 绘制)都能采样到，不依赖 Render Graph 的作用域。
///
/// 只有 ScreenBlurState.BlurActive 为真(至少一个弹窗开着)的时候才会真的跑这几趟
/// 降采样/升采样，平时(没有任何弹窗)直接跳过整个 Pass，不吃额外性能。
/// </summary>
public class ScreenBlurFeature : ScriptableRendererFeature
{
    public Shader blurShader;

    Material blurMaterial;
    ScreenBlurPass pass;

    public override void Create()
    {
        if (blurShader == null) blurShader = Shader.Find("Hidden/RidingBike/ScreenBlur");
        if (blurShader == null) return;

        blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
        pass = new ScreenBlurPass(blurMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || !ScreenBlurState.BlurActive) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(blurMaterial);
        pass?.Dispose();
    }
}

class ScreenBlurPass : ScriptableRenderPass
{
    static readonly int GlobalBlurTextureId = Shader.PropertyToID("_ScreenBlurTexture");
    const int DownsamplePassIndex = 0;
    const int UpsamplePassIndex = 1;

    readonly Material material;
    readonly ProfilingSampler sampler = new ProfilingSampler("Screen Blur");

    // 手动持有、跨帧不重建的最终结果贴图——见类顶部注释,这个必须是真正的 RenderTexture
    // (不是 Render Graph 内部池化的那种),UI 才能稳定采样到。
    RTHandle finalTexture;
    int finalWidth = -1;
    int finalHeight = -1;

    public ScreenBlurPass(Material material)
    {
        this.material = material;
    }

    class BlitPassData
    {
        public TextureHandle source;
        public Material material;
        public int pass;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (material == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle source = resourceData.activeColorTexture;
        if (!source.IsValid()) return;

        GraphicsFormat format = cameraData.cameraTargetDescriptor.graphicsFormat;
        int screenWidth = cameraData.cameraTargetDescriptor.width;
        int screenHeight = cameraData.cameraTargetDescriptor.height;

        EnsureFinalTexture(screenWidth / 2, screenHeight / 2, format);
        TextureHandle finalHandle = renderGraph.ImportTexture(finalTexture);

        // 降采样 3 趟(1/2、1/4、1/8),升采样 2 趟回到 1/4、1/2——最后一趟直接写进上面那张
        // 手动持有的 finalTexture,不用 Render Graph 内部再临时分配一张。
        TextureHandle level1 = CreateLevel(renderGraph, 0.5f, format, "_ScreenBlurDown1");
        TextureHandle level2 = CreateLevel(renderGraph, 0.25f, format, "_ScreenBlurDown2");
        TextureHandle level3 = CreateLevel(renderGraph, 0.125f, format, "_ScreenBlurDown3");

        BlitPass(renderGraph, "Blur Downsample 1", source, level1, DownsamplePassIndex);
        BlitPass(renderGraph, "Blur Downsample 2", level1, level2, DownsamplePassIndex);
        BlitPass(renderGraph, "Blur Downsample 3", level2, level3, DownsamplePassIndex);

        BlitPass(renderGraph, "Blur Upsample 1", level3, level2, UpsamplePassIndex);
        BlitPass(renderGraph, "Blur Upsample 2", level2, finalHandle, UpsamplePassIndex);

        Shader.SetGlobalTexture(GlobalBlurTextureId, finalTexture);
    }

    void EnsureFinalTexture(int width, int height, GraphicsFormat format)
    {
        width = Mathf.Max(width, 4);
        height = Mathf.Max(height, 4);
        if (finalTexture != null && width == finalWidth && height == finalHeight) return;

        finalTexture?.Release();
        finalTexture = RTHandles.Alloc(
            width, height,
            colorFormat: format,
            filterMode: FilterMode.Bilinear,
            wrapMode: TextureWrapMode.Clamp,
            name: "_ScreenBlurFinal");
        finalWidth = width;
        finalHeight = height;
    }

    TextureHandle CreateLevel(RenderGraph renderGraph, float scale, GraphicsFormat format, string name)
    {
        TextureDesc desc = new TextureDesc(new Vector2(scale, scale))
        {
            format = format,
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            clearBuffer = false,
        };
        return renderGraph.CreateTexture(desc);
    }

    void BlitPass(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, int shaderPass)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(name, out var passData, sampler))
        {
            passData.source = source;
            passData.material = material;
            passData.pass = shaderPass;

            builder.UseTexture(source);
            builder.SetRenderAttachment(destination, 0);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.pass);
            });
        }
    }

    public void Dispose()
    {
        finalTexture?.Release();
        finalTexture = null;
    }
}

/// <summary>
/// 骑行中的各个弹窗(Station 三选一/开始画面 Menu/暂停面板)开关的时候各自喊一声,
/// 用计数而不是单个 bool——Station 和暂停面板现在允许同时开着(见 RunManager.TogglePause
/// 那次改动),用 bool 的话两个一起开、关掉一个会把另一个也带没了模糊。
/// </summary>
public static class ScreenBlurState
{
    static int activeCount;

    public static bool BlurActive => activeCount > 0;

    public static void BeginBlur()
    {
        activeCount++;
    }

    public static void EndBlur()
    {
        activeCount = Mathf.Max(0, activeCount - 1);
    }
}
