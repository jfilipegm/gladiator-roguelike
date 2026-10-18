// Gladiator/PaletteLit — the one lit shader for everything opaque (environment, characters, enemies, props).
// Stylised, not PBR: wrap-lit main light with tinted shadows, hemisphere ambient, optional rim, emission from the
// palette alpha (A = 1 - emission), optional point-sampled "pixel noise" grain, and a flash term used by the shared
// hit-flash material. SRP-Batcher compatible (all material data in UnityPerMaterial) and GPU-instancing ready.
Shader "Gladiator/PaletteLit"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap ("Palette", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _EmissionBoost ("Emission Boost (palette alpha)", Range(0, 8)) = 2.5
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.66, 0.9, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.8
        _RimColor ("Rim Color", Color) = (1, 0.96, 0.88, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _PixelNoise ("Pixel Noise", Range(0, 0.3)) = 0
        _PixelDensity ("Pixel Density (texels per m)", Range(1, 32)) = 8
        [Toggle] _NoiseWorldSpace ("Noise In World Space", Float) = 1
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _Flash ("Flash", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowTint;
            half4 _RimColor;
            half4 _FlashColor;
            half _EmissionBoost;
            half _Wrap;
            half _ShadowStrength;
            half _RimStrength;
            half _RimPower;
            half _PixelNoise;
            half _PixelDensity;
            half _NoiseWorldSpace;
            half _Flash;
            half _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Hemisphere ambient, set per scene by EnvironmentLighting (falls back to the SH ambient probe when unset).
        half4 _GLAmbientSky;
        half4 _GLAmbientEquator;
        half4 _GLAmbientGround;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float3 noisePos : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
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
                if (dot(sky, sky) + dot(_GLAmbientGround.rgb, _GLAmbientGround.rgb) < 1e-6)
                    return SampleSH(n);
                half up = n.y;
                return up >= 0 ? lerp(_GLAmbientEquator.rgb, sky, up) : lerp(_GLAmbientEquator.rgb, _GLAmbientGround.rgb, -up);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.uv = v.uv;
                o.noisePos = lerp(v.positionOS.xyz, pos.positionWS, _NoiseWorldSpace);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 pal = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = pal.rgb * _BaseColor.rgb;
                half3 n = normalize(i.normalWS);

                // Point-sampled grain on the dominant projection plane (retro "pixel texture" without textures).
                half3 an = abs(n);
                float2 p2 = (an.y > max(an.x, an.z)) ? i.noisePos.xz : ((an.x > an.z) ? i.noisePos.zy : i.noisePos.xy);
                half grain = Hash12(floor(p2 * _PixelDensity));
                albedo *= 1.0h + (grain - 0.5h) * 2.0h * _PixelNoise;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = dot(n, mainLight.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half litAmount = min(wrapped, shadow);

                half3 light = Ambient(n) * lerp(_ShadowTint.rgb, half3(1, 1, 1), litAmount)
                            + mainLight.color * wrapped * shadow;

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light dl = GetAdditionalLight(li, i.positionWS, inputData.shadowMask);
                    light += dl.color * dl.distanceAttenuation * saturate(dot(n, dl.direction));
                }
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS, inputData.shadowMask);
                    light += al.color * al.distanceAttenuation * saturate(dot(n, al.direction) * 0.8h + 0.2h);
                LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                light += VertexLighting(i.positionWS, n);
                #endif

                half3 color = albedo * light;

                half rim = pow(1.0h - saturate(dot(n, inputData.viewDirectionWS)), _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * (0.4h + 0.6h * wrapped);

                // Emissive swatches ignore lighting and glow (HDR -> bloom).
                half e = 1.0h - pal.a;
                color = lerp(color, albedo * (1.0h + _EmissionBoost), e);

                color = lerp(color, _FlashColor.rgb * 1.6h, _Flash);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DNVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 DNFrag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
