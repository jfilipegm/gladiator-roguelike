// Gladiator/BloodSmear — the Blood Slide trail (note 32): blood smeared on the sand, drawn instanced by
// BloodTrailRenderer, one ground quad per Bleed slide-trail mark (uv.x across the slide, uv.y along it, 1 = forward).
// _SmearTex (PixelSrc/blood_smears.py; linear, point filtered) is data: R thickness in 4 steps, G wet glint, B a
// per-pixel crumble noise, A coverage. Per instance: x age 0..1 of the mark's life, y its life (s), z which atlas cell,
// w mirror (-1) across the slide. Over a mark's life the blood splashes in (thick first, _GrowTime), dries from
// saturated crimson to maroon-brown (thin edges first, pixel by pixel), loses its glint, then crumbles away thin-first
// down to the pools and is gone at the end of its life. Lit like the floor it lies on (PaletteLit's wrap light, tinted
// shadows, hemisphere ambient), no emission or glow: paint on the floor, not an effect. Deep crimson, never orange-red
// (CLAUDE.md §5: orange-red means "this hurts the player"). Opaque by default: the smears overlap (newest on top), and
// any see-through would show the layers as beads along the streak. Keep the colour rules in step with the preview in
// PixelSrc/blood_smears.py.
Shader "Gladiator/BloodSmear"
{
    Properties
    {
        [NoScaleOffset] _SmearTex ("Smears (R thick, G glint, B noise, A cover)", 2D) = "black" {}
        _Cells ("Atlas Cells Per Side", Float) = 2
        _FreshThin ("Fresh: Thin Edge", Color) = (0.55, 0.05, 0.09, 1)
        _FreshThick ("Fresh: Pooled", Color) = (0.33, 0.02, 0.04, 1)
        _Sheen ("Fresh: Wet Glint", Color) = (0.72, 0.18, 0.21, 1)
        _DryThin ("Dried: Thin Edge", Color) = (0.33, 0.12, 0.08, 1)
        _DryThick ("Dried: Pooled", Color) = (0.17, 0.04, 0.04, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _GrowTime ("Splash In (s)", Range(0.01, 0.5)) = 0.1
        _DryStart ("Dry Start (life)", Range(0, 1)) = 0.15
        _DryLength ("Dry Length (life)", Range(0.05, 1)) = 0.5
        _EdgeLead ("Thin Edges Dry First (life)", Range(0, 0.5)) = 0.15
        _DryNoise ("Dry Unevenness (life)", Range(0, 0.5)) = 0.15
        _SheenEnd ("Glint Gone By (life)", Range(0.01, 1)) = 0.3
        _FadeStart ("Crumble Start (life)", Range(0, 0.99)) = 0.6
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.66, 0.9, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.8
    }
    SubShader
    {
        // under the blob shadows (Transparent-10) and every telegraph (Transparent-5): enemies and threats read on top
        Tags { "Queue" = "Transparent-12" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -1

        Pass
        {
            Name "BloodSmear"
            Tags { "LightMode" = "UniversalForward" }
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FreshThin, _FreshThick, _Sheen, _DryThin, _DryThick, _ShadowTint;
                half _Cells, _Opacity, _GrowTime, _DryStart, _DryLength, _EdgeLead, _DryNoise, _SheenEnd, _FadeStart;
                half _Wrap, _ShadowStrength;
            CBUFFER_END

            TEXTURE2D(_SmearTex);
            SAMPLER(smp_smear_point_clamp); // inline sampler: crisp texels and no bleeding between cells, whatever the import

            // Hemisphere ambient, set per scene by EnvironmentLighting (falls back to the SH ambient probe when unset).
            half4 _GLAmbientSky;
            half4 _GLAmbientEquator;
            half4 _GLAmbientGround;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SmearParams) // x age 0..1, y life (s), z atlas cell, w mirror +-1
            UNITY_INSTANCING_BUFFER_END(Props)

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half3 Ambient(half3 n)
            {
                half3 sky = _GLAmbientSky.rgb;
                half up = n.y;
                half3 hemi = up >= 0 ? lerp(_GLAmbientEquator.rgb, sky, up) : lerp(_GLAmbientEquator.rgb, _GLAmbientGround.rgb, -up);
                bool unset = dot(sky, sky) + dot(_GLAmbientGround.rgb, _GLAmbientGround.rgb) < 1e-6;
                return unset ? SampleSH(n) : hemi;
            }

            V Vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                // the quad's up axis is the ground normal it was laid on (its y scale is 1, x and z are perpendicular)
                o.normalWS = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0)));
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 prm = UNITY_ACCESS_INSTANCED_PROP(Props, _SmearParams);
                float age = saturate(prm.x);
                float2 uv = i.uv;
                if (prm.w < 0) uv.x = 1 - uv.x;
                float cells = max(_Cells, 1);
                float2 cell = float2(fmod(prm.z, cells), floor(prm.z / cells));
                half4 s = SAMPLE_TEXTURE2D(_SmearTex, smp_smear_point_clamp, (cell + saturate(uv)) / cells);
                if (s.a < 0.5) discard;

                // splash in (thick first) and crumble out (thin first), pixel by pixel through the noise
                float keep = s.r * 0.8 + s.b * 0.2;
                float grow = saturate(age * prm.y / _GrowTime);
                float crumble = saturate((age - _FadeStart) / (1 - _FadeStart));
                if (keep < 1 - grow || keep <= crumble) discard;

                // thick blood is darker; it dries from crimson to maroon-brown, thin edges and random pixels first,
                // in 3 steps (flat pixel-art tones, no gradient)
                float t = saturate((s.r - 0.25) / 0.75);
                float dry = saturate((age - _DryStart + (1 - t) * _EdgeLead + (s.b - 0.5) * _DryNoise) / _DryLength);
                dry = floor(dry * 3 + 0.5) / 3;
                half3 albedo = lerp(lerp(_FreshThin.rgb, _FreshThick.rgb, t), lerp(_DryThin.rgb, _DryThick.rgb, t), dry);
                if (s.g > 0.5 && age < _SheenEnd * (0.6 + 0.4 * s.b)) albedo = _Sheen.rgb;

                // lit like PaletteLit lights the floor under it
                half3 n = normalize(i.normalWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half wrapped = saturate((dot(n, mainLight.direction) + _Wrap) / (1.0h + _Wrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half3 light = Ambient(n) * lerp(_ShadowTint.rgb, half3(1, 1, 1), min(wrapped, shadow))
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

                half3 color = MixFog(albedo * light, i.fog);
                return half4(color, _Opacity);
            }
            ENDHLSL
        }
    }
}
