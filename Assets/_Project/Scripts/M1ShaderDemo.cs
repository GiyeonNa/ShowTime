using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// M1 검증 데모 사이클: 플래시 스윕 → 아웃라인 플레어(+충격파) → 전체 디졸브 소멸 → 재등장 → 반복.
    /// M2에서 Timeline 커스텀 트랙이 이 역할을 이어받으면 삭제 예정.
    /// </summary>
    public sealed class M1ShaderDemo : MonoBehaviour
    {
        [SerializeField] float flashInterval = 0.12f;   // 플래시 스윕 간격
        [SerializeField] float sweepDuration = 2.4f;    // 스윕 단계 길이
        [SerializeField] float outlineFlareDuration = 0.45f; // 아웃라인 차오르는 시간 (전멸 예고)
        [SerializeField] float dissolveDuration = 1.1f; // 몹 1기의 소멸 시간
        [SerializeField] float stagger = 0.04f;         // 몹별 시작 지연 — 물결처럼 번지게
        [SerializeField] float holdDuration = 0.6f;     // 전멸 후 정적

        enum Phase { FlashSweep, OutlineFlare, DissolveOut, Hold, DissolveIn }

        HitFlash[] _flash;
        Dissolver[] _dissolve;
        Outliner[] _outline;
        Shockwave _shockwave;
        Phase _phase;
        float _phaseT;
        float _flashT;
        int _flashIndex;

        void Start()
        {
            _flash = FindObjectsByType<HitFlash>(FindObjectsSortMode.InstanceID);
            _dissolve = FindObjectsByType<Dissolver>(FindObjectsSortMode.InstanceID);
            _outline = FindObjectsByType<Outliner>(FindObjectsSortMode.InstanceID);
            _shockwave = FindFirstObjectByType<Shockwave>();
        }

        void Update()
        {
            _phaseT += Time.deltaTime;
            switch (_phase)
            {
                case Phase.FlashSweep:
                    _flashT += Time.deltaTime;
                    if (_flashT >= flashInterval && _flash.Length > 0)
                    {
                        _flashT = 0f;
                        _flash[_flashIndex % _flash.Length].Flash();
                        _flashIndex++;
                    }
                    if (_phaseT >= sweepDuration) Next(Phase.OutlineFlare);
                    break;

                case Phase.OutlineFlare:
                    // 전멸 예고 — 아웃라인이 차오른다 (스킬 타겟팅 강조의 용법)
                    SetOutline(Mathf.Clamp01(_phaseT / outlineFlareDuration));
                    if (_phaseT >= outlineFlareDuration)
                    {
                        if (_shockwave != null) _shockwave.Play(); // 충격파와 함께 소멸 개시
                        Next(Phase.DissolveOut);
                    }
                    break;

                case Phase.DissolveOut:
                    if (DriveDissolve(forward: true)) Next(Phase.Hold);
                    break;

                case Phase.Hold:
                    if (_phaseT >= holdDuration) Next(Phase.DissolveIn);
                    break;

                case Phase.DissolveIn:
                    // 재등장하며 아웃라인 해제
                    SetOutline(1f - Mathf.Clamp01(_phaseT / outlineFlareDuration));
                    if (DriveDissolve(forward: false)) Next(Phase.FlashSweep);
                    break;
            }
        }

        /// <summary>몹별로 stagger만큼 어긋난 로컬 진행도를 계산해 꽂는다. 전원 완료 시 true.</summary>
        bool DriveDissolve(bool forward)
        {
            bool allDone = true;
            for (int i = 0; i < _dissolve.Length; i++)
            {
                float local = Mathf.Clamp01((_phaseT - i * stagger) / dissolveDuration);
                _dissolve[i].SetAmount(forward ? local : 1f - local);
                if (local < 1f) allDone = false;
            }
            return allDone;
        }

        void SetOutline(float amount)
        {
            for (int i = 0; i < _outline.Length; i++)
                _outline[i].SetAmount(amount);
        }

        void Next(Phase phase)
        {
            _phase = phase;
            _phaseT = 0f;
        }
    }
}
