Shader "Shooter/CombatParticle"
{
 Properties { _BaseMap("Flipbook",2D)="white"{} _Ring("Shockwave",Float)=0 _Brightness("Radiance",Float)=1 _SoftParticles("Depth softening",Float)=0 _SrcBlend("Src",Float)=5 _DstBlend("Dst",Float)=10
  _FrameBlend("Blend adjacent flipbook frames (UV2 + AnimBlend streams)",Float)=0 _Lit("Scene light on non-glowing texels",Float)=0 _Emission("HDR boost of fiery texels",Float)=0 }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
 Pass {Blend [_SrcBlend] [_DstBlend] ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma target 3.0
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)float _Brightness;float _SoftParticles;float _Ring;float _FrameBlend;float _Lit;float _Emission;CBUFFER_END
 // Particle vertex streams (ADR-126): Position, Color, UV (xy), UV2 (zw: next frame), AnimBlend (TEXCOORD1.x).
 // Meshes and line renderers that also use this shader carry only xy; _FrameBlend stays 0 for them.
 struct A{float3 p:POSITION;float4 uv:TEXCOORD0;float blend:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float4 uv:TEXCOORD0;half4 color:COLOR;float eye:TEXCOORD1;float blend:TEXCOORD2;half3 light:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A i)
 {
  V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
  float3 world=TransformObjectToWorld(i.p);o.p=TransformWorldToHClip(world);o.eye=-TransformWorldToView(world).z;o.uv=i.uv;o.color=i.color;o.blend=i.blend*_FrameBlend;
  // Smoke takes the sky ambient plus part of the sun colour, so it is grey at noon and dim at dusk instead of glowing.
  half3 light=SampleSH(half3(0,1,0))+GetMainLight().color*.45h;o.light=min(light,1.5h);
  return o;
 }
 half4 Frag(V i):SV_Target
 {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  half4 t=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv.xy);
  if(_FrameBlend>.5){half4 next=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv.zw);t=lerp(t,next,saturate(i.blend));}
  // Fiery texels are red over blue; grey smoke is not. Fire glows (HDR) and ignores scene light, smoke is lit.
  half hot=saturate((t.r-t.b)*2.5h-.25h);
  half4 c=t*i.color;c.rgb*=_Brightness*(1+_Emission*hot);
  c.rgb*=lerp((half3)1,i.light,_Lit*(1-hot));
  if(_Ring>.5){float2 q=frac(i.uv.xy*8)-.5;float r=length(q);c=half4(i.color.rgb*(_Brightness*.4),saturate(1-abs(r-.36)*24)*i.color.a);}
  if(_SoftParticles>.5){float scene=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);c.a*=saturate((scene-i.eye)*2);}
  return c;
 }
 ENDHLSL
 } }
}
