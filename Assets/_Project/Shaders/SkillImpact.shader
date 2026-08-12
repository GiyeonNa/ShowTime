// M3-①: 스킬 발동 화면 연출 — 비네트 + 색조(탈색·한색 틴트) 전체 화면 블릿.
// Blit.hlsl의 Vert(풀스크린 삼각형)와 _BlitTexture를 쓰는 URP 블릿 셰이더 정석 형태.
Shader "Hidden/ShowTime/SkillImpact"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SkillImpact"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // URP Core.hlsl이 TEXTURE2D_X 등 플랫폼 매크로를 세팅한 뒤 Blit.hlsl을 넣어야 한다
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half _Intensity;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // 색조: 탈색 + 차가운 틴트 — "시간이 멈춘 듯한" 스킬 발동 문법
                half gray = dot(color, half3(0.299, 0.587, 0.114));
                half3 graded = lerp(color, gray * half3(0.72, 0.80, 1.08), 0.65 * _Intensity);

                // 비네트: 중심 거리 제곱 기반 — smoothstep으로 가장자리만 부드럽게 어둡게
                float2 fromCenter = uv - 0.5;
                half vignette = 1.0 - _Intensity * 0.85 * smoothstep(0.15, 0.85, dot(fromCenter, fromCenter) * 2.4);

                return half4(graded * vignette, 1);
            }
            ENDHLSL
        }
    }
}
