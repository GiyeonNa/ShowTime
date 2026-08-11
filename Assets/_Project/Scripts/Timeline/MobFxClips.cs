using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    // M2-①: 몹 연출 클립 3종.
    //
    // 설계 원칙: 클립은 전부 "무상태(stateless)" — 누적 변수 없이 클립 로컬 시간만으로 값을 계산한다.
    // 그래야 타임라인을 앞뒤로 스크럽해도, 중간부터 재생해도 항상 같은 그림이 나온다.
    // (M1ShaderDemo는 누적 타이머 방식이라 스크럽이 불가능했다 — 이것이 Timeline 이관의 실질 이득)

    /// <summary>평타 스윕: 몹 i를 시각 i×interval에 때리고, 각자 decay 속도로 감쇠.</summary>
    [Serializable]
    public sealed class FlashSweepClip : PlayableAsset, ITimelineClipAsset
    {
        public FlashSweepBehaviour template = new FlashSweepBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<FlashSweepBehaviour>.Create(graph, template);
    }

    [Serializable]
    public sealed class FlashSweepBehaviour : PlayableBehaviour
    {
        [Min(0.01f)] public float interval = 0.12f;   // 타격 간격
        [Min(0.1f)] public float decayPerSecond = 4f; // 플래시 감쇠 속도

        public void Apply(MobGroup group, double clipTime, float weight)
        {
            int count = group.Count;
            for (int i = 0; i < count; i++)
            {
                float hitTime = i * interval;
                float amount = clipTime < hitTime
                    ? 0f
                    : 1f - (float)(clipTime - hitTime) * decayPerSecond;
                group.SetFlash(i, Mathf.Max(0f, amount) * weight);
            }
        }
    }

    /// <summary>디졸브: 몹별로 stagger만큼 어긋나게 소멸(또는 역재생 = 재등장).</summary>
    [Serializable]
    public sealed class DissolveClip : PlayableAsset, ITimelineClipAsset
    {
        public DissolveBehaviour template = new DissolveBehaviour();

        // 디졸브는 알파 테스트(clip) 기반이라 가중치 블렌드가 시각적으로 무의미 → 블렌딩 미지원 선언
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<DissolveBehaviour>.Create(graph, template);
    }

    [Serializable]
    public sealed class DissolveBehaviour : PlayableBehaviour
    {
        [Min(0.05f)] public float perMobDuration = 1.1f; // 몹 1기의 소멸 시간
        [Min(0f)] public float stagger = 0.04f;          // 몹별 시작 지연 — 물결처럼 번지게
        public bool reverse;                             // true = 재등장(1→0)

        public void Apply(MobGroup group, double clipTime)
        {
            int count = group.Count;
            for (int i = 0; i < count; i++)
            {
                float local = Mathf.Clamp01((float)(clipTime - i * stagger) / perMobDuration);
                group.SetDissolve(i, reverse ? 1f - local : local);
            }
        }
    }

    /// <summary>아웃라인 강조: 세기는 클립 ease-in/out 가중치를 그대로 탄다 (믹서 참조).</summary>
    [Serializable]
    public sealed class OutlineClip : PlayableAsset, ITimelineClipAsset
    {
        public OutlineBehaviour template = new OutlineBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<OutlineBehaviour>.Create(graph, template);
    }

    [Serializable]
    public sealed class OutlineBehaviour : PlayableBehaviour
    {
        [Range(0f, 1f)] public float amount = 1f;
    }
}
