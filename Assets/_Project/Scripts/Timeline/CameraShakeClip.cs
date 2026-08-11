using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>카메라 셰이크 클립. Perlin 기반이라 결정적 — 스크럽 안전 (CameraShakeMixerBehaviour 참조).</summary>
    [Serializable]
    public sealed class CameraShakeClip : PlayableAsset, ITimelineClipAsset
    {
        public CameraShakeBehaviour template = new CameraShakeBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<CameraShakeBehaviour>.Create(graph, template);
    }

    [Serializable]
    public sealed class CameraShakeBehaviour : PlayableBehaviour
    {
        [Min(0f)] public float amplitude = 0.12f;  // 흔들림 크기 (월드 단위)
        [Min(0.1f)] public float frequency = 22f;  // 초당 진동 수
        public float seed = 7.13f;                 // 축별 노이즈 분리용
    }
}
