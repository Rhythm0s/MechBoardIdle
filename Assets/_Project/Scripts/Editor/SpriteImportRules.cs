using System.IO;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 도트 스프라이트 임포트 규격 강제(260824_V02 §4 승인).
    ///
    /// 왜 사람이 맞추지 않는가: 값만 정해 두면 임포트마다 손으로 지정해야 하고,
    /// 한 번 빠뜨리면 **그 스프라이트만** 틀어진다. 틀어진 것은 눈으로 잘 안 잡힌다 —
    /// PPU가 하나만 다르면 그 오브젝트의 크기가 미묘하게 어긋날 뿐 에러가 나지 않기 때문이다.
    /// 규격을 코드에 한 번 적고 이후 교체는 파일만 넣게 만든다.
    ///
    /// 적용 대상: Assets/_Project/Art 이하 텍스처. 그 밖은 건드리지 않는다
    /// (URP·패키지 텍스처까지 Point 필터로 바꾸면 안 된다).
    /// </summary>
    public sealed class SpriteImportRules : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ArtSpec.PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f); // Center — 현 전 스프라이트와 동일
            importer.filterMode = FilterMode.Point;         // 도트 1:1에서 보간이 들어가면 픽셀이 뭉갠다
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // spritePivot은 Alignment가 Custom일 때만 반영된다.
            TextureImporterSettings s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;

            // ⚠️ **UI 그릇은 9-슬라이스 여백이 있어야 한다** (2026-09-11 · `260911_W02` 9장).
            //
            // 아트가 그림에 여백 16 을 그려 넣어도 **임포터가 0 이면 늘릴 때 통짜로 늘어난다** —
            // 모서리가 뭉개지고 테두리가 굵어진다. 그 값을 **여기서 강제하는 이유**는 이 파일이
            // 임포트 규격의 소스이기 때문이다(아트가 `.meta` 를 손으로 고치면 다음 재임포트에
            // 지워진다 · 클래스 요약의 「파일만 넣게 만든다」가 그 뜻이다).
            //
            // ⚠️ **IMGUI 는 이 값을 안 본다** — `UiSkin` 은 `GUIStyle.border` 를 쓴다.
            // 그래도 넣는 이유는 **자산이 스스로 제 여백을 말해야** 하기 때문이다:
            // 생성기가 이 값을 읽어 `UiSkinAssets.border` 에 넣으므로, 그림이 바뀌면
            // **코드 상수를 안 고쳐도 따라온다.**
            if (ArtSpec.IsUiPlatePath(assetPath))
            {
                int b = ArtSpec.UiPlateBorder;
                s.spriteBorder = new Vector4(b, b, b, b);
            }

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// 보드용(Nodes·Belts) 스프라이트는 캔버스가 **192의 배수**여야 한다(V02 §4 단서).
        /// 한 칸 = 192px이므로 배수가 아니면 타일이 격자에 딱 떨어지지 않는다.
        /// 임포트를 막지는 않고 경고만 낸다 — 작업 중인 임시 파일까지 튕기면 아트 반복이 느려진다.
        /// </summary>
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(ArtRoot) || !ArtSpec.IsBoardArtPath(assetPath)) return;
            if (texture == null) return;

            if (!IsTileMultiple(texture.width) || !IsTileMultiple(texture.height))
            {
                Debug.LogWarning(
                    $"[아트 규격] {Path.GetFileName(assetPath)} 캔버스 {texture.width}×{texture.height} — " +
                    $"보드용 스프라이트는 {ArtSpec.TileCanvas}의 배수여야 격자 한 칸에 맞는다.");
            }
        }

        private static bool IsTileMultiple(int px) => px > 0 && px % ArtSpec.TileCanvas == 0;
    }
}
