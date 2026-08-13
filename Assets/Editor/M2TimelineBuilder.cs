// M2: 스킬 쇼케이스 타임라인을 코드로 조립하는 빌더.
// M1ShaderDemo(누적 타이머 하드코딩)가 하던 시간 제어를 Timeline 에셋으로 이관 —
// 이후 연출 수정은 코드가 아니라 Timeline 창에서 클립을 드래그해서 한다 (아티스트 협업 요건).
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace ShowTime.EditorTools
{
    public static class M2TimelineBuilder
    {
        const string Dir = "Assets/_Project/Timeline";
        const string AssetPath = Dir + "/SkillShowcase.playable";

        // 스테이지 빌더가 바인딩할 때 쓰는 트랙 이름 (이름 = 계약)
        public const string FlashTrack = "Mob Flash";
        public const string OutlineTrack = "Mob Outline";
        public const string DissolveTrack = "Mob Dissolve";
        public const string ShockwaveTrack = "Shockwave";
        public const string CameraTrack = "Camera Shake";
        public const string ScreenFxTrack = "Screen FX";

        [MenuItem("ShowTime/Build M2 Timeline Asset")]
        public static TimelineAsset Build()
        {
            System.IO.Directory.CreateDirectory(Dir);
            AssetDatabase.DeleteAsset(AssetPath); // 툴이 원본 — 항상 재생성 (M0 씬과 같은 원칙)

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, AssetPath);

            // ── 몹 연출: 연출 종류별 트랙 분리 (같은 트랙에서 겹치면 크로스페이드로 섞이므로) ──
            var flash = timeline.CreateTrack<MobFxTrack>(null, FlashTrack);
            var flashClip = flash.CreateClip<FlashSweepClip>();
            flashClip.start = 0.0;
            flashClip.duration = 2.4;       // 0.12s × 몹 20기 = 스윕 한 바퀴
            flashClip.displayName = "평타 스윕";

            var outline = timeline.CreateTrack<MobFxTrack>(null, OutlineTrack);
            var outlineClip = outline.CreateClip<OutlineClip>();
            outlineClip.start = 2.4;
            outlineClip.duration = 2.5;
            outlineClip.easeInDuration = 0.45;  // "차오르는" 연출 = 클립 ease 곡선 (코드 아님)
            outlineClip.easeOutDuration = 0.4;
            outlineClip.displayName = "전멸 예고";

            var dissolve = timeline.CreateTrack<MobFxTrack>(null, DissolveTrack);
            var dissolveOut = dissolve.CreateClip<DissolveClip>();
            dissolveOut.start = 2.85;
            dissolveOut.duration = 2.6;     // 마지막 몹 소멸 후 정적(hold)까지 포함
            dissolveOut.displayName = "전멸";
            var dissolveIn = dissolve.CreateClip<DissolveClip>();
            dissolveIn.start = 5.45;
            dissolveIn.duration = 2.05;
            dissolveIn.displayName = "재등장";
            ((DissolveClip)dissolveIn.asset).template.reverse = true;

            // ── 충격파: 범용 셰이더 float 트랙으로 _Progress 0→1 ──
            var wave = timeline.CreateTrack<ShaderFloatTrack>(null, ShockwaveTrack);
            wave.controlRendererEnabled = true; // 클립 밖에서는 렌더러 자체를 꺼서 그랩 비용 0
            var waveClip = wave.CreateClip<ShaderFloatClip>();
            waveClip.start = 2.85;
            waveClip.duration = 0.55;
            waveClip.displayName = "충격파";
            var waveB = ((ShaderFloatClip)waveClip.asset).template;
            waveB.propertyName = "_Progress";
            waveB.from = 0f;
            waveB.to = 1f;

            // ── 카메라 셰이크 ──
            var cam = timeline.CreateTrack<CameraShakeTrack>(null, CameraTrack);
            var camClip = cam.CreateClip<CameraShakeClip>();
            camClip.start = 2.85;
            camClip.duration = 0.5;
            camClip.easeOutDuration = 0.25;
            camClip.displayName = "임팩트 셰이크";

            // ── M3: 화면 연출 (비네트/색조 + 전체 화면 물결) — 내장 AnimationTrack으로 구동 ──
            // 렌더러가 없는 대상(RenderFeature 구동 값)은 표준 애니메이션 바인딩이 정석.
            // 커스텀 트랙과 내장 트랙을 한 타임라인에서 혼용하는 시연이기도 하다.
            var screenFxClip = new AnimationClip { name = "ScreenFxCurves" };
            screenFxClip.SetCurve("", typeof(SkillImpactDriver), "intensity", new AnimationCurve(
                new Keyframe(2.4f, 0f), new Keyframe(2.85f, 1f),
                new Keyframe(3.7f, 1f), new Keyframe(4.5f, 0f)));
            screenFxClip.SetCurve("", typeof(SkillImpactDriver), "rippleProgress", new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(2.84f, 1f),   // 1 = 꺼짐 상태 유지
                new Keyframe(2.85f, 0f), new Keyframe(3.5f, 1f))); // 터지는 순간 0 → 화면 끝까지
            AssetDatabase.AddObjectToAsset(screenFxClip, timeline);
            var screenFx = timeline.CreateTrack<AnimationTrack>(null, ScreenFxTrack);
            var screenFxTlClip = screenFx.CreateClip(screenFxClip);
            screenFxTlClip.start = 0.0;
            screenFxTlClip.duration = 7.5;
            screenFxTlClip.displayName = "화면 연출";

            // ── 히트스탑 마커: 충격파 터지는 그 시점 ──
            timeline.CreateMarkerTrack(); // markerTrack은 자동 생성되지 않는다 — 명시적 생성 필요
            var marker = timeline.markerTrack.CreateMarker<HitStopMarker>(2.85);
            marker.duration = 0.09f;
            marker.timeScale = 0.05f;

            // ── M4: 탄막 발사 마커 — 충격파와 함께 개시, 디졸브 구간 동안 쏟아진다 ──
            var burst = timeline.markerTrack.CreateMarker<BulletBurstMarker>(2.85);
            burst.duration = 1.5f;

            // ── M5: Spine 공격 모션 — 탄막보다 반 박자 먼저 (조준→발사 선행 동작) ──
            timeline.markerTrack.CreateMarker<SpineAttackMarker>(2.6);

            timeline.fixedDuration = 7.5; // 루프 한 사이클
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            Debug.Log("[M2] Timeline asset built: " + AssetPath);
            return timeline;
        }
    }
}
