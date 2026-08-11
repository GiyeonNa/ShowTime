using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// 충격파 구동부. Play()가 호출되면 _Progress를 0→1로 진행시키고 끝나면 렌더러를 끈다.
    /// 대기 중에는 렌더러 자체를 꺼서 화면 재샘플링(그랩) 비용이 0이 되게 한다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class Shockwave : MonoBehaviour
    {
        static readonly int ProgressId = Shader.PropertyToID("_Progress");

        [SerializeField] float duration = 0.55f;

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        float _t = -1f; // 음수 = 정지 상태

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        public void Play()
        {
            _t = 0f;
            _renderer.enabled = true;
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            float progress = Mathf.Clamp01(_t / duration);
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_mpb);
            if (progress >= 1f)
            {
                _t = -1f;
                _renderer.enabled = false;
            }
        }
    }
}
