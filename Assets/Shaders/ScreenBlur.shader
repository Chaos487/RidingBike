// 全屏模糊用的降采样/升采样 Pass——Dual Kawase Blur,很多手游做"毛玻璃"背景用的就是这套手法:
// 便宜(每趟只采样 5/8 个点)、效果比单趟多点取平均柔和得多(靠反复"缩小再放大"实现,
// 不是靠加大单次采样的偏移量)。
//
// 这个 Shader 本身不认识 Render Graph/相机——只是两个"输入一张图、输出一张图"的 Pass,
// 真正决定"降采样几次、每次尺寸多大、结果贴哪"的逻辑在 ScreenBlurFeature.cs 里。
Shader "Hidden/RidingBike/ScreenBlur"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        // 用 Core RP Library 现成的全屏三角形手法,不用自己传网格。
        Varyings Vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
            OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);
            return OUT;
        }
        ENDHLSL

        // Pass 0: 降采样——目标分辨率只有输入的一半,采样点故意用半像素的对角偏移
        // (不是直接在同一个点采样),这样降采样本身就顺带把高频细节磨掉了。
        Pass
        {
            Name "Downsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 halfPixel = _MainTex_TexelSize.xy * 0.5;
                float2 uv = IN.uv;

                half4 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * 4.0;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - halfPixel);
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + halfPixel);
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(halfPixel.x, -halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(halfPixel.x, -halfPixel.y));
                return sum / 8.0;
            }
            ENDHLSL
        }

        // Pass 1: 升采样——目标分辨率是输入的两倍,8 个点的"米字形"加权采样,
        // 跟降采样搭配起来才是完整的 Dual Kawase,单独一个方向效果不对。
        Pass
        {
            Name "Upsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 halfPixel = _MainTex_TexelSize.xy * 0.5;
                float2 uv = IN.uv;

                half4 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-halfPixel.x * 2.0, 0.0));
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-halfPixel.x, halfPixel.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, halfPixel.y * 2.0));
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(halfPixel.x, halfPixel.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(halfPixel.x * 2.0, 0.0));
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(halfPixel.x, -halfPixel.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, -halfPixel.y * 2.0));
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-halfPixel.x, -halfPixel.y)) * 2.0;
                return sum / 12.0;
            }
            ENDHLSL
        }
    }
}
