// M3-②: 전체 화면 충격파 물결 — M1 ScreenDistortion(오브젝트 방식)의 링 수학을
// 화면 공간으로 옮긴 패스 방식 버전. 종횡비 보정으로 링이 타원이 아닌 원이 되게 한다.
Shader "Hidden/ShowTime/SkillRipple"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SkillRipple"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // URP Core.hlsl이 TEXTURE2D_X 등 플랫폼 매크로를 세팅한 뒤 Blit.hlsl을 넣어야 한다
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half _Progress;      // 0=중심 → 1=화면 끝 (1이면 피처가 패스를 넣지 않음)
            float4 _Center;      // xy = 뷰포트 좌표 (패스가 월드→뷰포트 변환해서 공급)

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // 종횡비 보정 거리 (보정 없으면 가로 화면에서 링이 타원으로 퍼진다)
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = uv - _Center.xy;
                d.x *= aspect;
                float r = length(d);

                // 링 마스크 + 에너지 소산 (M1과 같은 수학 — 좌표계만 화면 공간)
                float ring = 1.0 - saturate(abs(r - _Progress * 0.9) / 0.12);
                ring *= ring;
                float fade = 1.0 - _Progress;

                float2 dir = d / max(r, 1e-4);
                dir.x /= aspect; // UV 공간으로 복귀
                float2 offset = dir * (ring * fade * 0.035);

                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset).rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
