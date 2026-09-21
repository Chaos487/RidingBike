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
/// 结果通过 SetGlobalTextureAfterPass 写进一个全局贴图(_ScreenBlurTexture)，任何 UI
/// Image 用的 Material(比如 Assets/Shaders/BlurredPanelBackground.shader)只要采样这张
/// 全局贴图就能拿到模糊背景——不需要跟这个 Feature 有任何直接引用关系，天然"随时在别处
/// 调用"。只有 ScreenBlurState.BlurActive 为真(至少一个弹窗开着)的时候才会真的跑这几趟
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
    }
}

class ScreenBlurPass : ScriptableRenderPass
{
    static readonly int GlobalBlurTextureId = Shader.PropertyToID("_ScreenBlurTexture");
    const int DownsamplePassIndex = 0;
    const int UpsamplePassIndex = 1;

    readonly Material material;
    readonly ProfilingSampler sampler = new ProfilingSampler("Screen Blur");

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

        // 降采样 3 趟(1/2、1/4、1/8),升采样 2 趟回到 1/4、1/2——最终结果停在半分辨率,
        // 不用完全升到满分辨率:UI 那边用 Material 采样的时候本来就会走一次双线性过滤，
        // 从半分辨率再插值放大一次，顺带又送一层免费的柔化，也省一趟升采样的开销。
        TextureHandle level1 = CreateLevel(renderGraph, 0.5f, format, "_ScreenBlurDown1");
        TextureHandle level2 = CreateLevel(renderGraph, 0.25f, format, "_ScreenBlurDown2");
        TextureHandle level3 = CreateLevel(renderGraph, 0.125f, format, "_ScreenBlurDown3");

        BlitPass(renderGraph, "Blur Downsample 1", source, level1, DownsamplePassIndex, false);
        BlitPass(renderGraph, "Blur Downsample 2", level1, level2, DownsamplePassIndex, false);
        BlitPass(renderGraph, "Blur Downsample 3", level2, level3, DownsamplePassIndex, false);

        BlitPass(renderGraph, "Blur Upsample 1", level3, level2, UpsamplePassIndex, false);
        BlitPass(renderGraph, "Blur Upsample 2", level2, level1, UpsamplePassIndex, true);
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

    // isFinal=true 的那一趟额外把结果登记成全局贴图——只有最后一趟需要,中间那几趟只是
    // 过渡贴图,没有别的系统会去采样它们。
    void BlitPass(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, int shaderPass, bool isFinal)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(name, out var passData, sampler))
        {
            passData.source = source;
            passData.material = material;
            passData.pass = shaderPass;

            builder.UseTexture(source);
            builder.SetRenderAttachment(destination, 0);
            builder.AllowPassCulling(false);
            if (isFinal) builder.SetGlobalTextureAfterPass(destination, GlobalBlurTextureId);

            builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.pass);
            });
        }
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
