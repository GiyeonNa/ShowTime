using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// M3: 화면 연출 패스들의 구동 값. Timeline의 내장 AnimationTrack이 이 필드들을 애니메이션한다
    /// (커스텀 트랙과 내장 트랙의 혼용 — 렌더러가 없는 대상은 표준 애니메이션 바인딩이 정석).
    /// RenderFeature는 매 프레임 Current를 읽고, 값이 0이면 패스를 큐에 넣지 않는다 = 평상시 비용 0.
    /// </summary>
    public sealed class SkillImpactDriver : MonoBehaviour
    {
        public static SkillImpactDriver Current { get; private set; }

        [Range(0f, 1f)] public float intensity;          // 비네트/색조 세기 (0 = 패스 꺼짐)
        [Range(0f, 1f)] public float rippleProgress = 1f; // 전체 화면 물결 진행도 (1 = 퍼짐 완료 = 꺼짐)
        public Vector3 rippleWorldCenter;                 // 물결 중심 (월드) — 패스가 뷰포트로 변환

        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }
    }
}
