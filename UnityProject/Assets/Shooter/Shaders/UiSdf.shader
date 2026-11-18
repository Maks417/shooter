// ADR-084: analytic signed-distance UI primitives for the IMGUI shell/HUD.
// Pure code: no textures are sampled except IMGUI's own clip mask, so the player art audit is unchanged.
// Drawn with Graphics.DrawTexture inside OnGUI Repaint; one quad per shape, padded for shadow/glow.
Shader "Shooter/UiSdf"
{
    Properties
    {
        _MainTex("Unused", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "IgnoreProjector"="True" }
        ZTest Always ZWrite Off Cull Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "UiSdf"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // IMGUI clip mask (same globals the engine's GUI texture shader reads).
            TEXTURE2D(_GUIClipTexture); SAMPLER(sampler_GUIClipTexture);
            // ADR-085 icon atlas (R8 signed distance, 128 = edge) bound as the DrawTexture texture.
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _GlyphRect; // atlas uv x, y, width, height of the icon cell
            float4 _GlyphMap;  // spread in atlas px, logical px per atlas px, unused, unused
            float4x4 unity_GUIClipTextureMatrix;

            float4 _SizePad;   // quad width, quad height (logical px), pad (logical px), anti-alias (logical px)
            float4 _ShapeA;    // kind, corner radius, stroke width (0 = fill), gradient mode
            float4 _Color;     // fill colour (straight alpha, output space)
            float4 _Color2;    // gradient end colour
            float4 _Shadow;    // offset x, offset y, softness, alpha
            float4 _Glow;      // width, alpha, glint position (-1 none), glint alpha
            float4 _GlowColor;
            float4 _Arc;       // start rad, sweep rad, thickness, unused
            float4 _Fade;      // band, strength, unused, unused
            float4 _ClipCircle; // ADR-104 circular mask: centre x, y, radius, anti-alias in this quad's centred logical frame (same as p; radius 0 = off)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 clipUV : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 viewPos = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                o.clipUV = mul(unity_GUIClipTextureMatrix, float4(viewPos.xy, 0, 1)).xy;
                o.uv = input.uv;
                return o;
            }

            // Evaluated in the quad's own local frame (rotation-invariant), not in view space: inside IMGUI the
            // pipeline's view matrices are not a reliable device-pixel frame, which masked out all minimap content.
            float CircleMask(float2 p)
            {
                if (_ClipCircle.z <= 0) return 1;
                float aa = max(_ClipCircle.w, 1e-3);
                return saturate((_ClipCircle.z - length(p - _ClipCircle.xy)) / aa + 0.5);
            }

            static const float Tau = 6.28318530718;

            float RoundedBox(float2 p, float2 h, float r)
            {
                r = clamp(r, 0, min(h.x, h.y));
                float2 q = abs(p) - h + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            float Triangle(float2 p, float2 h)
            {
                // Apex at top (y down), base at bottom.
                float2 q = float2(abs(p.x), p.y + h.y);
                float2 n = normalize(float2(2 * h.y, -h.x));
                return max(dot(q, n), p.y - h.y);
            }

            float Diamond(float2 p, float2 h)
            {
                float2 q = abs(p) / max(h, 1e-3);
                return (q.x + q.y - 1) * min(h.x, h.y) * 0.7071;
            }

            float Arc(float2 p, float2 h)
            {
                float t = max(_Arc.z, 0.5);
                float outer = min(h.x, h.y);
                float r = length(p);
                float ring = abs(r - (outer - t * 0.5)) - t * 0.5;
                if (_Arc.y >= Tau - 1e-4) return ring;
                float ang = atan2(p.x, -p.y);              // 0 at top, clockwise positive (y down)
                float rel = fmod(ang - _Arc.x + 2 * Tau, Tau);
                float ad = rel <= _Arc.y
                    ? -min(rel, _Arc.y - rel) * r
                    : min(rel - _Arc.y, Tau - rel) * r;
                return max(ring, ad);
            }

            float Glyph(float2 p, float2 h)
            {
                float2 local = float2((p.x + h.x) / (2 * h.x), 1 - (p.y + h.y) / (2 * h.y));
                float2 uv = _GlyphRect.xy + saturate(local) * _GlyphRect.zw;
                float s = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).r;
                float d = (0.5 - s) * 2 * _GlyphMap.x * _GlyphMap.y;
                return max(d, RoundedBox(p, h, 0));
            }

            float ShapeDistance(float2 p, float2 h, float kind, float radius)
            {
                if (kind > 5.5) return Glyph(p, h);
                if (kind < 0.5) return RoundedBox(p, h, radius);
                if (kind < 1.5) return length(p) - min(h.x, h.y);
                if (kind < 3.5) return Arc(p, h);           // 2 (EdgeFade) is handled separately
                if (kind < 4.5) return Triangle(p, h);
                return Diamond(p, h);
            }

            float4 Over(float4 top, float4 bottom) { return top + bottom * (1 - top.a); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 size = _SizePad.xy;
                float pad = _SizePad.z;
                float aa = max(_SizePad.w, 1e-3);
                float2 p = float2(input.uv.x - 0.5, 0.5 - input.uv.y) * size;  // logical px, y down, centred
                float2 h = max(size * 0.5 - pad, 0.5);
                float kind = _ShapeA.x;
                float clipMask = SAMPLE_TEXTURE2D(_GUIClipTexture, sampler_GUIClipTexture, input.clipUV).a * CircleMask(p);

                if (kind > 1.5 && kind < 2.5)
                {
                    // Edge fade (vignette): quadratic falloff from the rect border inward.
                    float inside = -RoundedBox(p, h, 0);
                    float u = saturate(inside / max(_Fade.x, 1e-3));
                    float k = 1 - u;
                    float a = inside < 0 ? 0 : _Fade.y * k * k * _Color.a;
                    return half4(_Color.rgb * a, a) * clipMask;
                }

                float radius = _ShapeA.y;
                float stroke = _ShapeA.z;
                float d = ShapeDistance(p, h, kind, radius);
                float body = stroke > 0 ? abs(d + stroke * 0.5) - stroke * 0.5 : d;
                float cover = saturate(0.5 - body / aa);

                // Gradient: 1 vertical (top→bottom), 2 horizontal (left→right), 3 radial (centre→edge).
                float4 fill = _Color;
                float g = _ShapeA.w;
                if (g > 0.5)
                {
                    float t = g < 1.5 ? (p.y / (2 * h.y) + 0.5)
                            : g < 2.5 ? (p.x / (2 * h.x) + 0.5)
                            : saturate(length(p / h));
                    fill = lerp(_Color, _Color2, saturate(t));
                }

                float4 result = float4(fill.rgb * fill.a, fill.a) * cover;

                // Glint: one diagonal highlight band across the filled body.
                if (_Glow.z >= 0)
                {
                    float s = (p.x + p.y * 0.6) / max(2 * h.x, 1) + 0.5;
                    float band = exp(-pow((s - lerp(-0.3, 1.3, _Glow.z)) / 0.09, 2));
                    float ga = band * _Glow.w * cover;
                    result = Over(float4(ga, ga, ga, ga) * float4(1, 0.97, 0.9, 1), result);
                }

                // Outer glow below the body.
                if (_Glow.x > 0 && _Glow.y > 0)
                {
                    float u = saturate(max(d, 0) / _Glow.x);
                    float ga = _Glow.y * (1 - u) * (1 - u) * (1 - cover) * _GlowColor.a;
                    result = Over(result, float4(_GlowColor.rgb * ga, ga));
                }

                // Soft drop shadow below everything.
                if (_Shadow.w > 0)
                {
                    float ds = ShapeDistance(p - _Shadow.xy, h, kind, radius);
                    float soft = max(_Shadow.z, aa);
                    float sa = _Shadow.w * (1 - smoothstep(-soft * 0.5, soft, ds));
                    result = Over(result, float4(0, 0, 0, sa));
                }

                return half4(result * clipMask);
            }
            ENDHLSL
        }
    }
}
