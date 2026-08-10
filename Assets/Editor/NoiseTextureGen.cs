// 디졸브용 밸류 노이즈 텍스처를 코드로 생성하는 툴 (외부 에셋 0 원칙).
// 격자점에 결정론적 해시 난수를 놓고 스무스 보간 + 4옥타브 합성 = 구름무늬.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShowTime.EditorTools
{
    public static class NoiseTextureGen
    {
        public const string AssetPath = "Assets/_Project/Textures/ValueNoise256.png";
        const int Size = 256;

        [MenuItem("ShowTime/Generate Noise Texture")]
        public static Texture2D Generate()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0f, amp = 0.5f;
                for (int oct = 0; oct < 4; oct++)          // 4옥타브: 큰 얼룩 + 잔결
                {
                    float freq = 4f * (1 << oct);
                    v += amp * ValueNoise(x / (float)Size * freq, y / (float)Size * freq, freq);
                    amp *= 0.5f;
                }
                tex.SetPixel(x, y, new Color(v, v, v));
            }
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.WriteAllBytes(AssetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(AssetPath);

            // 데이터 텍스처 임포트 설정: sRGB 해제(감마 보정 없이 값 그대로), 반복 랩
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            Debug.Log("[NoiseTextureGen] generated: " + AssetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        }

        public static Texture2D Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
            return existing != null ? existing : Generate();
        }

        // 격자 밸류 노이즈: 정수 격자점의 해시 난수를 smoothstep 보간
        static float ValueNoise(float x, float y, float wrap)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = Smooth(x - x0), ty = Smooth(y - y0);
            // wrap으로 나머지 연산 → 텍스처가 이음매 없이(seamless) 반복되게
            float a = Hash(Mod(x0, wrap), Mod(y0, wrap));
            float b = Hash(Mod(x0 + 1, wrap), Mod(y0, wrap));
            float c = Hash(Mod(x0, wrap), Mod(y0 + 1, wrap));
            float d = Hash(Mod(x0 + 1, wrap), Mod(y0 + 1, wrap));
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t); // smoothstep 곡선
        static int Mod(int v, float m) => (int)((v % m + m) % m);

        // 셰이더에서 흔히 쓰는 sin 기반 해시의 C# 버전 — 시드 고정 = 결정론
        static float Hash(int x, int y)
        {
            float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }
    }
}
