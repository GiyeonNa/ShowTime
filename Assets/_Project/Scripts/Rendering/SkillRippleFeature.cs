using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ShowTime
{
    /// <summary>
    /// M3-②: 전체 화면 충격파 물결 — M1의 "오브젝트 방식" 왜곡(투명 쿼드 + Opaque Texture)을
    /// RenderGraph 패스 방식으로 재구현한 대조군.
    ///
    /// [오브젝트 방식 vs 패스 방식 — 기술 문서 소재]
    ///  - 오브젝트: Opaque Texture 복사 1회 필요, 쿼드가 놓인 영역만 왜곡, 투명 큐 정렬에 종속
    ///  - 패스: 화면 전체를 한 번에, 카메라 컬러를 직접 갈아끼움, UI 이전/이후 등 삽입 지점 자유
    /// </summary>
    public sealed class SkillRippleFeature : ScriptableRendererFeature
    {
        public Shader shader;

        Material _material;
        SkillRipplePass _pass;

        public override void Create()
        {
            _pass = new SkillRipplePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var driver = SkillImpactDriver.Current;
            // progress 1 = 다 퍼져서 보이지 않음 → 활성 구간에만 패스 삽입
            if (driver == null || driver.rippleProgress >= 0.999f) return;

            if (_material == null)
            {
                if (shader == null) shader = Shader.Find("Hidden/ShowTime/SkillRipple");
                if (shader == null) return;
                _material = CoreUtils.CreateEngineMaterial(shader);
            }

            _pass.Setup(_material, driver);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);
    }

    sealed class SkillRipplePass : ScriptableRenderPass
    {
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int CenterId = Shader.PropertyToID("_Center");

        Material _material;
        SkillImpactDriver _driver;

        public void Setup(Material material, SkillImpactDriver driver)
        {
            _material = material;
            _driver = driver;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var source = resources.activeColorTexture;

            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "SkillRippleColor";
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            var destination = renderGraph.CreateTexture(desc);

            // 월드 중심 → 뷰포트 좌표 (패스 방식의 이점: 카메라 정보에 직접 접근)
            Vector3 vp = cameraData.camera.WorldToViewportPoint(_driver.rippleWorldCenter);
            _material.SetFloat(ProgressId, _driver.rippleProgress);
            _material.SetVector(CenterId, new Vector4(vp.x, vp.y, 0f, 0f));

            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0),
                "Skill Ripple (Fullscreen)");

            resources.cameraColor = destination;
        }
    }
}
