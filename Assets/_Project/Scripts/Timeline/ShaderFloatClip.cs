using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>지정한 셰이더 float 프로퍼티를 클립 구간 동안 from→to로 보간.</summary>
    [Serializable]
    public sealed class ShaderFloatClip : PlayableAsset, ITimelineClipAsset
    {
        public ShaderFloatBehaviour template = new ShaderFloatBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<ShaderFloatBehaviour>.Create(graph, template);
    }

    [Serializable]
    public sealed class ShaderFloatBehaviour : PlayableBehaviour
    {
        public string propertyName = "_Progress";
        public float from;
        public float to = 1f;

        int _propertyId = -1;

        public int PropertyId
        {
            get
            {
                if (_propertyId < 0) _propertyId = Shader.PropertyToID(propertyName);
                return _propertyId;
            }
        }
    }
}
