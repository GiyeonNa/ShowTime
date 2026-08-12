// M4: 탄환 셰이더 — 가산 블렌드 글로우 쿼드, GPU Instancing 지원.
// 텍스처 없이 UV 거리 기반 원형 감쇠로 탄환 모양을 절차 생성한다 (외부 에셋 0 원칙).
Shader "ShowTime/Bullet"
{
    Properties
    {
        [HDR][MainColor] _BaseColor("Color", Color) = (0.6, 1.8, 2.4, 1)
        _Softness("Edge Softness", Range(1, 6)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Bullet"
            Blend One One            // 가산 — 겹칠수록 밝아지는 탄막 글로우
            ZWrite Off               // 반투명 정렬 문제 회피 (가산은 순서 무관)
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing   // GPU Instancing 변형 생성 (드로우콜 1회의 전제)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // 인스턴스별 행렬 인덱스
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Softness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN); // 이 매크로가 unity_ObjectToWorld를 인스턴스 것으로 바꾼다
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 중심 거리 기반 원형 글로우 (0.5 = 쿼드 가장자리에서 소멸)
                half d = saturate(1.0 - length(IN.uv - 0.5) * 2.0);
                half glow = pow(d, _Softness);
                return half4(_BaseColor.rgb * glow, 1);
            }
            ENDHLSL
        }
    }
}
