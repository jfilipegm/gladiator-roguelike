// Gladiator/FxSlash — additive melee slash arc. Mesh: 180° ribbon, uv.x along the arc (0 = right side), uv.y radial
// (0 inner, 1 outer). _ArcFrac clips to the weapon's arc; _Progress (0..1) sweeps a bright head with a fading trail.
// Player VFX colour language: cool white / cyan (§5).
Shader "Gladiator/FxSlash"
{
    Properties
    {
        [HDR] _Color ("Head Color", Color) = (1.6, 2.2, 2.4, 1)
        [HDR] _TrailColor ("Trail Color", Color) = (0.25, 1.1, 1.6, 1)
        _Progress ("Progress", Range(0, 1)) = 0.5
        _ArcFrac ("Arc Fraction", Range(0, 1)) = 0.83
        _Trail ("Trail Length", Range(0.05, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Fx"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _TrailColor;
                half _Progress, _ArcFrac, _Trail;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V Vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float a0 = 0.5 - _ArcFrac * 0.5;
                float u = (i.uv.x - a0) / max(_ArcFrac, 1e-3);           // 0..1 inside the weapon's arc
                if (u < 0 || u > 1) discard;
                float head = saturate(_Progress * 1.35);                  // sweep reaches the end early, then fades
                float behind = head - u;
                if (behind < 0) discard;
                float trail = saturate(1 - behind / _Trail);
                float fade = saturate((1 - _Progress) * 2.2);
                float radial = smoothstep(0.0, 0.55, i.uv.y) * smoothstep(1.0, 0.82, i.uv.y);
                float edge = smoothstep(0.75, 0.97, i.uv.y);
                half3 col = lerp(_TrailColor.rgb, _Color.rgb, saturate(trail * trail + edge * 0.6));
                return half4(col * trail * radial * fade, 1);
            }
            ENDHLSL
        }
    }
}
