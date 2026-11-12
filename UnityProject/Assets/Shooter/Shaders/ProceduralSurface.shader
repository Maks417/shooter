Shader "Shooter/ProceduralSurface"
{
    Properties
    {
        _BaseMap("Generated albedo", 2D) = "white" {}
        _NormalMap("Generated tangent normal", 2D) = "bump" {}
        _MaskMap("Metal occlusion roughness", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Tiling("Repeats per meter", Float) = 1
        [Toggle(_NORMALMAP)] _NormalEnabled("Normal detail", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Tiling;
                float _NormalEnabled;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; half fog:TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS=float4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
                o.uv=input.uv*_Tiling; o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                half3 mask=saturate(SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,input.uv).rgb);
                half3 n=normalize(input.normalWS), t=normalize(input.tangentWS.xyz);
                half3 b=normalize(cross(n,t))*input.tangentWS.w;
                half3 normalTS=half3(0,0,1);
                #if defined(_NORMALMAP)
                    normalTS=normalize(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,input.uv).xyz*2-1);
                #endif
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS; lighting.positionCS=input.positionCS;
                lighting.normalWS=normalize(t*normalTS.x+b*normalTS.y+n*normalTS.z);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo; surface.metallic=mask.r; surface.occlusion=mask.g;
                surface.smoothness=1-mask.b; surface.normalTS=normalTS; surface.alpha=1;
                half4 color=UniversalFragmentPBR(lighting,surface);
                color.rgb=MixFog(color.rgb,input.fog); return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Tiling;
                float _NormalEnabled;
            CBUFFER_END
            float3 _LightDirection;
            float4 ShadowVert(float4 positionOS:POSITION,float3 normalOS:NORMAL):SV_POSITION
            {
                float3 world=TransformObjectToWorld(positionOS.xyz);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,TransformObjectToWorldNormal(normalOS),_LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
