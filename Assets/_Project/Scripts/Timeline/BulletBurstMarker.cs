using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime
{
    /// <summary>M4-마커: 탄막 발사 시점. HitStopMarker와 같은 INotification 패턴.</summary>
    [CustomStyle("BulletBurstMarker")]
    public sealed class BulletBurstMarker : Marker, INotification
    {
        [Min(0.05f)] public float duration = 1.4f; // 발사 지속 시간(초)

        public PropertyName id => new PropertyName("BulletBurst");
    }
}
