Shader "Hidden/FXAA" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;

        struct VertexData {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vp(VertexData v) {
            v2f f;
            f.pos = UnityObjectToClipPos(v.vertex);
            f.uv = v.uv;

            return f;
        }
    ENDCG

    SubShader {

        Cull Off ZWrite Off ZTest Always

        Pass { // 0 LUMINANCE_PASS
            CGPROGRAM
            #pragma vertex vp
            #pragma fragment fp


            float4 fp(v2f f) : SV_Target {
                float4 sample = tex2D(_MainTex, f.uv);
                sample.a = LinearRgbToLuminance(saturate(sample.rgb));
                return sample;
            }
            ENDCG
        }

        Pass {
            CGPROGRAM
            #pragma vertex vp
            #pragma fragment fp

            #pragma multi_compile _ LUMINANCE_GREEN

            float _ContrastThreshold;
            float _RelativeThreshold;
            float _SubpixelBlending;

            struct LuminanceData {
                float m, n, e, s, w;
                float ne, nw, se, sw;
                float highest, lowest, contrast;
            };

            struct EdgeData {
                bool isHorizontal;
                float pixelStep;
                float oppositeLuminance, gradient;
            };

            float4 Sample(float2 uv) {
                return tex2Dlod(_MainTex, float4(uv, 0, 0));
            }

            float SampleLuminance(float2 uv) {
                #ifdef LUMINANCE_GREEN
                    return Sample(uv).g;
                #else
                    return Sample(uv).a;
                #endif
            }

            float SampleLuminance(float2 uv, float uOffset, float vOffset) {
                uv += _MainTex_TexelSize * float2(uOffset, vOffset);
                return SampleLuminance(uv);
            }

            LuminanceData SampleLuminanceNeighbors(float2 uv) {
                LuminanceData l;
                l.m = SampleLuminance(uv);
                l.n = SampleLuminance(uv, 0, 1);
                l.e = SampleLuminance(uv, 1, 0);
                l.s = SampleLuminance(uv, 0, -1);
                l.w = SampleLuminance(uv, -1, 0);

                l.highest = max(max(max(max(l.n, l.e), l.s), l.w), l.m);
                l.lowest = min(min(min(min(l.n, l.e), l.s), l.w), l.m);
                l.contrast = l.highest - l.lowest;

                return l;
            }

            void SampleLuminanceCorners(inout LuminanceData l, float2 uv) {
                l.ne = SampleLuminance(uv, 1, 1);
                l.nw = SampleLuminance(uv, -1, 1);
                l.se = SampleLuminance(uv, 1, -1);
                l.sw = SampleLuminance(uv, -1, -1);
            }

            bool SkipPixel(LuminanceData l) {
                float threshold = max(_ContrastThreshold, _RelativeThreshold * l.highest);
                return l.contrast < threshold;
            }

            float BlendFactor(LuminanceData l) {
                float filter = 2 * (l.n + l.e + l.s + l.w);
                filter += l.ne + l.nw + l.se + l.sw;
                filter *= 1.0f / 12;
                filter = abs(filter - l.m);
                filter = saturate(filter / l.contrast);

                float blendFactor = smoothstep(0, 1, filter);
                return blendFactor * blendFactor * _SubpixelBlending;
            }

            EdgeData DetermineEdge (LuminanceData l) {
                EdgeData e;
                float horizontal = abs(l.n + l.s - 2 * l.m) * 2 +
                                   abs(l.ne + l.se - 2 * l.e) +
                                   abs(l.nw + l.sw - 2 * l.w);

                float vertical = abs(l.e + l.w - 2 * l.m) * 2 +
                                 abs(l.ne + l.nw - 2 * l.n) +
                                 abs(l.se + l.sw - 2 * l.s);

                e.isHorizontal = horizontal >= vertical;
                float pLuminance = e.isHorizontal ? l.n : l.e;
                float nLuminance = e.isHorizontal ? l.s : l.w;
                float pGradient = abs(pLuminance - l.m);
                float nGradient = abs(nLuminance - l.m);

                e.pixelStep = e.isHorizontal ? _MainTex_TexelSize.y : _MainTex_TexelSize.x;
                if (pGradient < nGradient) {
                    e.pixelStep = -e.pixelStep;
                    e.oppositeLuminance = nLuminance;
                    e.gradient = nGradient;
                } else {
                    e.oppositeLuminance = pLuminance;
                    e.gradient = pGradient;
                }
                
                return e;
            }

            float DetermineEdgeBlendFactor(LuminanceData l, EdgeData e, float2 uv) {
                int i;
                float2 uvEdge = uv;
                float2 edgeStep;
                if (e.isHorizontal) {
                    uvEdge.y += e.pixelStep * 0.5f;
                    edgeStep = float2(_MainTex_TexelSize.x, 0);
                } else {
                    uvEdge.x += e.pixelStep * 0.5f;
                    edgeStep = float2(0, _MainTex_TexelSize.y);
                }

                float edgeLuminance = (l.m + e.oppositeLuminance) * 0.5f;
                float gradientThreshold = e.gradient * 0.25f;
                float2 puv = uvEdge + edgeStep;
                float pLuminanceDelta = SampleLuminance(puv) - edgeLuminance;
                bool pAtEnd = abs(pLuminanceDelta) >= gradientThreshold;

                UNITY_UNROLL
                for (i = 0; i < 9 && !pAtEnd; ++i) {
                    puv += edgeStep;
                    pLuminanceDelta = SampleLuminance(puv) - edgeLuminance;
                    pAtEnd = abs(pLuminanceDelta) >= gradientThreshold;
                }

                if (!pAtEnd) puv += edgeStep;

                float2 nuv = uvEdge - edgeStep;
                float nLuminanceDelta = SampleLuminance(nuv) - edgeLuminance;
                bool nAtEnd = abs(nLuminanceDelta) >= gradientThreshold;

                UNITY_UNROLL
                for (i = 0; i < 9 && !nAtEnd; ++i) {
                    nuv -= edgeStep;
                    nLuminanceDelta = SampleLuminance(nuv) - edgeLuminance;
                    nAtEnd = abs(nLuminanceDelta) >= gradientThreshold;
                }

                if (!nAtEnd) nuv -= edgeStep;

                float pDistance, nDistance;
                if (e.isHorizontal) {
                    pDistance = puv.x - uv.x;
                    nDistance = uv.x - nuv.x;
                } else {
                    pDistance = puv.y - uv.y;
                    nDistance = uv.y - nuv.y;
                }

                float shortestDistance;
                bool deltaSign;
                if (pDistance <= nDistance) {
                    shortestDistance = pDistance;
                    deltaSign = pLuminanceDelta >= 0;
                } else {
                    shortestDistance = nDistance;
                    deltaSign = nLuminanceDelta >= 0;
                }

                if (deltaSign == (l.m - edgeLuminance >= 0)) return 0;
                return 0.5 - shortestDistance / (pDistance + nDistance);
            }

            float4 fp(v2f f) : SV_Target {
                float2 uv = f.uv;
                LuminanceData luminance = SampleLuminanceNeighbors(uv);

                if (SkipPixel(luminance)) return Sample(uv);

                SampleLuminanceCorners(luminance, uv);
                float pixelBlend = BlendFactor(luminance);
                EdgeData e = DetermineEdge(luminance);
                float edgeBlend = DetermineEdgeBlendFactor(luminance, e, uv);
                float finalBlend = max(pixelBlend, edgeBlend);

                if (e.isHorizontal) {
                    uv.y += e.pixelStep * finalBlend;
                } else {
                    uv.x += e.pixelStep * finalBlend;
                }

                return float4(Sample(uv).rgb, luminance.m);
            }
            ENDCG
        }
    }
}
