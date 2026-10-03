Shader "Bidwarss/BrunoCel"
{
    Properties
    {
        _BaseColor("Base color", Color) = (1,1,1,1)
        _ShadowTint("Cool shadow tint", Color) = (.60,.55,.80,1)
        _OutlineColor("Ink", Color) = (.035,.028,.05,1)
        _OutlineWidth("Outline width (metres)", Range(0,.01)) = .002
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowTint;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float fog:TEXCOORD2; };
        Varyings Vert(Attributes i)
        {
            Varyings o;
            o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(i.normalOS);
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "CelForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            half4 Frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Stable art-directed fill prevents black faces inside the depot.
                float key=max(dot(n,light.direction),dot(n,normalize(float3(-.5,.85,.65)))*.7);
                float level=key>.42?1:(key>-.15?.77:.49);
                float shadow=lerp(.73,1,light.shadowAttenuation);
                half3 tint=lerp(_ShadowTint.rgb,half3(1,1,1),step(.42,key));
                half3 lightColor=lerp(half3(1,1,1),saturate(light.color),.18);
                half3 color=_BaseColor.rgb*level*tint*shadow*lightColor;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "InkOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            Varyings OutlineVert(Attributes i)
            {
                i.positionOS.xyz+=i.normalOS*_OutlineWidth;
                return Vert(i);
            }
            half4 OutlineFrag(Varyings i):SV_Target { return half4(MixFog(_OutlineColor.rgb,i.fog),1); }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0 ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVert(Attributes i):SV_POSITION
            {
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(i.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-p);
                #else
                    float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
                #if UNITY_REVERSED_Z
                    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 DepthFrag():SV_Target {return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i):SV_Target {return i.positionCS.z;}
            ENDHLSL
        }
    }
    FallBack Off
}
