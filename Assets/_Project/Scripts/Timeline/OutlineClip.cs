using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>아웃라인 강조: 세기는 클립 ease-in/out 가중치를 그대로 탄다 (MobFxMixerBehaviour 참조).</summary>
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
