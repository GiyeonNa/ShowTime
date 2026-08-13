using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;

namespace ShowTime
{
    /// <summary>
    /// M5: Spine 공격 마커 수신자 — PlayableDirector와 같은 GameObject에 붙인다.
    /// Spine AnimationState의 "트랙" 개념을 사용: idle은 트랙 0에서 계속 돌고,
    /// 공격 모션을 트랙 1에 얹었다가(AddEmptyAnimation) 블렌드로 걷어낸다 — 상태머신 없이 오버레이.
    /// </summary>
    public sealed class SpineAttackReceiver : MonoBehaviour, INotificationReceiver
    {
        SkeletonAnimation _skeleton;

        void Awake() => _skeleton = FindFirstObjectByType<SkeletonAnimation>();

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not SpineAttackMarker marker) return;
            if (!Application.isPlaying || _skeleton == null) return;

            var state = _skeleton.AnimationState;
            state.SetAnimation(1, marker.animationName, false);
            state.AddEmptyAnimation(1, marker.returnMix, 0f); // 모션 끝나면 트랙 1 비우고 idle로 복귀
        }
    }
}
