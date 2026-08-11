using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>
    /// M2-①: 몹 연출 커스텀 트랙. MobGroup에 바인딩되고 플래시 스윕/디졸브/아웃라인 클립을 받는다.
    /// 연출 종류별로 트랙을 하나씩 두는 구성(Flash/Outline/Dissolve 레인 분리)을 권장 —
    /// 같은 트랙 위에서 클립이 겹치면 Timeline이 크로스페이드 블렌드로 취급해 의도가 섞인다.
    /// </summary>
    [TrackColor(0.95f, 0.55f, 0.2f)]
    [TrackBindingType(typeof(MobGroup))]
    [TrackClipType(typeof(FlashSweepClip))]
    [TrackClipType(typeof(DissolveClip))]
    [TrackClipType(typeof(OutlineClip))]
    public sealed class MobFxTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<MobFxMixerBehaviour>.Create(graph, inputCount);
    }

    /// <summary>
    /// 몹 연출 믹서. 트랙 위 클립들의 (가중치, 클립 로컬 시간)을 읽어 MobGroup에 값을 쌓고
    /// 프레임 끝에 Flush 1회. 클립 종류는 GetPlayableType으로 판별한다.
    /// </summary>
    public sealed class MobFxMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var group = playerData as MobGroup;
            if (group == null) return;

            float outline = 0f;
            bool outlineTrack = false;

            int inputs = playable.GetInputCount();
            for (int i = 0; i < inputs; i++)
            {
                float weight = playable.GetInputWeight(i);
                var input = playable.GetInput(i);
                var type = input.GetPlayableType();

                if (type == typeof(OutlineBehaviour))
                {
                    // 아웃라인 세기 = 클립 amount × 클립 가중치.
                    // 가중치는 클립의 ease-in/out 곡선이 만들어 준다 — "차오르는" 연출을 공짜로 얻는 설계.
                    outlineTrack = true;
                    outline += weight * ((ScriptPlayable<OutlineBehaviour>)input).GetBehaviour().amount;
                }
                else if (weight <= 0f)
                {
                    continue; // 플래시/디졸브는 클립 창 밖이면 건드리지 않는다 (마지막 상태 유지)
                }
                else if (type == typeof(FlashSweepBehaviour))
                {
                    var sp = (ScriptPlayable<FlashSweepBehaviour>)input;
                    sp.GetBehaviour().Apply(group, sp.GetTime(), weight);
                }
                else if (type == typeof(DissolveBehaviour))
                {
                    var sp = (ScriptPlayable<DissolveBehaviour>)input;
                    sp.GetBehaviour().Apply(group, sp.GetTime());
                }
            }

            // 아웃라인 클립이 있는 트랙에서만 매 프레임 값을 쓴다 (클립 밖 = 0으로 자연 복귀)
            if (outlineTrack) group.SetOutlineAll(outline);

            group.Flush();
        }
    }
}
