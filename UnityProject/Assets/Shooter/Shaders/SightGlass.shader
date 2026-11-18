Shader "Shooter/SightGlass"
{
 // ADR-115 clear reflex-sight pane: almost fully transparent at normal incidence, a faint blue-cyan coating tint,
 // a Fresnel rim toward grazing angles and a soft sky sheen, so the window reads as a lens and never as a dark disc.
 Properties { _Tint("Coating tint",Color)=(0.55,0.78,1.0,1) _Alpha("Head-on opacity",Range(0,0.5))=0.07 _Rim("Grazing opacity",Range(0,1))=0.55 _Sheen("Sky sheen",Range(0,1))=0.35 }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
 Pass {Tags {"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma target 3.0
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)half4 _Tint;half _Alpha;half _Rim;half _Sheen;CBUFFER_END
 struct A{float3 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=TransformObjectToWorld(i.p);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);return o;}
 half4 Frag(V i):SV_Target
 {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  float3 n=normalize(i.n);float3 v=normalize(GetWorldSpaceViewDir(i.w));
  half ndv=saturate(abs(dot(n,v)));
  half fresnel=pow(1-ndv,5);
  // Reflection of a bright sky above and a dark ground below: a soft gradient sheen across the pane.
  float3 r=reflect(-v,n*sign(dot(n,v)));
  half sky=saturate(r.y*0.5+0.5);
  half3 col=lerp(_Tint.rgb,half3(0.85,0.92,1.0),sky*_Sheen);
  half a=saturate(_Alpha+fresnel*_Rim+sky*sky*_Sheen*0.06);
  return half4(col,a);
 }
 ENDHLSL
 } }
}
