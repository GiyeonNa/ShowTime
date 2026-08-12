using UnityEngine;
using UnityEngine.Playables;

namespace ShowTime
{
    /// <summary>탄막 마커 수신자 — PlayableDirector와 같은 GameObject에 붙인다.</summary>
    public sealed class BulletBurstReceiver : MonoBehaviour, INotificationReceiver
    {
        BulletSystem _bullets;

        void Awake() => _bullets = FindFirstObjectByType<BulletSystem>();

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not BulletBurstMarker marker) return;
            if (!Application.isPlaying) return;
            if (_bullets != null) _bullets.Burst(marker.duration);
        }
    }
}
