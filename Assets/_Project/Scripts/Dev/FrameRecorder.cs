using UnityEngine;

namespace ShowTime.Dev
{
    /// <summary>
    /// [개발 보조] 플레이 중 게임 뷰를 일정 간격으로 PNG 캡처하는 레코더.
    /// OS 화면 캡처와 달리 게임 뷰 백버퍼를 원본 해상도로 뽑는다 (에디터 창이 가려져 있어도 동작).
    /// M4의 영상/GIF 촬영 기반으로 재사용 예정. 히트스탑(timeScale) 중에도 돌도록 unscaled 시간 사용.
    /// </summary>
    public sealed class FrameRecorder : MonoBehaviour
    {
        public float interval = 0.3f;                // 캡처 간격 (초, 게임 시간)
        public float duration = 7.6f;                // 총 기록 길이 = 타임라인 한 루프
        public string outputDir = "Docs/screenshots/rec"; // 프로젝트 루트 기준

        float _elapsed;
        float _nextCapture;
        int _frameIndex;

        void OnEnable()
        {
            System.IO.Directory.CreateDirectory(outputDir);
            _elapsed = 0f;
            _nextCapture = 0f;
            _frameIndex = 0;
        }

        void Update()
        {
            // 게임 시간 기준 — 타임라인과 같은 시계를 쓴다. 이유 (M4 교훈):
            // 플레이 시작 직후 히치에서 Time.maximumDeltaTime 클램프로 게임 시간이 벽시계보다
            // 수 초 뒤처질 수 있다. 실시간 기준 녹화는 연출이 나오기 전에 끝나버린다.
            _elapsed += Time.deltaTime;
            if (_elapsed >= duration)
            {
                enabled = false;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false; // 기록 끝 = 검증 세션 끝
#endif
                return;
            }

            if (_elapsed >= _nextCapture)
            {
                _nextCapture = _elapsed + interval;
                ScreenCapture.CaptureScreenshot($"{outputDir}/frame_{_frameIndex:000}.png");
                _frameIndex++;
            }
        }
    }
}
