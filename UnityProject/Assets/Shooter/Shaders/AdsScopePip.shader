Shader "Shooter/AdsScopePip"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _BlurTex("Blurred", 2D) = "white" {}
        _LensTex("Optical lens camera", 2D) = "black" {}
        _UseLensTex("Use optical lens camera", Float) = 0
        _BlurOffset("Blur texel offset", Vector) = (1, 0, 0, 0)
        _LensRadius("Lens radius (min-axis UV)", Float) = 0.42
        _Zoom("Lens UV zoom", Float) = 0.525
        _Strength("PiP strength", Float) = 1
        _RimWidth("Housing rim width", Float) = 0.045
        _Aspect("Width / height", Float) = 1.777
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragBlur
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float4 _BlurOffset;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 FragBlur(Varyings input) : SV_Target
            {
                float2 o = _BlurOffset.xy * _MainTex_TexelSize.xy;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * 0.227027f;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + o) * 0.316216f;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - o) * 0.316216f;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + o * 2.0f) * 0.070270f;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - o * 2.0f) * 0.070270f;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BlurTex); SAMPLER(sampler_BlurTex);
            TEXTURE2D(_LensTex); SAMPLER(sampler_LensTex);
            float _UseLensTex;
            float _LensRadius;
            float _Zoom;
            float _Strength;
            float _RimWidth;
            float _Aspect;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 FragComposite(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float aspect = max(_Aspect, 1e-4f);
                float2 q = uv - 0.5f;
                // Pixel-equivalent radius: lens uses LensRadiusFraction of min(width,height).
                // For landscape, min axis is height → top-center distance in these units is 0.5.
                float rd = length(float2(q.x * aspect, q.y));
                float lensR = _LensRadius;
                float rim = max(_RimWidth, 1e-4f);
                float strength = saturate(_Strength);

                float zoom = max(_Zoom, 0.05f);
                float2 uvZoom = saturate(0.5f + q * zoom);

                half3 sharp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZoom).rgb;
                // ADR-103: the optical lens camera renders exactly the lens disc (square target, lens diameter wide).
                float2 lensUv = saturate(0.5f + float2(q.x * aspect, q.y) / max(2.0f * _LensRadius, 1e-4f));
                half3 optical = SAMPLE_TEXTURE2D(_LensTex, sampler_LensTex, lensUv).rgb;
                sharp = lerp(sharp, optical, step(0.5f, _UseLensTex));
                half3 blurred = SAMPLE_TEXTURE2D(_BlurTex, sampler_BlurTex, uv).rgb;
                half3 hip = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;

                float inside = 1.0f - smoothstep(lensR - rim, lensR, rd);
                float housing = smoothstep(lensR - rim * 1.35f, lensR - rim * 0.2f, rd)
                              * (1.0f - smoothstep(lensR, lensR + rim * 0.9f, rd));

                half3 peri = lerp(hip, blurred, strength);
                half3 col = lerp(peri, sharp, inside);
                half3 rimCol = half3(0.02h, 0.022h, 0.028h);
                col = lerp(col, rimCol, housing * max(strength, 0.35h));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
