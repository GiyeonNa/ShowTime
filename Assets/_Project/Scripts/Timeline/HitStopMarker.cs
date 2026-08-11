using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>
    /// M2-마커: 히트스탑. 플레이헤드가 지나는 순간 INotification으로 수신자(HitStopReceiver)에게 통지된다.
    /// 트랙(구간)이 아니라 마커(시점)인 이유: 히트스탑은 "이 순간!"의 사건이지 지속 상태가 아니다.
    /// </summary>
    [CustomStyle("HitStopMarker")]
    public sealed class HitStopMarker : Marker, INotification
    {
        [Min(0.01f)] public float duration = 0.09f;        // 실시간 기준 정지 길이
        [Range(0f, 0.5f)] public float timeScale = 0.05f;  // 정지 중 시간 배율 (0이면 완전 정지)

        public PropertyName id => new PropertyName("HitStop");
    }
}
