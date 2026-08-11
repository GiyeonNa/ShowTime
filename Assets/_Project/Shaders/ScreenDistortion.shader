// M1-④: 화면 왜곡 (충격파) — 씬 컬러를 "밀린 UV"로 재샘플링하는 그랩 방식 셰이더.
//
// [원리 요약]
//  1) URP가 불투명 패스 직후 화면을 _CameraOpaqueTexture로 복사해 둔다 (RP 에셋 Opaque Texture 필요)
//  2) 반투명 큐의 쿼드가 자기 뒤 화면을 어긋난 UV로 다시 그린다 → 유리/열기처럼 왜곡되어 보인다
//  3) _Progress로 링 반경을 키우면 충격파. 링 밖 픽셀은 clip으로 아예 버려서 재샘플링 비용을 한정한다
//
// M3에서 같은 왜곡을 RenderGraph 전체 화면 패스로 확장 예정 (이 셰이더는 "오브젝트 단위" 방식의 대조군)
Shader "ShowTime/ScreenDistortion"
{
    Properties
    {
        // 기본값 1 = fade가 0이라 전 픽셀 clip → 재생 중이 아니면 아무것도 안 그린다
        _Progress("Progress (0=중심, 1=가장자리 도달)", Range(0, 1)) = 1
        _RingWidth("Ring Width", Range(0.01, 0.5)) = 0.16
        _Strength("Distortion Strength", Range(0, 0.2)) = 0.055
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Shockwave"
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // SampleSceneColor(_CameraOpaqueTexture) 제공
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half _Progress;
                half _RingWidth;
                half _Strength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 쿼드 로컬 반지름: 중심 0 → 가장자리 1
                float2 fromCenter = IN.uv - 0.5;
                float r = length(fromCenter) * 2.0;

                // 링 마스크: 반경이 _Progress인 링 근처만 1, 멀어질수록 0
                float ring = 1.0 - saturate(abs(r - _Progress) / _RingWidth);
                ring *= ring;                 // 제곱 감쇠 — 링 한가운데만 강하게
                float fade = 1.0 - _Progress; // 퍼질수록 약해진다 (에너지 소산)

                // 링 밖 픽셀은 그리지 않는다 — 화면 재샘플링(대역폭) 비용을 링 영역으로 한정.
                // (M1-② 교훈 재사용: clip은 필요한 곳에만. 여기선 쿼드 대부분을 버리는 게 이득)
                clip(ring * fade - 0.005);

                // 바깥 방향으로 "압축된" 화면을 보여준다 (안쪽 픽셀을 끌어와 샘플링)
                float2 dir = fromCenter / max(length(fromCenter), 1e-4);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                half3 scene = SampleSceneColor(screenUV - dir * (ring * fade * _Strength));
                return half4(scene, 1);
            }
            ENDHLSL
        }
    }
}
