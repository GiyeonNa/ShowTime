// [개발 보조] 외부(Claude Code)가 명령 파일로 에디터를 조종하는 봇 — 출시 전 삭제
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShowTime.EditorTools
{
    [InitializeOnLoad]
    public static class ShowTimeBot
    {
        const string Dir = @"C:\Users\N\AppData\Local\Temp\claude\C--Users-N-Desktop----\40d0b838-d327-4968-8ef0-ea0b22b5fc07\scratchpad";
        static string CmdPath => Path.Combine(Dir, "st_cmd.txt");
        static string StatusPath => Path.Combine(Dir, "st_status.txt");
        static double _next;

        static ShowTimeBot()
        {
            EditorApplication.update += Poll;
            EditorApplication.delayCall += () => Status("ready");
        }

        static void Status(string msg)
        {
            try { File.WriteAllText(StatusPath, msg); } catch { }
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.5;

            // record 종료 감시 (플레이 도메인 리로드에도 살아남도록 SessionState 사용)
            if (SessionState.GetBool("st_recwatch", false)
                && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool("st_recwatch", false);
                var rec = UnityEngine.Object.FindFirstObjectByType<ShowTime.Dev.FrameRecorder>(FindObjectsInactive.Include);
                if (rec != null) rec.enabled = false;
                Status("done: record");
            }

            if (!File.Exists(CmdPath)) return;
            string[] lines;
            try { lines = File.ReadAllLines(CmdPath); File.Delete(CmdPath); }
            catch { return; }
            if (lines.Length == 0) return;
            try { Execute(lines); }
            catch (Exception e) { Status("error: " + e.Message + "\n" + e.StackTrace); }
        }

        static float Arg(string[] lines, string key, float def)
        {
            foreach (var l in lines)
                if (l.StartsWith(key + "="))
                    return float.Parse(l.Substring(key.Length + 1));
            return def;
        }

        static void Execute(string[] lines)
        {
            switch (lines[0].Trim())
            {
                case "build_m0":
                    M0StageBuilder.Build();
                    Status("done: build_m0");
                    break;
                case "game_shot":
                {
                    var gv = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                    gv.Focus();
                    if (Arg(lines, "max", 1f) > 0.5f) gv.maximized = true;
                    Status("done: game_shot");
                    break;
                }
                case "game_unmax":
                {
                    var gv = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                    if (gv.maximized) gv.maximized = false;
                    Status("done: game_unmax");
                    break;
                }
                case "scene_shot":
                {
                    var sv = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                    sv.Focus();
                    var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                    if (rends.Length == 0) { Status("error: no renderers"); return; }
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    sv.drawGizmos = false;
                    sv.showGrid = false;
                    sv.LookAt(b.center,
                        Quaternion.Euler(Arg(lines, "pitch", 30f), Arg(lines, "yaw", 200f), 0f),
                        b.extents.magnitude * Arg(lines, "zoom", 0.9f), false, true);
                    if (Arg(lines, "max", 1f) > 0.5f) sv.maximized = true;
                    sv.Repaint();
                    Status("done: scene_shot");
                    break;
                }
                case "bg_on":
                    // 에디터 백그라운드 절전 해제 — 포커스 없이도 플레이 루프가 돈다
                    EditorPrefs.SetInt("InteractionMode", 1); // 1 = No Throttling
                    PlayerSettings.runInBackground = true;
                    Status("done: bg_on");
                    break;
                case "record":
                {
                    // 씬의 FrameRecorder를 켜고 플레이 — 녹화가 끝나면 레코더가 스스로 플레이를 멈춘다
                    var rec = UnityEngine.Object.FindFirstObjectByType<ShowTime.Dev.FrameRecorder>(FindObjectsInactive.Include);
                    if (rec == null) { Status("error: no FrameRecorder in scene"); break; }
                    rec.interval = Arg(lines, "interval", 0.3f);
                    rec.duration = Arg(lines, "duration", 7.6f); // 게임 시간 기준 한 루프
                    rec.enabled = true;
                    SessionState.SetBool("st_recwatch", true);
                    EditorApplication.isPlaying = true;
                    Status("recording");
                    break;
                }
                case "composer_smoke":
                    Status(SkillComposerWindow.RunSmokeTest());
                    break;
                case "bullet_mode":
                {
                    // 실측 모드 전환: naive=1 → GameObject 대조군, 기본 → Instanced
                    var bs = UnityEngine.Object.FindFirstObjectByType<ShowTime.BulletSystem>(FindObjectsInactive.Include);
                    if (bs == null) { Status("error: no BulletSystem"); break; }
                    bs.mode = Arg(lines, "naive", 0f) > 0.5f
                        ? ShowTime.BulletSystem.BulletRenderMode.NaiveGameObjects
                        : ShowTime.BulletSystem.BulletRenderMode.Instanced;
                    Status("done: bullet_mode " + bs.mode);
                    break;
                }
                case "play":
                    EditorApplication.isPlaying = true;
                    Status("done: play");
                    break;
                case "stop":
                    EditorApplication.isPlaying = false;
                    Status("done: stop");
                    break;
                default:
                    Status("error: unknown command " + lines[0]);
                    break;
            }
        }
    }
}
