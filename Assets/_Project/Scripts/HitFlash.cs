using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// 히트 플래시 구동부. Flash()가 호출되면 _FlashAmount를 1로 올리고 매 프레임 감쇠시킨다.
    /// 머티리얼 인스턴스를 만들지 않고 MaterialPropertyBlock으로 렌더러별 값만 덮어쓴다
    /// — 20기가 같은 머티리얼을 공유한 채 각자 다른 타이밍에 번쩍일 수 있고, 배칭도 유지된다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class HitFlash : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [SerializeField] float decayPerSecond = 4f; // 1 → 0까지 0.25초

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        float _amount;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public void Flash(float strength = 1f)
        {
            _amount = Mathf.Max(_amount, Mathf.Clamp01(strength));
        }

        void Update()
        {
            if (_amount <= 0f) return;
            _amount = Mathf.Max(0f, _amount - decayPerSecond * Time.deltaTime);
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashAmountId, _amount);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
