using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>
    /// M2-②: 범용 셰이더 float 파라미터 트랙. Renderer에 바인딩하고,
    /// 클립 구간 동안 지정한 프로퍼티를 from→to로 몰고 간다 (MPB — 머티리얼 에셋 비파괴).
    /// 데모에서는 충격파 쿼드의 _Progress를 구동하지만, 이름만 바꾸면 어떤 float에도 쓸 수 있다.
    /// </summary>
    [TrackColor(0.3f, 0.75f, 0.95f)]
    [TrackBindingType(typeof(Renderer))]
    [TrackClipType(typeof(ShaderFloatClip))]
    public sealed class ShaderFloatTrack : TrackAsset
    {
        [Tooltip("클립이 하나도 활성이 아닐 때 렌더러를 끈다 (충격파처럼 '재생 중에만 존재'하는 오브젝트용)")]
        public bool controlRendererEnabled;

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<ShaderFloatMixerBehaviour>.Create(graph, inputCount);
            mixer.GetBehaviour().controlRendererEnabled = controlRendererEnabled;
            return mixer;
        }
    }

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

    public sealed class ShaderFloatMixerBehaviour : PlayableBehaviour
    {
        public bool controlRendererEnabled;

        MaterialPropertyBlock _mpb;
        Renderer _lastRenderer;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var renderer = playerData as Renderer;
            if (renderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _lastRenderer = renderer;

            bool anyActive = false;
            int inputs = playable.GetInputCount();
            for (int i = 0; i < inputs; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;
                anyActive = true;

                var sp = (ScriptPlayable<ShaderFloatBehaviour>)playable.GetInput(i);
                var b = sp.GetBehaviour();
                float t = Mathf.Clamp01((float)(sp.GetTime() / Math.Max(sp.GetDuration(), 1e-4)));

                renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(b.PropertyId, Mathf.Lerp(b.from, b.to, t) * weight);
                renderer.SetPropertyBlock(_mpb);
            }

            if (controlRendererEnabled) renderer.enabled = anyActive;
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            // 그래프 종료 시(에디터 프리뷰 종료 포함) 렌더러를 꺼진 기본 상태로
            if (controlRendererEnabled && _lastRenderer != null) _lastRenderer.enabled = false;
        }
    }
}
