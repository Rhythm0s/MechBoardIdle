using System.IO;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 회피 줄기의 **가산 합성 머티리얼**을 자산으로 굽는다 (2026-09-18 · 설계 지적 뒤).
    ///
    /// ⚠️⚠️ **왜 자산이어야 하는가.** <c>Shader.Find("Particles/Additive")</c> 는
    /// **에디터에서는 찾아지고 웹빌드에서는 null** 이 된다 — 그 셰이더를 무는 머티리얼이
    /// 하나도 없으면 빌드가 통째로 **스트립**하기 때문이다. 그래서 코드가 이름으로 찾는 한
    /// 「에디터에서는 빛나고 빌드에서는 안 빛나는」 갈림이 영영 남는다.
    ///
    /// 📌 **자산 하나가 그 갈림을 없앤다.** `Resources/` 에 놓인 머티리얼은 셰이더를 물고
    /// 있으므로 빌드가 **둘 다 싣는다.** 프로젝트 설정(Always Included Shaders)을 고치는 길도
    /// 있지만, 그쪽은 **리포 밖 설정 파일**이라 왜 그 줄이 있는지가 코드에서 안 보인다 —
    /// 자산은 눈에 보이고 지우면 폴백이 도로 선다.
    ///
    /// ⚠️ **없어도 안 깨진다** — `DodgeTrail` 이 자산을 못 찾으면 보통 알파로 그린다.
    /// </summary>
    public static class DodgeStreakMaterialGenerator
    {
        /// <summary>`Resources` 기준 이름 — 부르는 쪽과 같은 자리에 적는다.</summary>
        public const string ResourceName = "DodgeStreakAdditive";

        private const string Dir = "Assets/_Project/Resources";

        /// <summary>
        /// 굽는다. **이미 있으면 셰이더만 다시 맞춘다** — 자산을 지웠다 만들면 참조가 끊긴다.
        /// </summary>
        [MenuItem("MBI/회피 줄기 머티리얼 굽기")]
        public static void Generate()
        {
            // ⚠️ 이름이 버전마다 다르다 — **있는 것을 찾아 쓰고, 무엇을 골랐는지 찍는다.**
            //    지어낸 이름 하나만 박아 두면 다음 유니티에서 조용히 null 이 된다.
            string[] names =
            {
                "Particles/Additive",
                "Legacy Shaders/Particles/Additive",
                "Mobile/Particles/Additive",
            };

            Shader shader = null;
            string picked = null;
            foreach (string n in names)
            {
                shader = Shader.Find(n);
                if (shader != null) { picked = n; break; }
            }

            if (shader == null)
            {
                // **없는 것을 있는 척하지 않는다** — 자산을 안 만들면 런타임이 폴백으로 간다.
                Debug.LogWarning("[MBI] 가산 셰이더를 못 찾았다 — 줄기는 보통 알파로 그려진다. "
                                 + "(찾아본 이름: " + string.Join(" · ", names) + ")");
                return;
            }

            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            string path = Dir + "/" + ResourceName + ".mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = ResourceName };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MBI] 회피 줄기 머티리얼 — " + path + " · 셰이더 「" + picked + "」");
        }
    }
}
