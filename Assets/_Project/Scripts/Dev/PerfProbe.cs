using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace ShowTime.Dev
{
    /// <summary>
    /// [개발 보조] M4 실측 프로브 — 매 프레임 드로우콜/셋패스/배치/프레임 타임/탄환 수를 수집해
    /// 플레이 종료 시 CSV로 남긴다 (before/after 표의 원자료).
    /// MyPark2 측정 때 드로우콜이 0으로 잡힌 전례가 있어, 기록 후 콘솔 요약으로 즉시 검증한다.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        public string outputDir = "Docs/perf"; // 프로젝트 루트 기준

        ProfilerRecorder _drawCalls;
        ProfilerRecorder _setPass;
        ProfilerRecorder _batches;
        StringBuilder _csv;
        BulletSystem _bullets;
        string _modeName; // OnDisable 시점엔 _bullets가 이미 파괴됐을 수 있어 미리 캐시
        float _t;

        void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _csv = new StringBuilder("t,dtMs,drawCalls,setPass,batches,bullets\n");
            _bullets = FindFirstObjectByType<BulletSystem>();
            _modeName = _bullets != null ? _bullets.mode.ToString() : "unknown";
            _t = 0f;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            _csv.Append(_t.ToString("F3")).Append(',')
                .Append((Time.unscaledDeltaTime * 1000f).ToString("F2")).Append(',')
                .Append(_drawCalls.Valid ? _drawCalls.LastValue : -1).Append(',')
                .Append(_setPass.Valid ? _setPass.LastValue : -1).Append(',')
                .Append(_batches.Valid ? _batches.LastValue : -1).Append(',')
                .Append(_bullets != null ? _bullets.AliveCount : 0).Append('\n');
        }

        void OnDisable()
        {
            _drawCalls.Dispose();
            _setPass.Dispose();
            _batches.Dispose();
            if (_csv == null) return;

            System.IO.Directory.CreateDirectory(outputDir);
            string path = $"{outputDir}/perf_{_modeName}.csv";
            System.IO.File.WriteAllText(path, _csv.ToString());
            Debug.Log($"[PerfProbe] saved {path} ({_t:F1}s)"); // 기록 검증용 1줄
        }
    }
}
