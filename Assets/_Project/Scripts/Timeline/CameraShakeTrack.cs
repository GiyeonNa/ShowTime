using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>
    /// M2-③: 카메라 셰이크 트랙. Transform에 바인딩, 클립 구간 동안 Perlin 노이즈로 로컬 위치를 흔든다.
    /// 랜덤이 아닌 Perlin인 이유: 연속적(부드러운 떨림)이고 시간의 함수라 스크럽해도 결정적이다.
    /// 기준 위치는 믹서가 잡아두고 클립이 끝나면 복원한다 — 트랙을 지워도 씬이 오염되지 않는다.
    /// </summary>
    [TrackColor(0.85f, 0.3f, 0.5f)]
    [TrackBindingType(typeof(Transform))]
    [TrackClipType(typeof(CameraShakeClip))]
    public sealed class CameraShakeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<CameraShakeMixerBehaviour>.Create(graph, inputCount);
    }

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

    public sealed class CameraShakeMixerBehaviour : PlayableBehaviour
    {
        Transform _target;
        Vector3 _basePosition;
        bool _hasBase;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Transform;
            if (target == null) return;

            if (!_hasBase || _target != target)
            {
                _target = target;
                _basePosition = target.localPosition;
                _hasBase = true;
            }

            Vector3 offset = Vector3.zero;
            bool anyActive = false;

            int inputs = playable.GetInputCount();
            for (int i = 0; i < inputs; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;
                anyActive = true;

                var sp = (ScriptPlayable<CameraShakeBehaviour>)playable.GetInput(i);
                var b = sp.GetBehaviour();
                float t = (float)sp.GetTime() * b.frequency;
                // PerlinNoise ∈ [0,1] → [-1,1]로 정규화, 축별로 다른 노이즈 라인
                float nx = Mathf.PerlinNoise(t, b.seed) * 2f - 1f;
                float ny = Mathf.PerlinNoise(b.seed, t) * 2f - 1f;
                offset += new Vector3(nx, ny, 0f) * (b.amplitude * weight);
            }

            target.localPosition = anyActive ? _basePosition + offset : _basePosition;
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (_hasBase && _target != null) _target.localPosition = _basePosition;
        }
    }
}
