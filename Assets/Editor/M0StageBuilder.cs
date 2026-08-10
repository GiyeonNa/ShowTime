// M0: 그레이박스 무대를 코드로 생성하는 빌더 (AnteHill 방식 — 씬은 툴로 생성)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShowTime.EditorTools
{
    public static class M0StageBuilder
    {
        const string ScenePath = "Assets/Scenes/M0_Stage.unity";
        const string MatDir = "Assets/_Project/Materials";

        [MenuItem("ShowTime/Build M0 Stage")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 재질 준비 (URP Unlit — 셰이더 작업 전 임시)
            System.IO.Directory.CreateDirectory(MatDir);
            var groundMat = MakeMat("M0_Ground", new Color(0.16f, 0.17f, 0.20f));
            var playerMat = MakeMat("M0_Player", new Color(0.35f, 0.55f, 0.95f));
            var noiseTex = NoiseTextureGen.Ensure(); // M1-②: 디졸브용 노이즈 (없으면 생성)
            var mobMats = new Material[4];
            for (int i = 0; i < 4; i++)
            {
                float shade = 1f - i * 0.16f; // 뒷줄일수록 어둡게 — 겹침 속 깊이 표현
                mobMats[i] = MakeMat($"M0_Mob_Row{i}", new Color(0.90f * shade, 0.35f * shade, 0.35f * shade),
                    "ShowTime/Mob_Combat"); // M1: 플래시+디졸브 통합 셰이더
                mobMats[i].SetTexture("_NoiseTex", noiseTex);
            }

            // 바닥 — 전투 레인
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 1.6f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // 아군 캐릭터 (좌측) — 2.5D 스프라이트 자리의 스탠드 쿼드
            var player = GameObject.CreatePrimitive(PrimitiveType.Quad);
            player.name = "Player";
            player.transform.position = new Vector3(-3.5f, 0.9f, 0f);
            player.transform.localScale = new Vector3(1.2f, 1.8f, 1f);
            player.GetComponent<Renderer>().sharedMaterial = playerMat;

            // 적 웨이브 (우측) — 몹 20기 그리드 + 지터 (뒷줄일수록 멀리, 겹침 최소화)
            var mobRoot = new GameObject("Mobs");
            int idx = 0;
            for (int col = 0; col < 5; col++)
            for (int row = 0; row < 4; row++)
            {
                var mob = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mob.name = $"Mob_{idx}";
                mob.transform.SetParent(mobRoot.transform);
                float jx = (idx * 0.37f) % 0.3f - 0.15f;
                float jz = (idx * 0.61f) % 0.3f - 0.15f;
                mob.transform.position = new Vector3(1.4f + col * 0.8f + jx, 0.5f, 0.2f + row * 1.1f + jz);
                mob.transform.localScale = new Vector3(0.6f, 1.0f, 1f);
                mob.GetComponent<Renderer>().sharedMaterial = mobMats[row];
                mob.AddComponent<HitFlash>();  // M1-①: 플래시 구동부
                mob.AddComponent<Dissolver>(); // M1-②: 디졸브 구동부
                idx++;
            }

            // 카메라 — 가로 한 화면 구도 (아군 좌 / 적 우), 그레이박스용 단색 배경
            var cam = Camera.main;
            cam.transform.position = new Vector3(0.3f, 2.3f, -8.6f);
            cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.14f);

            // 라이트 — 살짝 따뜻하게
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                light.color = new Color(1f, 0.96f, 0.90f);
            }

            // M1 검증 드라이버 (플래시 스윕 → 디졸브 소멸 → 재등장 루프)
            new GameObject("M1ShaderDemo").AddComponent<M1ShaderDemo>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[M0] Stage built and saved: " + ScenePath);
        }

        static Material MakeMat(string name, Color c, string shaderName = "Universal Render Pipeline/Unlit")
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != shader) existing.shader = shader;
                existing.color = c;
                return existing;
            }
            var mat = new Material(shader) { color = c };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
