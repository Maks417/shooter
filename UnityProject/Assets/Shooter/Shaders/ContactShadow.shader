Shader "Shooter/ContactShadow"
{
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10"}
 Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1,-1
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float3 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.uv=i.uv;return o;}
 half4 Frag(V i):SV_Target{float2 q=(i.uv-.5)*2;float a=saturate(1-dot(q,q));return half4(.02,.025,.02,a*a*.22);}
 ENDHLSL
 } }
}
