using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// 아웃라인 구동부. Dissolver와 같은 패턴 — MaterialPropertyBlock으로
    /// 렌더러별 _OutlineAmount만 덮어쓴다. 시간 제어는 외부(데모 드라이버, 나중엔 Timeline) 담당.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class Outliner : MonoBehaviour
    {
        static readonly int OutlineAmountId = Shader.PropertyToID("_OutlineAmount");

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        float _current = -1f;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public void SetAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _current)) return;
            _current = amount;
            // Get→수정→Set: HitFlash/Dissolver가 써둔 값들과 공존
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OutlineAmountId, amount);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
