Shader "CoreGem/DistantMountainBackdrop"
{
    Properties
    {
        _BaseMap ("Mountain Image", 2D) = "white" {}
        _Tint ("Warm Earth Tint", Color) = (1, 0.86, 0.72, 1)
        _FadeStart ("Ground Fade Start", Range(0, 1)) = 0.03
        _FadeEnd ("Ground Fade End", Range(0, 1)) = 0.37
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _Tint;
            float _FadeStart;
            float _FadeEnd;

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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                color.rgb *= _Tint.rgb;
                color.a *= smoothstep(_FadeStart, _FadeEnd, input.uv.y);
                return color;
            }
            ENDHLSL
        }
    }
}
