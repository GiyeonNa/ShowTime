using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>
    /// M2-마커: 히트스탑. 플레이헤드가 지나는 순간 INotification으로 수신자에게 통지된다.
    /// 트랙(구간)이 아니라 마커(시점)인 이유: 히트스탑은 "이 순간!"의 사건이지 지속 상태가 아니다.
    /// </summary>
    [CustomStyle("HitStopMarker")]
    public sealed class HitStopMarker : Marker, INotification
    {
        [Min(0.01f)] public float duration = 0.09f;        // 실시간 기준 정지 길이
        [Range(0f, 0.5f)] public float timeScale = 0.05f;  // 정지 중 시간 배율 (0이면 완전 정지)

        public PropertyName id => new PropertyName("HitStop");
    }

    /// <summary>
    /// 히트스탑 수신자 — PlayableDirector와 같은 GameObject에 붙인다.
    /// Time.timeScale을 잠깐 낮췄다가 "실시간" 대기 후 복원한다.
    /// (게임 시간은 timeScale의 영향을 받아 함께 멈추므로 반드시 Realtime 대기 —
    ///  Timeline도 Game Time 모드면 같이 멈춘다: 그것이 의도. 전장 전체가 한 박자 멎는다)
    /// </summary>
    public sealed class HitStopReceiver : MonoBehaviour, INotificationReceiver
    {
        Coroutine _running;

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not HitStopMarker marker) return;
            if (!Application.isPlaying) return; // 에디터 스크럽 중 timeScale 오염 방지

            if (_running != null) StopCoroutine(_running); // 연타 시 마지막 히트스탑으로 갱신
            _running = StartCoroutine(Run(marker));
        }

        IEnumerator Run(HitStopMarker marker)
        {
            Time.timeScale = marker.timeScale;
            yield return new WaitForSecondsRealtime(marker.duration);
            Time.timeScale = 1f;
            _running = null;
        }

        void OnDisable()
        {
            // 히트스탑 도중 파괴/비활성돼도 시간을 멈춘 채 두지 않는다
            if (_running != null) Time.timeScale = 1f;
        }
    }
}
