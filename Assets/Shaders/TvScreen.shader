// 타이틀 화면 브라운관. _Power 0 = 꺼진 화면(검은 유리), 1 = 켜져서 지지직거리는 정전기.
// 켜지는 순간(_Power 0→1)은 브라운관답게 가운데 가로줄 하나가 먼저 번쩍이고 위아래로 벌어진다.
Shader "CaveGame/TvScreen"
{
    Properties
    {
        _Power ("Power", Range(0, 1)) = 0
        _Brightness ("Brightness", Float) = 1.6
        _Tint ("Tint", Color) = (0.85, 0.92, 1, 1)
        _Resolution ("Noise Resolution", Vector) = (220, 160, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Power;
                float _Brightness;
                half4 _Tint;
                float4 _Resolution;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            // sin 기반 해시는 큰 입력에서 사선 무늬가 생긴다 — 곱셈 기반(Dave Hoskins hash12)으로
            float Hash(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

                // 켜짐: 앞 30%는 가운데 가로줄이 번쩍, 나머지 동안 위아래로 벌어진다
                float open = saturate((_Power - 0.3) / 0.7);
                float halfHeight = lerp(0.004, 0.5, open * open);
                float dy = abs(uv.y - 0.5);
                float inside = 1.0 - smoothstep(halfHeight, halfHeight + 0.01, dy);
                float flashLine = saturate(_Power / 0.3) * (1.0 - open);

                // 정전기: 프레임마다(초당 30번) 새로 뽑는 알갱이
                float frame = floor(t * 30.0);
                float2 cell = floor(uv * _Resolution.xy);
                float n = Hash(cell + float2(frame * 113.0, frame * 71.0));

                // 천천히 아래로 흐르는 밝은 띠 + 주사선
                float roll = smoothstep(0.0, 0.15, frac(uv.y * 0.8 - t * 0.35)) * smoothstep(0.35, 0.15, frac(uv.y * 0.8 - t * 0.35));
                float scan = 0.8 + 0.2 * sin(uv.y * _Resolution.y * 3.14159);
                float lum = (n * 0.85 + roll * 0.25) * scan;

                // 가장자리는 브라운관처럼 어둡게
                float2 c = uv - 0.5;
                float vignette = saturate(1.0 - dot(c, c) * 2.2);

                half3 col = _Tint.rgb * lum * vignette * _Brightness * open * inside;
                col += _Tint.rgb * flashLine * inside * 3.0;

                // 꺼진 유리: 위쪽이 살짝 밝은 회색 + 왼쪽 위 흐린 반사광 — 완전한 검정이면 구멍처럼 보인다
                float2 hl = (uv - float2(0.25, 0.78)) * float2(1.0, 1.6);
                half3 glass = lerp(0.012, 0.035, uv.y) + 0.05 * saturate(1.0 - length(hl) * 3.0);
                col += glass * (1.0 - open);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
