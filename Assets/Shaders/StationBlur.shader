// Station 暂停时的背景模糊——手写 ShaderLab/HLSL(不是 Shader Graph),单个 3x3 tent 核,
// 由 StationBlurFeature 用经典的 CommandBuffer.Blit 反复跑这同一个 Pass 好几次(每次采样
// 间距递增,Kawase 风格)叠出更强的模糊,比一次性用超大核便宜。
//
// 故意只依赖 _MainTex 这套从 Unity 最早期就有、经典 CommandBuffer.Blit(src, dst, material)
// 会自动帮你设好的纹理槽位和 UnityCG/Core 里最基础的宏,不碰 URP 较新版本才有的 Blit.hlsl/
// Blitter API——那套 API 在不同 URP 版本之间改动比较大,这里选兼容性最稳的写法。
// 9 次采样手动展开写,不用数组下标 + 循环，部分 GLES/WebGL 编译器对循环里动态下标数组
// 支持不好，展开写兼容性最稳。
Shader "Custom/StationBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 20)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "StationBlurPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // Unity 对经典 Blit 的 _MainTex 会自动提供这个,.xy = 1/宽高
            float _BlurSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // 经典 CommandBuffer.Blit 画的是一个顶点已经在裁剪空间 -1..1 范围内的全屏四边形,
            // 这里直接透传,不用 TransformObjectToHClip 之类需要 MVP 矩阵的变换。
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 texel = _MainTex_TexelSize.xy * _BlurSize;

                half4 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texel.x, -texel.y)) * 0.0625h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, -texel.y)) * 0.125h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texel.x, -texel.y)) * 0.0625h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texel.x, 0.0)) * 0.125h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * 0.25h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texel.x, 0.0)) * 0.125h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texel.x, texel.y)) * 0.0625h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, texel.y)) * 0.125h;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texel.x, texel.y)) * 0.0625h;

                return sum;
            }
            ENDHLSL
        }
    }
}
