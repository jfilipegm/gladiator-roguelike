// Gladiator/Telegraph — ground decals drawn instanced by TelegraphRenderer (procedural, no texture):
//   style 0 = attack telegraph (orange-red ring + filling disc + bright fill front), style 1 = fire patch (flickering),
//   style 2 = thin pulsing ring (elite affix / fuse), style 3 = straight lane (charge / dash / aim line: rim along the
//   sides, fills from the start toward the far end). Per-instance colour + params via the instancing buffer.
Shader "Gladiator/Telegraph"
{
    Properties
    {
        _Intensity ("Emission Boost", Range(0.5, 4)) = 1.1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-5" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -2, -2

        Pass
        {
            Name "Telegraph"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TParams) // x fill 0..1, y style, z seed, w ring thickness
            UNITY_INSTANCING_BUFFER_END(Props)

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V Vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _TColor);
                float4 prm = UNITY_ACCESS_INSTANCED_PROP(Props, _TParams);
                float th = max(prm.w, 0.02);
                if (prm.y > 2.5)
                {
                    // lane: uv.x across (0..1), uv.y along from the start (0) to the far end (1)
                    float ex = 1 - abs(i.uv.x * 2 - 1);                   // 0 at the side edges, 1 on the centre line
                    float aax = fwidth(ex) * 1.5;
                    float rim = 1 - smoothstep(th, th + aax, ex);
                    float lf = saturate(prm.x);
                    float inside = i.uv.y < lf ? 1 : 0;
                    float lfront = 1 - saturate(abs(i.uv.y - lf) / 0.04);
                    float tip = smoothstep(1, 0.97, i.uv.y);                // soft far end
                    float la = max(rim, 0.18 + inside * 0.36 + lfront * lf * 0.5) * tip;
                    return half4(col.rgb * _Intensity * (1 + lfront * 0.35), saturate(la) * col.a);
                }
                float2 p = i.uv * 2 - 1;
                float d = length(p);
                if (d > 1) discard;
                float t = _Time.y + prm.z * 7.13;
                float aa = fwidth(d) * 1.5;
                float ring = smoothstep(1 - th - aa, 1 - th, d) * (1 - smoothstep(1 - aa, 1, d));
                half3 c = col.rgb;
                float a;
                if (prm.y < 0.5)
                {
                    // telegraph: faint base, filling disc, bright front, solid rim
                    float fill = saturate(prm.x);
                    float inside = d < fill ? 1 : 0;
                    float front = 1 - saturate(abs(d - fill) / 0.05);
                    // keep it saturated: brightness pushed through bloom/grading washes orange-red out to cream
                    a = max(ring, 0.18 + inside * 0.36 + front * fill * 0.5);
                    c *= _Intensity * (1 + front * 0.35);
                }
                else if (prm.y < 1.5)
                {
                    // fire patch: flickering tongues, hotter centre, soft edge
                    float ang = atan2(p.y, p.x);
                    float n = sin(ang * 7 + t * 5.3 + d * 9) * 0.5 + 0.5;
                    float n2 = sin(ang * 13 - t * 7.1 + d * 17) * 0.5 + 0.5;
                    float flick = saturate(n * 0.6 + n2 * 0.4);
                    a = (1 - smoothstep(0.7 + flick * 0.2, 1, d)) * (0.5 + 0.35 * flick) * saturate(prm.x * 4);
                    c = lerp(col.rgb, half3(1, 0.85, 0.35), saturate((1 - d) * flick)) * _Intensity;
                }
                else
                {
                    // pulsing ring (elite affix / fuse)
                    float pulse = 0.65 + 0.35 * sin(t * (prm.x > 0 ? 12 : 4));
                    a = ring * pulse + (1 - d) * 0.08;
                    c *= _Intensity;
                }
                return half4(c, saturate(a) * col.a);
            }
            ENDHLSL
        }
    }
}
