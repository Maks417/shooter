Shader "Shooter/CombatParticle"
{
 Properties { _BaseMap("Numeric flipbook",2D)="white"{} _Ring("Shockwave",Float)=0 _Brightness("Radiance",Float)=1 _SoftParticles("Depth softening",Float)=0 _SrcBlend("Src",Float)=5 _DstBlend("Dst",Float)=10 }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
 Pass {Blend [_SrcBlend] [_DstBlend] ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma target 3.0
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)float _Brightness;float _SoftParticles;float _Ring;CBUFFER_END
 struct A{float3 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float eye:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 world=TransformObjectToWorld(i.p);o.p=TransformWorldToHClip(world);o.eye=-TransformWorldToView(world).z;o.uv=i.uv;o.color=i.color;return o;}
 half4 Frag(V i):SV_Target
 {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.color;c.rgb*=_Brightness;
  if(_Ring>.5){float2 q=frac(i.uv*8)-.5;float r=length(q);c=half4(i.color.rgb*(_Brightness*.4),saturate(1-abs(r-.36)*24)*i.color.a);}
  if(_SoftParticles>.5){float scene=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);c.a*=saturate((scene-i.eye)*2);}
  return c;
 }
 ENDHLSL
 } }
}
