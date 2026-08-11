// M1-①②③: 몹 전투 셰이더 — 히트 플래시 + 디졸브 + 아웃라인 통합.
// 실전에서도 캐릭터 셰이더 하나에 전투 연출 기능들을 통합하는 게 일반적이다 (머티리얼/배칭 관리 단순화).
//
// [디졸브 원리 요약]
//  1) 노이즈 텍스처의 밝기값 = 픽셀별 "사라지는 순서표"
//  2) clip(noise - _DissolveAmount): 임계값보다 어두운 픽셀부터 버린다(알파 테스트)
//  3) 막 사라지려는 경계 픽셀에 HDR 발광색을 입히면 "타들어가는" 느낌이 완성된다
//
// [아웃라인 원리 요약]
//  1) "실루엣 알파" = 스프라이트 텍스처의 A 채널. 단, UV 0~1 밖은 알파 0으로 간주한다
//     → 그레이박스(민무늬 white)에서는 쿼드 테두리가, 스프라이트에서는 그림 실루엣이 경계가 된다 (같은 코드)
//  2) 내 픽셀은 불투명한데 _OutlineWidth 반경의 이웃 중 하나라도 투명하면 = 경계 픽셀 → 아웃라인색
Shader "ShowTime/Mob_Combat"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        // 스프라이트 (A = 실루엣). 그레이박스 단계에선 기본값 white → 쿼드 전체가 불투명
        [MainTexture] _MainTex("Sprite (A = Silhouette)", 2D) = "white" {}

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

        // --- 아웃라인 ---
        [HDR] _OutlineColor("Outline Color", Color) = (1, 0.85, 0.25, 1) // 금색 강조
        _OutlineWidth("Outline Width (UV)", Range(0.001, 0.1)) = 0.025
        _OutlineAmount("Outline Amount", Range(0, 1)) = 0
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
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            // 아웃라인 이웃 샘플 방향 8개 (상하좌우 + 대각)
            static const float2 kOutlineDirs[8] =
            {
                float2( 1,  0), float2(-1,  0), float2( 0,  1), float2( 0, -1),
                float2( 0.707,  0.707), float2(-0.707,  0.707),
                float2( 0.707, -0.707), float2(-0.707, -0.707)
            };

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
                half4  _OutlineColor;
                half   _OutlineWidth;
                half   _OutlineAmount;
            CBUFFER_END

            // 실루엣 알파: UV 0~1 밖은 0으로 간주 (분기 대신 step 곱 — GPU 친화)
            // LOD 0 명시 샘플링: 동적 분기([branch]) 안에서는 암시적 미분(LOD 자동 계산)이 금지라서
            half SilhouetteAlpha(float2 uv)
            {
                half inside = step(0.0, uv.x) * step(uv.x, 1.0)
                            * step(0.0, uv.y) * step(uv.y, 1.0);
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a * inside;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _NoiseTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 0) 스프라이트 실루엣: 알파 0.5 미만 픽셀 폐기.
                //    그레이박스(white 텍스처)에선 전부 통과 — 스프라이트 도입 시 자동으로 실루엣이 된다.
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(sprite.a - 0.5);

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

                // 3) 히트 플래시: 기본색 ↔ 플래시색 보간 (스프라이트 색은 기본색에 곱)
                half4 color = lerp(_BaseColor * sprite, _FlashColor, _FlashAmount);

                // 4) 디졸브 경계 발광: 막 타들어가는 중인(임계값에 근접한) 픽셀에 엣지색.
                //    step(a, b) = b >= a ? 1 : 0. 분기 대신 step 곱으로 처리 (GPU 친화).
                half edge = step(dist, _EdgeWidth)            // 경계 폭 안쪽인가
                          * step(0.001, _DissolveAmount);     // 디졸브 진행 중일 때만
                color = lerp(color, _EdgeColor, edge);

                // 5) 아웃라인: 이웃 8방향 중 실루엣이 빈 곳이 있으면 경계 픽셀.
                //    이웃 샘플 8회가 비용 — 모바일 저사양이면 4방향(상하좌우)으로 줄이는 선택지 (문서 기록)
                //    _OutlineAmount가 0이면 branch로 샘플 자체를 건너뛴다 (평상시 비용 0에 수렴)
                half outline = 0;
                [branch] if (_OutlineAmount > 0.001)
                {
                    half minNeighbor = 1.0;
                    [unroll] for (int i = 0; i < 8; i++)
                        minNeighbor = min(minNeighbor,
                            SilhouetteAlpha(IN.uv + kOutlineDirs[i] * _OutlineWidth));
                    outline = (1.0 - step(0.5, minNeighbor)) * _OutlineAmount;
                }
                color = lerp(color, _OutlineColor, outline);

                return color;
            }
            ENDHLSL
        }
    }
}
