using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ShowTime
{
    /// <summary>
    /// M3-①: 스킬 발동 화면 연출 (비네트 + 색조) — RenderGraph 전체 화면 패스.
    ///
    /// [RenderGraph 원리 요약]
    ///  1) 패스는 "무엇을 읽고 무엇에 쓰는지"를 선언만 한다 — 실행 순서/메모리는 그래프가 최적화
    ///  2) 화면 효과 = activeColor를 읽어 새 텍스처에 쓰고, cameraColor를 그 텍스처로 교체
    ///  3) AddBlitPass가 선언·실행을 한 번에 처리 (머티리얼 블릿의 정석 경로)
    ///
    /// 평상시 비용 0 설계: AddRenderPasses에서 intensity가 0이면 패스를 큐에 넣지 않는다.
    /// </summary>
    public sealed class SkillImpactFeature : ScriptableRendererFeature
    {
        public Shader shader; // 빌드 포함 보장을 위해 에셋에 직렬화 (설치 툴이 할당)

        Material _material;
        SkillImpactPass _pass;

        public override void Create()
        {
            _pass = new SkillImpactPass
            {
                // 포스트프로세싱 직전 — 씬은 다 그려졌고 톤매핑 전이라 색 조작이 안전한 지점
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var driver = SkillImpactDriver.Current;
            if (driver == null || driver.intensity <= 0.001f) return; // 평상시: 패스 없음 = 비용 0

            if (_material == null)
            {
                if (shader == null) shader = Shader.Find("Hidden/ShowTime/SkillImpact");
                if (shader == null) return;
                _material = CoreUtils.CreateEngineMaterial(shader);
            }

            _pass.Setup(_material, driver.intensity);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);
    }

    sealed class SkillImpactPass : ScriptableRenderPass
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        Material _material;
        float _intensity;

        public void Setup(Material material, float intensity)
        {
            _material = material;
            _intensity = intensity;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var source = resources.activeColorTexture;

            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "SkillImpactColor";
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            var destination = renderGraph.CreateTexture(desc);

            _material.SetFloat(IntensityId, _intensity);
            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0),
                "Skill Impact (Vignette/Grade)");

            // 이후 패스들이 읽을 카메라 컬러를 결과물로 교체 — RenderGraph식 "출력 바꿔치기"
            resources.cameraColor = destination;
        }
    }
}
