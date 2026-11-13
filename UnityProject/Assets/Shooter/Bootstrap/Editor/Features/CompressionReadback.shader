Shader "Hidden/Shooter/CompressionReadback"
{
    Properties { _MainTex("Numeric source", 2D) = "black" {} _Mip("Mip", Float) = 0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Mip;
            float4 frag(v2f_img input) : SV_Target { return tex2Dlod(_MainTex, float4(input.uv, 0, _Mip)); }
            ENDHLSL
        }
    }
    FallBack Off
}
