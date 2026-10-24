// Gladiator/FxGhost — additive after-images of a character's parts (the Thunder Champion's blink, note 35), drawn
// instanced by BlinkGhosts. No texture: a dim core and a bright fresnel rim per flat face, so the low-poly silhouette
// reads as a cyan-white energy shell. Per-instance colour via the instancing buffer (rgb colour, a strength 0..1).
// Player VFX colour language: cool white / cyan (§5).
Shader "Gladiator/FxGhost"
{
    Properties
    {
        _Rim ("Rim Power", Range(0.5, 6)) = 2
        _Core ("Core Brightness", Range(0, 1)) = 0.3
        _Intensity ("Emission Boost", Range(0.5, 4)) = 1.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Rim, _Core, _Intensity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GColor) // rgb colour, a strength (0 = gone)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V Vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetCameraPositionWS() - ws;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _GColor);
                float3 n = normalize(i.normalWS);
                float3 vd = normalize(i.viewWS);
                float rim = pow(1 - saturate(abs(dot(n, vd))), _Rim);
                half k = (_Core + rim * (1 - _Core)) * _Intensity * saturate(c.a);
                return half4(c.rgb * k, 1);
            }
            ENDHLSL
        }
    }
}
