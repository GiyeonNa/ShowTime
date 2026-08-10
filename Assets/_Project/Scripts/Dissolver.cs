using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// 디졸브 구동부. HitFlash와 같은 패턴 — MaterialPropertyBlock으로
    /// 렌더러별 _DissolveAmount만 덮어써서, 공유 머티리얼을 유지한 채 개별 소멸을 표현한다.
    /// 값의 시간 제어(0→1 진행)는 외부(데모 드라이버, 나중엔 Timeline)가 담당하고
    /// 이 컴포넌트는 "값을 렌더러에 꽂는 일"만 한다 — 연출 데이터와 적용 로직의 분리.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class Dissolver : MonoBehaviour
    {
        static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        static readonly int NoiseVariationId = Shader.PropertyToID("_NoiseVariation");

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        float _current = -1f;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            // 인스턴스별 패턴 변주: 노이즈 UV를 랜덤 오프셋 + 스케일(0.8~1.4)로 어긋나게
            // → 텍스처 하나를 공유하면서 몹마다 다른 모양으로 타들어간다
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(NoiseVariationId,
                new Vector4(Random.value, Random.value, Random.Range(0.8f, 1.4f), 0f));
            _renderer.SetPropertyBlock(_mpb);
        }

        public void SetAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _current)) return; // 변화 없으면 MPB 갱신 생략
            _current = amount;
            // Get→수정→Set 패턴: HitFlash가 써둔 _FlashAmount를 지우지 않고 공존시킨다
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(DissolveAmountId, amount);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
