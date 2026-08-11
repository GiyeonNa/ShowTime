using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace ShowTime
{
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
