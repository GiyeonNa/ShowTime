// M1-①②: 몹 전투 셰이더 — 히트 플래시 + 디졸브 통합.
// 실전에서도 캐릭터 셰이더 하나에 전투 연출 기능들을 통합하는 게 일반적이다 (머티리얼/배칭 관리 단순화).
//
// [디졸브 원리 요약]
//  1) 노이즈 텍스처의 밝기값 = 픽셀별 "사라지는 순서표"
//  2) clip(noise - _DissolveAmount): 임계값보다 어두운 픽셀부터 버린다(알파 테스트)
//  3) 막 사라지려는 경계 픽셀에 HDR 발광색을 입히면 "타들어가는" 느낌이 완성된다
Shader "ShowTime/Mob_Combat"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        // --- 히트 플래시 ---
        [HDR] _FlashColor("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount("Flash Amount", Range(0, 1)) = 0

        // --- 디졸브 ---
        _NoiseTex("Dissolve Noise", 2D) = "white" {}
        // xy = UV 오프셋, z = UV 스케일 — 인스턴스별로 MPB가 랜덤 부여 (패턴 변주)
        _NoiseVariation("Noise Variation (xy=offset, z=scale)", Vector) = (0, 0, 1, 0)
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        [HDR] _EdgeColor("Dissolve Edge Color", Color) = (1, 0.55, 0.12, 1) // 불꽃 주황
        _EdgeWidth("Dissolve Edge Width", Range(0.001, 0.2)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 텍스처/샘플러는 CBUFFER 밖에 선언한다 (리소스 바인딩이라 상수버퍼 대상이 아님)
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0; // 노이즈 샘플링용 UV (쿼드는 0~1 전개)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            // SRP Batcher 호환: 머티리얼 프로퍼티는 전부 UnityPerMaterial CBUFFER 안에
            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _FlashColor;
                half   _FlashAmount;
                float4 _NoiseTex_ST;     // 타일링/오프셋 (TRANSFORM_TEX가 사용)
                float4 _NoiseVariation;  // 인스턴스별 패턴 변주 (xy=offset, z=scale)
                half   _DissolveAmount;
                half4  _EdgeColor;
                half   _EdgeWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _NoiseTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1) 노이즈 값 읽기 — 텍스처를 색이 아니라 "데이터(순서표)"로 쓴다.
                //    인스턴스별 오프셋/스케일을 더해 같은 텍스처에서 다른 순서표를 읽는다.
                //    (노이즈가 seamless 반복이라 오프셋해도 이음매가 안 보인다 — Repeat 랩모드 전제)
                float2 noiseUV = IN.uv * _NoiseVariation.z + _NoiseVariation.xy;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                // 2) 알파 테스트: 임계값 아래 픽셀은 폐기.
                //    clip(x)는 x < 0이면 해당 픽셀 렌더링을 중단한다.
                //    (면접 포인트: 알파 "블렌딩"과 달리 정렬 문제가 없지만,
                //     모바일 TBDR GPU에서는 Early-Z를 깨서 비용이 든다 — 필요한 곳에만 쓸 것)
                half dist = noise - _DissolveAmount;
                clip(dist);

                // 3) 히트 플래시: 기본색 ↔ 플래시색 보간
                half4 color = lerp(_BaseColor, _FlashColor, _FlashAmount);

                // 4) 디졸브 경계 발광: 막 타들어가는 중인(임계값에 근접한) 픽셀에 엣지색.
                //    step(a, b) = b >= a ? 1 : 0. 분기 대신 step 곱으로 처리 (GPU 친화).
                half edge = step(dist, _EdgeWidth)            // 경계 폭 안쪽인가
                          * step(0.001, _DissolveAmount);     // 디졸브 진행 중일 때만
                color = lerp(color, _EdgeColor, edge);

                return color;
            }
            ENDHLSL
        }
    }
}
