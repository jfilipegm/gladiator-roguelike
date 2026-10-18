// Gladiator/BlobShadow — soft dark disc under enemies, drawn instanced (BlobShadowRenderer). Procedural, no texture.
Shader "Gladiator/BlobShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0.08, 0.06, 0.1, 1)
        _Strength ("Strength", Range(0, 1)) = 0.55
        _Softness ("Softness", Range(0.05, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -1

        Pass
        {
            Name "Blob"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength, _Softness;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            V Vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float d = length(i.uv * 2 - 1);
                float a = (1 - smoothstep(1 - _Softness, 1, d)) * _Strength;
                half3 c = MixFog(_Color.rgb, i.fog);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
