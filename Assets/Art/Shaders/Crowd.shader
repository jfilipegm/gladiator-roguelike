// Gladiator/Crowd — instanced spectators (CLAUDE.md §4 "Crowd"). Same stylised lighting as PaletteLit (wrap-lit sun,
// hemisphere ambient, fog), plus: every instance gets a tunic colour and an animation phase hashed from its world
// position (no per-instance buffers), and the global _CrowdExcitement (0..1, CrowdExcitement.cs) drives how hard the
// crowd bobs, sways and throws its arms up. The tunic is the palette's `cloth_white` cell (_TintCell = u0,v0,u1,v1).
Shader "Gladiator/Crowd"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap ("Palette", 2D) = "white" {}
        _TintCell ("Tunic swatch UV rect", Vector) = (0, 0.375, 0.0625, 0.4375)
        _TintA ("Tunic A", Color) = (0.62, 0.32, 0.2, 1)
        _TintB ("Tunic B", Color) = (0.72, 0.6, 0.32, 1)
        _TintC ("Tunic C", Color) = (0.42, 0.48, 0.3, 1)
        _TintD ("Tunic D", Color) = (0.38, 0.44, 0.58, 1)
        _TintE ("Tunic E", Color) = (0.55, 0.5, 0.46, 1)
        _TintF ("Tunic F", Color) = (0.8, 0.74, 0.62, 1)
        _Wrap ("Light Wrap", Range(0, 1)) = 0.45
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.66, 0.9, 1)
        _Bob ("Bob Height (calm, excited)", Vector) = (0.015, 0.16, 0, 0)
        _BobSpeed ("Bob Speed (calm, excited)", Vector) = (2.2, 7.5, 0, 0)
        _ArmLift ("Arm Lift (excited)", Range(0, 0.4)) = 0.14
        _Sway ("Sway (radians)", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TintCell;
                half4 _TintA, _TintB, _TintC, _TintD, _TintE, _TintF;
                half4 _ShadowTint;
                float4 _Bob, _BobSpeed;
                half _Wrap, _ArmLift, _Sway;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            half4 _GLAmbientSky, _GLAmbientEquator, _GLAmbientGround;
            float _CrowdExcitement;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                nointerpolation half3 tint : TEXCOORD3;
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half3 Ambient(half3 n)
            {
                half3 sky = _GLAmbientSky.rgb;
                half up = n.y;
                half3 hemi = up >= 0 ? lerp(_GLAmbientEquator.rgb, sky, up) : lerp(_GLAmbientEquator.rgb, _GLAmbientGround.rgb, -up);
                bool unset = dot(sky, sky) + dot(_GLAmbientGround.rgb, _GLAmbientGround.rgb) < 1e-6;
                return unset ? SampleSH(n) : hemi;
            }

            half3 PickTint(float h)
            {
                int k = (int)floor(h * 6.0);
                return k == 0 ? _TintA.rgb : k == 1 ? _TintB.rgb : k == 2 ? _TintC.rgb : k == 3 ? _TintD.rgb : k == 4 ? _TintE.rgb : _TintF.rgb;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float h = Hash12(origin.xz * 1.37);
                float phase = h * 6.2831;
                float exc = saturate(_CrowdExcitement);
                float t = _Time.y * lerp(_BobSpeed.x, _BobSpeed.y, exc) + phase;

                float3 p = v.positionOS.xyz;
                // sway around the vertical axis
                float a = sin(_Time.y * 1.3 + phase * 1.7) * _Sway * (0.4 + 0.6 * exc);
                float s = sin(a), c = cos(a);
                p.xz = float2(p.x * c - p.z * s, p.x * s + p.z * c);
                // arms (above the shoulders, off-centre) pump with excitement
                float arm = step(1.15, v.positionOS.y) * step(0.2, abs(v.positionOS.x));
                p.y += arm * _ArmLift * exc * (0.5 + 0.5 * sin(t * 1.5));
                // the whole body hops
                p.y += lerp(_Bob.x, _Bob.y, exc) * abs(sin(t));

                VertexPositionInputs pos = GetVertexPositionInputs(p);
                o.positionCS = pos.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                o.tint = PickTint(Hash12(origin.zx * 2.11 + 17.0));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 pal = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                bool tunic = i.uv.x > _TintCell.x && i.uv.x < _TintCell.z && i.uv.y > _TintCell.y && i.uv.y < _TintCell.w;
                half3 albedo = tunic ? i.tint : pal.rgb;
                half3 n = normalize(i.normalWS);
                Light mainLight = GetMainLight();
                half wrapped = saturate((dot(n, mainLight.direction) + _Wrap) / (1.0h + _Wrap));
                half3 light = Ambient(n) * lerp(_ShadowTint.rgb, half3(1, 1, 1), wrapped) + mainLight.color * wrapped;
                half3 color = MixFog(albedo * light, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
