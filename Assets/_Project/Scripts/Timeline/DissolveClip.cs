using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>디졸브: 몹별로 stagger만큼 어긋나게 소멸(또는 역재생 = 재등장). 무상태.</summary>
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
}
