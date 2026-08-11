using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// 몹 무리의 셰이더 파라미터를 일괄 관리하는 바인딩 대상.
    /// M1의 HitFlash/Dissolver/Outliner(렌더러당 컴포넌트 3개) 역할을 그룹 하나로 흡수했다.
    ///
    /// 쓰기 패턴: 트랙 믹서가 Set*()로 값만 쌓고, 프레임 끝에 Flush() 1회가
    /// 더러워진(dirty) 렌더러에만 MPB를 적용한다 — 렌더러당 최대 1회 SetPropertyBlock.
    /// 값 캐시 덕에 변화 없는 프레임엔 쓰기 자체가 없다.
    /// </summary>
    public sealed class MobGroup : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        static readonly int OutlineAmountId = Shader.PropertyToID("_OutlineAmount");
        static readonly int NoiseVariationId = Shader.PropertyToID("_NoiseVariation");

        Renderer[] _renderers;
        MaterialPropertyBlock[] _mpbs;
        float[] _flash, _dissolve, _outline;
        bool[] _dirty;

        public int Count { get { EnsureCollected(); return _renderers.Length; } }

        /// <summary>에디터 프리뷰(스크럽)에서도 동작해야 하므로 Awake 대신 지연 수집.</summary>
        void EnsureCollected()
        {
            if (_renderers != null && _renderers.Length > 0 && _renderers[0] != null) return;

            _renderers = GetComponentsInChildren<Renderer>();
            int n = _renderers.Length;
            _mpbs = new MaterialPropertyBlock[n];
            _flash = new float[n];
            _dissolve = new float[n];
            _outline = new float[n];
            _dirty = new bool[n];

            for (int i = 0; i < n; i++)
            {
                _mpbs[i] = new MaterialPropertyBlock();
                _renderers[i].GetPropertyBlock(_mpbs[i]);
                // 인스턴스별 디졸브 패턴 변주 (M1의 Dissolver.Awake 역할).
                // Random 대신 인덱스 기반 결정적 값 — 에디터 프리뷰와 플레이가 같은 그림이 되도록.
                float ox = Frac01(i * 0.7548776662f); // 황금비 수열: 균등하게 흩어진 의사난수
                float oy = Frac01(i * 0.5698402910f);
                float scale = 0.8f + 0.6f * Frac01(i * 0.3819660113f);
                _mpbs[i].SetVector(NoiseVariationId, new Vector4(ox, oy, scale, 0f));
                _renderers[i].SetPropertyBlock(_mpbs[i]);
            }
        }

        static float Frac01(float v) => v - Mathf.Floor(v);

        public void SetFlash(int index, float amount) => Set(_flashKind, index, amount);
        public void SetDissolve(int index, float amount) => Set(_dissolveKind, index, amount);

        public void SetOutlineAll(float amount)
        {
            EnsureCollected();
            for (int i = 0; i < _renderers.Length; i++) Set(_outlineKind, i, amount);
        }

        const int _flashKind = 0, _dissolveKind = 1, _outlineKind = 2;

        void Set(int kind, int index, float amount)
        {
            EnsureCollected();
            if ((uint)index >= (uint)_renderers.Length) return;
            var arr = kind == _flashKind ? _flash : kind == _dissolveKind ? _dissolve : _outline;
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(arr[index], amount)) return;
            arr[index] = amount;
            _dirty[index] = true;
        }

        /// <summary>믹서가 프레임당 1회 호출. 더러워진 렌더러에만 MPB 반영.</summary>
        public void Flush()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!_dirty[i]) continue;
                _dirty[i] = false;
                var mpb = _mpbs[i];
                mpb.SetFloat(FlashAmountId, _flash[i]);
                mpb.SetFloat(DissolveAmountId, _dissolve[i]);
                mpb.SetFloat(OutlineAmountId, _outline[i]);
                _renderers[i].SetPropertyBlock(mpb);
            }
        }
    }
}
