// 캠코더 화면 이상 두 가지를 한 패스에서 처리한다 (URP Full Screen Pass Renderer Feature가 매 프레임 그림).
// 1) 신호 불량 — 가로 띠 찢김, 색 분리, 정전기 줄무늬. 전역 _CamGlitch(0~1), CamcorderGlitch.cs
// 2) 렌즈 김서림 — 가장자리부터 뿌옇게 번지고 흐려짐, 가운데는 선명. 전역 _CamFog(0~1), LensFog.cs
Shader "Hidden/CaveGame/CamcorderGlitch"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "CamcorderGlitch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _CamGlitch;     // 0이면 원본 그대로
            float _CamGlitchSeed; // 바뀔 때마다 찢기는 위치가 새로 뽑힌다
            float _CamFog;

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // 부드러운 값 노이즈 — 김이 고르게 차지 않고 얼룩지게
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half3 Sample(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }

            half3 ApplyFog(half3 col, float2 uv, float fog)
            {
                // 화면비 보정한 중심 거리 — 가운데 원은 선명하게 남긴다
                float2 c = (uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float patch = ValueNoise(uv * float2(5.0, 3.0)) * 0.6 + ValueNoise(uv * 13.0) * 0.4;
                float mask = saturate(smoothstep(0.18, 0.75, length(c) + (patch - 0.5) * 0.35) * fog * 1.4);
                if (mask <= 0.001) return col;

                // 김 = 빛이 퍼짐: 주변을 섞어 흐리게 + 어두운 곳이 우윳빛으로 뜬다
                float r = 0.035 * mask;
                half3 blur = (Sample(uv + float2(r, 0)) + Sample(uv - float2(r, 0)) +
                              Sample(uv + float2(0, r)) + Sample(uv - float2(0, r)) +
                              Sample(uv + float2(r, r) * 0.7) + Sample(uv - float2(r, r) * 0.7) +
                              Sample(uv + float2(r, -r) * 0.7) + Sample(uv - float2(r, -r) * 0.7)) * 0.125;
                // 어두운 동굴이라 흐림만으론 안 보인다 — 손전등 빛이 물방울에 번진 듯 푸르스름한 회색으로 띄운다
                half3 veiled = blur * 0.6 + half3(0.13, 0.14, 0.15);
                return lerp(col, veiled, mask);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float g = saturate(_CamGlitch);
                float fog = saturate(_CamFog);
                if (g <= 0.001)
                {
                    half3 clean = Sample(uv);
                    return half4(fog > 0.001 ? ApplyFog(clean, uv, fog) : clean, 1);
                }

                float seed = _CamGlitchSeed;

                // 굵은 가로 띠 몇 개가 옆으로 밀린다
                float band = floor(uv.y * 22.0);
                float bandRnd = Hash(float2(band, seed));
                bool torn = bandRnd > 1.0 - 0.4 * g;
                float shift = torn ? (Hash(float2(band, seed + 1.7)) - 0.5) * 0.14 * g : 0.0;

                // 가는 주사선 떨림
                float row = floor(uv.y * 270.0);
                shift += (Hash(float2(row, seed + 3.1)) - 0.5) * 0.008 * g;
                uv.x = frac(uv.x + shift);

                // 밀린 띠는 색이 갈라진다
                float split = (torn ? 0.018 : 0.004) * g;
                half3 col;
                col.r = Sample(uv + float2(split, 0)).r;
                col.g = Sample(uv).g;
                col.b = Sample(uv - float2(split, 0)).b;

                // 정전기: 전체에 옅은 알갱이(더하기 — 어두운 동굴이 회색으로 뜨지 않게) + 일부 줄만 하얗게 번쩍
                float grain = Hash(float2(floor(uv.x * 480.0), row) + seed * 7.13);
                col += (grain - 0.5) * 0.06 * g;
                float staticRow = step(1.0 - 0.05 * g, Hash(float2(row, seed + 5.9)));
                col = lerp(col, grain.xxx * 0.8, staticRow * 0.6);

                // 찢긴 띠는 살짝 어두워진다 (신호 약화)
                col *= torn ? 1.0 - 0.35 * g * Hash(float2(band, seed + 9.3)) : 1.0;

                // 김은 렌즈 위에 있으니 신호가 깨져도 같은 자리에 남는다 (원래 uv 기준)
                if (fog > 0.001) col = ApplyFog(col, input.texcoord, fog);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
