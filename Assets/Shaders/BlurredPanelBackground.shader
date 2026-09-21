// 弹窗背景(NodePanel/MenuPanel/PausePanel)用——只做两件事:按当前屏幕位置采样
// ScreenBlurFeature 算好的那张全局模糊贴图(_ScreenBlurTexture),再乘一层 Tint Color。
// 真正"怎么模糊"完全不归这个 Shader 管,这里只负责"贴到 UI 上、上个颜色"。
//
// 手写 HLSL 而不是 Shader Graph——这几行逻辑足够简单,手写更可控;之前那份复杂的
// BackgroundBlur.shadergraph(5 点采样自己做模糊)已经不再使用,保留着但没接在任何材质上了。
Shader "RidingBike/BlurredPanelBackground"
{
    Properties
    {
        _TintColor("Tint Color", Color) = (0.12, 0.08, 0.10, 0.85)
        // UI Image/CanvasRenderer 要求任何挂在它上面的 Shader 必须有 _MainTex 这个贴图属性
        // (哪怕用不上),不然 Unity UI 批处理的时候会报错——纯占位,图里不采样它。
        [HideInInspector] _MainTex("Main Tex (unused, UI requires it)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ScreenBlurTexture);
            SAMPLER(sampler_ScreenBlurTexture);
            half4 _TintColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positionInputs.positionCS;
                OUT.screenPos = positionInputs.positionNDC;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;
                half3 blurred = SAMPLE_TEXTURE2D(_ScreenBlurTexture, sampler_ScreenBlurTexture, uv).rgb;
                return half4(blurred * _TintColor.rgb, _TintColor.a);
            }
            ENDHLSL
        }
    }
}
