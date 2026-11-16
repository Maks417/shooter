Shader "Shooter/ProceduralDecal"
{
 Properties { _BaseMap("Numeric atlas",2D)="white"{} _AtlasRect("Atlas rect",Vector)=(0,0,.25,.5) _Opacity("Opacity",Float)=1 _DrawOrder("Order",Float)=0 }
 SubShader
 {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial) float4 _AtlasRect; float _Opacity; CBUFFER_END
 struct Attributes { float3 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 Varyings Vert(Attributes i) {Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS);o.uv=i.uv;return o;}
 half4 Paint(float2 uv) {half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,_AtlasRect.xy+uv*_AtlasRect.zw);c.a*=_Opacity;return c;}
 ENDHLSL
 Pass
 {
 Name "DecalScreenSpaceProjector" Tags {"LightMode"="DecalScreenSpaceProjector"}
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Greater Cull Front
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 UNITY_INSTANCING_BUFFER_START(Decal)
 UNITY_DEFINE_INSTANCED_PROP(half4x4, _NormalToWorld)
 UNITY_INSTANCING_BUFFER_END(Decal)
 half4 Frag(Varyings i):SV_Target
 {
  UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;
  float depth=SampleSceneDepth(uv);
  #if !UNITY_REVERSED_Z
  depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
  #endif
  float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
  float3 local=TransformWorldToObject(world);clip(.5-max(abs(local.x),max(abs(local.y),abs(local.z))));
  half4 color=Paint(local.xz+.5);half4x4 projector=UNITY_ACCESS_INSTANCED_PROP(Decal,_NormalToWorld);color.a*=projector[0][3];return color;
 }
 ENDHLSL
 }
 Pass
 {
 Name "LowMesh" Tags {"LightMode"="UniversalForward"}
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off Offset -1,-1
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 half4 Frag(Varyings i):SV_Target {UNITY_SETUP_INSTANCE_ID(i); return Paint(i.uv);}
 ENDHLSL
 }
 }
}
