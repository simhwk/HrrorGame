// 바닥 메모 테두리의 은은한 빛. 메모 밑에 까는 평평한 사각형(NoteGlow.cs가 만듦)에 그린다.
// 메모 사각형 바깥으로 거리에 따라 옅어지는 띠 — 안쪽은 메모에 가려 안 보이니 신경 쓰지 않는다. 더하기 합성이라 어둠 속에서 떠 보인다.
Shader "CaveGame/NoteGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.22, 0.18, 0.12, 1)
        _Falloff ("Falloff (m)", Float) = 0.022
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "NoteGlow"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Falloff;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            // 메모마다 다른 값 — MaterialPropertyBlock으로 넣는다
            float4 _GlowRect; // xy = 사각형(quad) 크기 (m), zw = 메모 크기 (m)
            float _Phase;     // 메모마다 맥박이 어긋나게

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * _GlowRect.xy;
                float2 q = abs(p) - _GlowRect.zw * 0.5;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0); // 메모 가장자리까지 거리, 바깥이 +
                float margin = min(_GlowRect.x - _GlowRect.z, _GlowRect.y - _GlowRect.w) * 0.5;
                float glow = exp(-max(d, 0.0) / _Falloff) * (1.0 - smoothstep(margin * 0.7, margin, d));
                glow *= smoothstep(-0.004, 0.004, d); // 메모 안쪽은 비우고 테두리 바깥만
                float pulse = 1.0 - _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed + _Phase));
                return half4(_Color.rgb * glow * pulse, 1);
            }
            ENDHLSL
        }
    }
}
