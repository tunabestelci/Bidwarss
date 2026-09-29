// Kasa Avcisi - Depo cel-shade shader'i (URP)
// - Bake edilmis isigi (lightmap / light probe) + ana isigi 3 banda boler
// - Kutu ve silindir kenarlarina piksel-sabit murekkep cizgisi (UV kenarlari)
// - Ters kabuk dis kontur (Unity ilkel kup/silindiri icin kusursuz; ozel mesh'lerde normal modu)
// Web onizlemedeki gorunumle ayni mantik.
Shader "Toon/DepoCel"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base color", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)

        [Header(Cel bantlari)]
        _ShadowTint ("Golge tonu", Color) = (0.62, 0.62, 0.76, 1)
        _Band1 ("Bant 1 esigi", Range(0,2)) = 0.28
        _Band2 ("Bant 2 esigi", Range(0,2)) = 0.62
        _BandSoft ("Bant yumusakligi", Range(0.001,0.2)) = 0.025
        _MinLight ("Karanlik bant", Range(0,1)) = 0.45
        _MidLight ("Orta bant", Range(0,1)) = 0.74
        _Exposure ("Pozlama", Range(0.2,4)) = 1.0
        _RimStrength ("Kenar isigi", Range(0,1)) = 0.10

        [Header(Murekkep)]
        [Toggle] _EdgeLines ("Kenar cizgisi (UV)", Float) = 1
        _EdgeColor ("Cizgi rengi", Color) = (0.11, 0.13, 0.15, 1)
        _EdgeWidth ("Cizgi kalinligi (px)", Range(0,4)) = 1.35
        _OutlineWidth ("Dis kontur (m)", Range(0,0.1)) = 0.028
        _OutlineColor ("Dis kontur rengi", Color) = (0.11, 0.13, 0.15, 1)
        [Toggle] _OutlineNormals ("Kontur: normal yonu (ozel mesh)", Float) = 0

        // Lit pass'leri (golge, derinlik, bake meta) icin gerekli isimler
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0.2
        [HideInInspector] _Metallic ("Metallic", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _ShadowTint;
            half _Band1, _Band2, _BandSoft, _MinLight, _MidLight, _Exposure, _RimStrength;
            half _EdgeLines;
            half4 _EdgeColor;
            half _EdgeWidth;
            half _OutlineWidth;
            half4 _OutlineColor;
            half _OutlineNormals;
            half _Cutoff, _Smoothness, _Metallic;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "DepoCelForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 rawUV      : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3  normalWS   : TEXCOORD3;
                float2 lightmapUV : TEXCOORD4;
                half   fogFactor  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.rawUV = input.uv;
                o.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 BakedLight(Varyings i, half3 n)
            {
            #if defined(LIGHTMAP_ON)
                half4 enc = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, i.lightmapUV);
                return DecodeLightmap(enc, half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0, 0));
            #else
                return SampleSH(n);
            #endif
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                half3 light = BakedLight(i, n);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                light += mainLight.color * saturate(dot(n, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
            #if defined(_ADDITIONAL_LIGHTS) && !defined(_FORWARD_PLUS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    light += l.color * saturate(dot(n, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                }
            #endif

                // cel bantlari
                half lum = max(1e-4, dot(light, half3(0.299, 0.587, 0.114))) * _Exposure;
                half t1 = smoothstep(_Band1 - _BandSoft, _Band1 + _BandSoft, lum);
                half t2 = smoothstep(_Band2 - _BandSoft, _Band2 + _BandSoft, lum);
                half band = lerp(lerp(_MinLight, _MidLight, t1), 1.0h, t2);
                half3 hue = light / max(max(light.r, light.g), max(light.b, 1e-4));
                hue = lerp(half3(1, 1, 1), hue, 0.5h);
                half3 tint = lerp(_ShadowTint.rgb, half3(1, 1, 1), t1);
                half dark = saturate(lum / 0.06);
                half3 col = albedo.rgb * band * tint * hue * dark;

                // kenar isigi
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = smoothstep(0.62, 0.68, 1.0h - saturate(dot(n, v)));
                col += albedo.rgb * rim * _RimStrength * t1;

                col += _EmissionColor.rgb;

                // murekkep: UV kenar cizgisi (piksel sabit, uzakta soner)
                float2 fw = max(fwidth(i.rawUV), float2(1e-5, 1e-5));
                if (_EdgeLines > 0.5)
                {
                    float2 dd = min(i.rawUV, 1.0 - i.rawUV) / fw;
                    float px = min(dd.x, dd.y);
                    float big = max(fw.x, fw.y);
                    float fade = 1.0 - smoothstep(0.05, 0.16, big);
                    float ln = (1.0 - smoothstep(_EdgeWidth - 0.55, _EdgeWidth + 0.55, px)) * fade;
                    col = lerp(col, _EdgeColor.rgb, ln * 0.9);
                }

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepoCelOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertOutline(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = input.positionOS.xyz;
                // Unity kupu (+-0.5) ve silindiri (yaricap 0.5, y +-1) icin esit kalinlikta kabuk
                float3 dir = float3(p.x * 2.0, p.y >= 0 ? 1.0 : -1.0, p.z * 2.0);
                if (_OutlineNormals > 0.5) dir = normalize(input.normalOS);
                float3 sc = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                float3 posOS = p + dir * _OutlineWidth / max(sc, float3(1e-4, 1e-4, 1e-4));
                o.positionCS = TransformObjectToHClip(posOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 fragOutline(Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/Lit/META"
    }
    FallBack "Universal Render Pipeline/Lit"
}
