// M2 후반: 연출 조립 툴 — 코드/Timeline 창 없이 클립을 추가·배치·튜닝하는 에디터 창.
// 목표 사용자: 아티스트. "코드 없이 연출 조립 가능한 툴" (공고: 아티스트 협업의 기술적 구현).
//
// v1 범위: 트랙/클립/마커의 추가·삭제·시간·파라미터·바인딩 편집 + 겹침 경고 + 플레이/녹화 검증 버튼.
// Undo는 구조 변경(클립/트랙/마커 추가·삭제) 단위로 지원. 파라미터 필드 단위 Undo는 v2.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShowTime.EditorTools
{
    public sealed class SkillComposerWindow : EditorWindow
    {
        PlayableDirector _director;
        Vector2 _scroll;

        [MenuItem("ShowTime/연출 조립 (Skill Composer)")]
        public static void Open() => GetWindow<SkillComposerWindow>("연출 조립");

        TimelineAsset Timeline => _director != null ? _director.playableAsset as TimelineAsset : null;

        void OnEnable()
        {
            if (_director == null) _director = FindFirstObjectByType<PlayableDirector>();
        }

        void OnGUI()
        {
            _director = (PlayableDirector)EditorGUILayout.ObjectField(
                "Director", _director, typeof(PlayableDirector), true);
            var timeline = Timeline;
            if (timeline == null)
            {
                EditorGUILayout.HelpBox("씬의 PlayableDirector(+TimelineAsset)를 지정하세요.", MessageType.Info);
                return;
            }

            DrawToolbar(timeline);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var track in timeline.GetOutputTracks().ToArray())
                DrawTrack(timeline, track);
            DrawMarkers(timeline);
            EditorGUILayout.EndScrollView();
        }

        // ── 상단 툴바 ──────────────────────────────────────────────

        void DrawToolbar(TimelineAsset timeline)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 트랙", GUILayout.Width(70)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("몹 연출 (MobFx)"), false,
                        () => AddTrack<MobFxTrack>(timeline, "Mob FX"));
                    menu.AddItem(new GUIContent("셰이더 Float"), false,
                        () => AddTrack<ShaderFloatTrack>(timeline, "Shader Float"));
                    menu.AddItem(new GUIContent("카메라 셰이크"), false,
                        () => AddTrack<CameraShakeTrack>(timeline, "Camera Shake"));
                    menu.ShowAsContext();
                }
                if (GUILayout.Button("저장", GUILayout.Width(60)))
                    AssetDatabase.SaveAssets();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▶ 플레이 검증", GUILayout.Width(100)))
                    EditorApplication.isPlaying = true;
                if (GUILayout.Button("● 녹화 검증", GUILayout.Width(100)))
                {
                    var rec = FindFirstObjectByType<ShowTime.Dev.FrameRecorder>(FindObjectsInactive.Include);
                    if (rec != null)
                    {
                        rec.enabled = true;
                        SessionState.SetBool("st_recwatch", true); // 봇이 종료 후 레코더를 꺼준다
                        EditorApplication.isPlaying = true;
                    }
                }
            }
            EditorGUILayout.Space(4);
        }

        void AddTrack<T>(TimelineAsset timeline, string name) where T : TrackAsset, new()
        {
            Undo.RegisterCompleteObjectUndo(timeline, "트랙 추가");
            timeline.CreateTrack<T>(null, name);
            Dirty(timeline);
        }

        // ── 트랙 그리기 ────────────────────────────────────────────

        void DrawTrack(TimelineAsset timeline, TrackAsset track)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{track.name}", EditorStyles.boldLabel, GUILayout.Width(140));
                    EditorGUILayout.LabelField(track.GetType().Name, EditorStyles.miniLabel, GUILayout.Width(110));
                    DrawBinding(track);
                    if (GUILayout.Button("+ 클립", GUILayout.Width(60))) ShowAddClipMenu(timeline, track);
                    if (GUILayout.Button("트랙 삭제", GUILayout.Width(70)))
                    {
                        Undo.RegisterCompleteObjectUndo(timeline, "트랙 삭제");
                        timeline.DeleteTrack(track);
                        Dirty(timeline);
                        return;
                    }
                }

                // 트랙 전용 옵션
                if (track is ShaderFloatTrack sft)
                {
                    EditorGUI.BeginChangeCheck();
                    bool v = EditorGUILayout.ToggleLeft(
                        "클립 밖에서 렌더러 끄기 (재생 중에만 존재하는 오브젝트용)", sft.controlRendererEnabled);
                    if (EditorGUI.EndChangeCheck()) { sft.controlRendererEnabled = v; Dirty(sft); }
                }

                var clips = track.GetClips().OrderBy(c => c.start).ToArray();
                for (int i = 0; i < clips.Length; i++)
                {
                    DrawClip(timeline, clips[i]);
                    // 겹침 경고: 같은 트랙에서 클립이 겹치면 Timeline은 크로스페이드로 취급한다
                    if (i > 0 && clips[i].start < clips[i - 1].end)
                        EditorGUILayout.HelpBox(
                            $"'{clips[i - 1].displayName}'와 겹칩니다 → 크로스페이드 블렌드가 됩니다. " +
                            "독립 연출이면 트랙을 분리하세요.", MessageType.Warning);
                }
            }
        }

        void DrawBinding(TrackAsset track)
        {
            Type bindType = track switch
            {
                MobFxTrack => typeof(MobGroup),
                ShaderFloatTrack => typeof(Renderer),
                CameraShakeTrack => typeof(Transform),
                _ => typeof(UnityEngine.Object)
            };
            var current = _director.GetGenericBinding(track);
            var next = EditorGUILayout.ObjectField(current, bindType, true, GUILayout.MinWidth(120));
            if (next != current)
            {
                Undo.RecordObject(_director, "바인딩 변경");
                _director.SetGenericBinding(track, next);
            }
        }

        void ShowAddClipMenu(TimelineAsset timeline, TrackAsset track)
        {
            var menu = new GenericMenu();
            void Item<T>(string label) where T : ScriptableObject, IPlayableAsset
                => menu.AddItem(new GUIContent(label), false, () => AddClip<T>(timeline, track, label));

            switch (track)
            {
                case MobFxTrack:
                    Item<FlashSweepClip>("평타 스윕");
                    Item<DissolveClip>("디졸브");
                    Item<OutlineClip>("아웃라인");
                    break;
                case ShaderFloatTrack:
                    Item<ShaderFloatClip>("셰이더 Float");
                    break;
                case CameraShakeTrack:
                    Item<CameraShakeClip>("셰이크");
                    break;
            }
            menu.ShowAsContext();
        }

        void AddClip<T>(TimelineAsset timeline, TrackAsset track, string name)
            where T : ScriptableObject, IPlayableAsset
        {
            Undo.RegisterCompleteObjectUndo(timeline, "클립 추가");
            var clip = track.CreateClip<T>();
            clip.displayName = name;
            // 기본 배치: 트랙의 마지막 클립 뒤 (겹침 함정을 기본값부터 회피)
            double lastEnd = track.GetClips().Where(c => c != clip)
                .Select(c => c.end).DefaultIfEmpty(0).Max();
            clip.start = lastEnd;
            clip.duration = Math.Max(clip.duration, 0.5);
            Dirty(timeline);
        }

        // ── 클립 한 줄 ─────────────────────────────────────────────

        void DrawClip(TimelineAsset timeline, TimelineClip clip)
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                clip.displayName = EditorGUILayout.TextField(clip.displayName, GUILayout.Width(110));
                EditorGUILayout.LabelField("시작", GUILayout.Width(28));
                clip.start = Math.Max(0, EditorGUILayout.DoubleField(clip.start, GUILayout.Width(50)));
                EditorGUILayout.LabelField("길이", GUILayout.Width(28));
                clip.duration = Math.Max(0.05, EditorGUILayout.DoubleField(clip.duration, GUILayout.Width(50)));
                EditorGUILayout.LabelField("이즈", GUILayout.Width(28));
                clip.easeInDuration = Math.Max(0, EditorGUILayout.DoubleField(clip.easeInDuration, GUILayout.Width(40)));
                clip.easeOutDuration = Math.Max(0, EditorGUILayout.DoubleField(clip.easeOutDuration, GUILayout.Width(40)));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    Undo.RegisterCompleteObjectUndo(timeline, "클립 삭제");
                    timeline.DeleteClip(clip);
                    Dirty(timeline);
                    return;
                }
            }
            using (new EditorGUI.IndentLevelScope())
                DrawClipParams(clip.asset);
            if (EditorGUI.EndChangeCheck()) Dirty(timeline);
        }

        /// <summary>클립 종류별 파라미터 — 아티스트에게 보여줄 필드만 한국어 라벨로 노출.</summary>
        void DrawClipParams(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case FlashSweepClip c:
                    c.template.interval = EditorGUILayout.Slider("타격 간격(초)", c.template.interval, 0.03f, 0.5f);
                    c.template.decayPerSecond = EditorGUILayout.Slider("감쇠 속도", c.template.decayPerSecond, 1f, 12f);
                    break;
                case DissolveClip c:
                    c.template.perMobDuration = EditorGUILayout.Slider("1기 소멸 시간(초)", c.template.perMobDuration, 0.1f, 3f);
                    c.template.stagger = EditorGUILayout.Slider("몹별 지연(초)", c.template.stagger, 0f, 0.3f);
                    c.template.reverse = EditorGUILayout.Toggle("역재생(재등장)", c.template.reverse);
                    break;
                case OutlineClip c:
                    c.template.amount = EditorGUILayout.Slider("세기", c.template.amount, 0f, 1f);
                    break;
                case ShaderFloatClip c:
                    c.template.propertyName = EditorGUILayout.TextField("프로퍼티", c.template.propertyName);
                    c.template.from = EditorGUILayout.FloatField("시작값", c.template.from);
                    c.template.to = EditorGUILayout.FloatField("끝값", c.template.to);
                    break;
                case CameraShakeClip c:
                    c.template.amplitude = EditorGUILayout.Slider("진폭", c.template.amplitude, 0f, 1f);
                    c.template.frequency = EditorGUILayout.Slider("진동수", c.template.frequency, 1f, 60f);
                    break;
            }
            if (asset != null && GUI.changed) EditorUtility.SetDirty(asset);
        }

        // ── 마커 (히트스탑) ────────────────────────────────────────

        void DrawMarkers(TimelineAsset timeline)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("히트스탑 마커", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ 히트스탑", GUILayout.Width(80)))
                    {
                        Undo.RegisterCompleteObjectUndo(timeline, "마커 추가");
                        timeline.CreateMarkerTrack();
                        timeline.markerTrack.CreateMarker<HitStopMarker>(0);
                        Dirty(timeline);
                    }
                }

                if (timeline.markerTrack == null) return;
                foreach (var m in timeline.markerTrack.GetMarkers().OfType<HitStopMarker>().ToArray())
                {
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("시각", GUILayout.Width(28));
                        m.time = Math.Max(0, EditorGUILayout.DoubleField(m.time, GUILayout.Width(50)));
                        EditorGUILayout.LabelField("길이", GUILayout.Width(28));
                        m.duration = EditorGUILayout.FloatField(m.duration, GUILayout.Width(40));
                        EditorGUILayout.LabelField("배율", GUILayout.Width(28));
                        m.timeScale = EditorGUILayout.FloatField(m.timeScale, GUILayout.Width(40));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                        {
                            Undo.RegisterCompleteObjectUndo(timeline, "마커 삭제");
                            timeline.markerTrack.DeleteMarker(m);
                            Dirty(timeline);
                            break;
                        }
                    }
                    if (EditorGUI.EndChangeCheck()) Dirty(m);
                }
            }
        }

        // ── 공통 ───────────────────────────────────────────────────

        static void Dirty(UnityEngine.Object o)
        {
            EditorUtility.SetDirty(o);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved); // Timeline 창 열려 있으면 즉시 반영
        }

        /// <summary>[개발 보조] 봇 스모크 테스트 — 창 열고 클립 추가/삭제 왕복이 에셋에 반영되는지.</summary>
        public static string RunSmokeTest()
        {
            var window = GetWindow<SkillComposerWindow>("연출 조립");
            var timeline = window.Timeline;
            if (timeline == null) return "smoke fail: no director/timeline";

            var track = timeline.GetOutputTracks().OfType<MobFxTrack>().FirstOrDefault();
            if (track == null) return "smoke fail: no MobFxTrack";

            int before = track.GetClips().Count();
            window.AddClip<OutlineClip>(timeline, track, "스모크");
            int added = track.GetClips().Count();
            var clip = track.GetClips().First(c => c.displayName == "스모크");
            timeline.DeleteClip(clip);
            int restored = track.GetClips().Count();
            window.Repaint();
            return $"done: smoke {before}->{added}->{restored}" +
                   (added == before + 1 && restored == before ? " ok" : " MISMATCH");
        }
    }
}
