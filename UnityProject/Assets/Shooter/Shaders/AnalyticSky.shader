Shader "Shooter/AnalyticSky"
{
 Properties { _Environment("Numeric sky cube",Cube)=""{} }
 SubShader {Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
 Pass {Cull Off ZWrite Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURECUBE(_Environment);SAMPLER(sampler_Environment);
 struct A{float3 p:POSITION;};struct V{float4 p:SV_POSITION;float3 ray:TEXCOORD0;};
 V Vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.ray=i.p;return o;}
 half4 Frag(V i):SV_Target{return half4(SAMPLE_TEXTURECUBE(_Environment,sampler_Environment,normalize(i.ray)).rgb,1);}
 ENDHLSL
 } }
}
