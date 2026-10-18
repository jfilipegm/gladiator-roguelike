// Gladiator/SkyGradient — procedural skybox: zenith / horizon / ground gradient, soft sun glow and a band of
// chunky low-frequency "cloud" blobs, point-quantised so it reads as retro rather than photographic.
Shader "Gladiator/SkyGradient"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.20, 0.45, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.70, 0.86, 0.96, 1)
        _GroundColor ("Ground", Color) = (0.42, 0.40, 0.38, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.2
        _SunColor ("Sun Glow", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Glow Size", Range(1, 256)) = 48
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _CloudAmount ("Cloud Amount", Range(0, 1)) = 0.45
        _CloudScale ("Cloud Scale", Range(1, 32)) = 7
        _CloudSteps ("Cloud Quantise Steps", Range(8, 256)) = 64
        _CloudSpeed ("Cloud Drift", Range(0, 0.1)) = 0.004
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudColor;
                half _HorizonSharpness, _SunSize, _CloudAmount, _CloudScale, _CloudSteps, _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half up = d.y;
                half3 col = up >= 0
                    ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), 1.0 / _HorizonSharpness))
                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 6));

                // Clouds: project onto a dome, quantise the lookup so blobs have chunky edges.
                if (up > 0.02)
                {
                    float2 uv = d.xz / (up + 0.25) * _CloudScale * 0.1;
                    uv.x += _Time.y * _CloudSpeed;
                    uv = floor(uv * _CloudSteps) / _CloudSteps;
                    float n = Noise(uv * 3) * 0.6 + Noise(uv * 7) * 0.3 + Noise(uv * 15) * 0.1;
                    float c = smoothstep(1 - _CloudAmount, 1 - _CloudAmount + 0.08, n) * smoothstep(0.02, 0.25, up);
                    col = lerp(col, _CloudColor.rgb, c * 0.9);
                }

                Light sun = GetMainLight();
                half s = pow(saturate(dot(d, sun.direction)), _SunSize);
                col += _SunColor.rgb * s * 0.8;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
