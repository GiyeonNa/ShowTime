using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    // 파일명 = 클래스명 필수: ScriptableObject 파생(클립 에셋)은 파일명이 다르면
    // 도메인 리로드 후 "No script asset" — 직렬화 복원이 깨진다 (M2 교훈)
    //
    // 설계: 클립은 무상태(stateless) — 누적 변수 없이 클립 로컬 시간만으로 값을 계산한다.
    // 그래야 스크럽해도, 중간부터 재생해도 항상 같은 그림이 나온다.

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
}
