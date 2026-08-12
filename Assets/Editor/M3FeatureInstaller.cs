// M3: RendererFeature를 렌더러 에셋(PC/Mobile)에 코드로 설치하는 유틸.
// 공개 API가 없어 SerializedObject로 m_RendererFeatures + m_RendererFeatureMap(로컬 파일 ID)을
// 직접 갱신한다 — 인스펙터 "Add Renderer Feature" 버튼이 하는 일과 동일. 멱등(이미 있으면 스킵).
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShowTime.EditorTools
{
    public static class M3FeatureInstaller
    {
        static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        [MenuItem("ShowTime/Install M3 Render Features")]
        public static void EnsureInstalled()
        {
            foreach (var path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null) { Debug.LogWarning("[M3] renderer not found: " + path); continue; }
                bool added = false;
                added |= AddIfMissing<SkillImpactFeature>(data, "Hidden/ShowTime/SkillImpact");
                added |= AddIfMissing<SkillRippleFeature>(data, "Hidden/ShowTime/SkillRipple");
                if (added) Debug.Log("[M3] features installed: " + path);
            }
            AssetDatabase.SaveAssets();
        }

        static bool AddIfMissing<T>(ScriptableRendererData data, string shaderName)
            where T : UnityEngine.Rendering.Universal.ScriptableRendererFeature
        {
            if (data.rendererFeatures.Any(f => f is T)) return false;

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = typeof(T).Name;
            // 셰이더를 직렬화 필드에 박아 빌드 포함을 보장 (Shader.Find는 에디터에서만 안전)
            var shaderField = typeof(T).GetField("shader");
            shaderField?.SetValue(feature, Shader.Find(shaderName));

            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets(); // 로컬 파일 ID 확정 (map에 필요)
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            int i = list.arraySize;
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = feature;
            var map = so.FindProperty("m_RendererFeatureMap");
            map.InsertArrayElementAtIndex(i);
            map.GetArrayElementAtIndex(i).longValue = localId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            return true;
        }
    }
}
