using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>M5-마커: Spine 공격 모션 트리거. 히트스탑/탄막과 같은 INotification 패턴.</summary>
    [CustomStyle("SpineAttackMarker")]
    public sealed class SpineAttackMarker : Marker, INotification
    {
        public string animationName = "shoot";
        [Min(0f)] public float returnMix = 0.2f; // 발사 후 기본 자세로 돌아가는 블렌드 시간

        public PropertyName id => new PropertyName("SpineAttack");
    }
}
