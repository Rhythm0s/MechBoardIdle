using System.IO;
using System.Text;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 품목 그림이 **캔버스에서 실제로 차지하는 비율**을 재서 <see cref="BoardArtSet"/> 에 넣는다
    /// (2026-09-15 · 사용자 육안 4차 ⑥).
    ///
    /// **왜 필요한가.** 벨트 위 품목의 크기를 `Sprite.bounds` 로 맞추고 있었는데
    /// 그것은 **캔버스**이지 **그림**이 아니다. 캔버스를 다 쓰는 그림과 구석에만 있는 그림이
    /// 같은 대접을 받아, 후자가 **점처럼 작게** 그려졌다 — 09-15 「주황 얼룩」의 원인이다.
    ///
    /// ⚠️ **눈으로는 못 가른다.** 둘 다 「그림이 나온다」이고 에러도 없다. 작은 쪽은
    /// 그냥 **작게** 나올 뿐이라, 화면을 봐도 「이 그림이 원래 이런가 보다」로 읽힌다.
    /// 그래서 재야 한다.
    ///
    /// **어떻게 재는가.** PNG 원본을 직접 읽어 알파가 있는 화소의 경계 상자를 구하고,
    /// 긴 변을 캔버스 변으로 나눈다. ⚠️ **텍스처 임포트 설정을 안 건드린다** —
    /// `isReadable` 을 켜면 메모리가 늘고 빌드에 실린다. 파일에서 직접 읽으면 그럴 일이 없다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.Editor.ItemArtSpanGenerator.Generate</c>
    /// </summary>
    public static class ItemArtSpanGenerator
    {
        private const string ArtSetPath = "Assets/_Project/ScriptableObjects/BoardArtSet.asset";

        /// <summary>이보다 알파가 낮으면 안 그려진 것으로 본다.</summary>
        private const byte AlphaFloor = 10;

        [MenuItem("MBI/Measure Item Art Spans")]
        public static void Generate()
        {
            var set = AssetDatabase.LoadAssetAtPath<BoardArtSet>(ArtSetPath);
            if (set == null)
            {
                Debug.LogError("[MBI] BoardArtSet 이 없다 — " + ArtSetPath);
                return;
            }

            var sb = new StringBuilder("[MBI] 품목 그림 차지 비율 실측" + System.Environment.NewLine);
            int measured = 0, missing = 0;

            for (int i = 0; i < set.items.Count; i++)
            {
                BoardArtSet.ItemArt it = set.items[i];
                if (it.sprite == null) { missing++; continue; }

                float span = MeasureSpan(AssetDatabase.GetAssetPath(it.sprite));
                if (span <= 0f) { missing++; continue; }

                it.contentSpan = span;
                set.items[i] = it;
                measured++;

                // 많이 비어 있는 그림은 지목한다 — 고칠 자리는 아트다.
                string flag = span < 0.75f ? "  <- 캔버스를 많이 남긴다" : "";
                sb.AppendLine($"  {it.kind} ({(int)it.kind}) · {it.sprite.name} · {span:F3}{flag}");
            }

            // ⚠️⚠️ **`EditorApplication.Exit` 를 여기서 부르지 않는다**(2026-09-15 · 실측으로 당했다).
            //
            // 처음에는 다른 프로브처럼 끝에 `Exit(0)` 을 넣었다. 로그에는 잰 값이 **전부
            // 찍혔는데 자산에는 0 만 남았다** — 에디터를 그 자리에서 죽이면 **디스크 쓰기가
            // 끝나기 전에 끊긴다.** `Temp/` 가 종료 때 지워지던 것과 같은 종류의 함정이다.
            //
            // 📌 **재는 도구와 쓰는 도구는 다르다.** 프로브는 값을 **찍기만** 하니 죽어도 되고,
            // 생성기는 **남겨야** 하니 죽으면 안 된다. 종료는 `-quit` 에 맡긴다.
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            AssetDatabase.SaveAssets();

            sb.AppendLine($"  잰 것 {measured} · 못 잰 것 {missing}");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// PNG 한 장의 「긴 변 차지 비율」. 못 읽으면 0.
        ///
        /// ⚠️ **긴 변 기준이다** — 크기를 맞추는 쪽(`FitScale`)이 긴 변을 쓰기 때문이다.
        /// 두 곳이 다른 기준을 쓰면 값이 맞아도 그림이 틀어진다.
        /// </summary>
        public static float MeasureSpan(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return 0f;

            var tex = new Texture2D(2, 2);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(assetPath))) return 0f;

                int w = tex.width, h = tex.height;
                Color32[] px = tex.GetPixels32();

                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < AlphaFloor) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

                if (maxX < 0) return 0f;   // 통째로 투명하다

                float content = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
                float canvas = Mathf.Max(w, h);
                return Mathf.Clamp01(content / canvas);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
