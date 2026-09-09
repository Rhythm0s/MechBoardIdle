using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 소리 임포트 규격 강제 (2026-09-09 신설 · <see cref="SpriteImportRules"/>와 같은 패턴).
    ///
    /// 사운드 문서 7장이 **적재 방식을 둘로 갈랐다** — 효과음은 메모리에 올려 두고,
    /// 배경 음악은 흘려 재생한다. 그 둘을 손으로 지정하면 **한 번 빠뜨린 파일만** 틀어지고,
    /// 틀어진 것은 눈으로 안 잡힌다 — 소리는 나기 때문이다.
    ///
    /// ⚠️ **`.meta`****를 손으로 만들지 않는다.** `Assets/_Project/Audio/`는 아트 세션 소유라
    /// 그 아래 파일을 문서·코드 세션이 만들거나 고치지 않는다(소유 표 §20-1).
    /// **임포터가 읽는 값을 코드가 정하는 것**은 그 금지에 안 걸린다 —
    /// 사람이 파일을 고치는 것이 아니라 규격이 한 곳에 적혀 있는 것이다.
    ///
    /// ⚠️ **소리 자산 대장이 이 자리를 구현 몫으로 명시했다** — 「`Load Type`·`Compression
    /// Format`·`Preload`를 정한 자리가 없다. 구현-문서·코드 세션이 정할 자리이며,
    /// 아트는 파일과 대장까지만 낸다.」
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string AudioRoot = "Assets/_Project/Audio";
        private const string BgmDir = AudioRoot + "/bgm";
        private const string SfxDir = AudioRoot + "/sfx";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot)) return;

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;

            if (assetPath.StartsWith(BgmDir))
            {
                // 배경 음악 = 흘려 재생. 176초짜리를 통째로 풀어 두면 WebGL 메모리를 먹는다.
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                // ⚠️ **`preloadAudioData`는 임포터가 아니라 샘플 설정에 있다** — 그쪽 속성은
                // 2026 시점에 폐기됐고 플랫폼별 값으로 옮겨졌다. 옛 자리에 쓰면 컴파일이 막힌다.
                s.preloadAudioData = false;
            }
            else if (assetPath.StartsWith(SfxDir))
            {
                // 효과음 = 메모리 적재. 0.5초짜리가 초당 여러 번 나므로 **풀어 두는 값이
                // 재생 지연보다 싸다.** 압축을 풀어 두는 것이 `Decompress on load`다.
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.PCM;
                s.preloadAudioData = true;
            }
            else
            {
                // Audio/ 아래지만 둘 중 어디도 아니면 건드리지 않는다 —
                // 모르는 자리에 규격을 밀어 넣지 않는다.
                return;
            }

            importer.defaultSampleSettings = s;

            // **효과음만 모노로 내린다** (2026-09-09 · 사운드 리스트가 효과음을 모노로 적었다).
            // 조달된 여덟이 스테레오로 왔는데 **파일은 안 건드린다** — 출처 규약이 개변본
            // 재배포까지 막고 있고, 무엇보다 **임포트 규격의 소스는 이 코드**다(2026-09-09 규칙 개정).
            // 파일을 고치면 규격이 두 곳에 살고, 다시 받은 사람은 스테레오를 쓰게 된다.
            //
            // ⚠️ **BGM 은 스테레오 그대로다** — 곡은 모노로 내리면 넓이가 사라진다.
            importer.forceToMono = assetPath.StartsWith(SfxDir);
            importer.loadInBackground = assetPath.StartsWith(BgmDir);
        }

        /// <summary>
        /// 이미 들어와 있는 파일에도 규격을 먹인다.
        ///
        /// **왜 필요한가.** 위 `OnPreprocessAudio`는 **임포트할 때만** 돈다. BGM 둘은
        /// 이 규칙보다 **먼저** 리포에 들어왔으므로(아트 `3658409`), 이 메뉴를 한 번
        /// 돌려야 규격이 붙는다. 안 돌리면 「규칙은 있는데 안 걸린 파일」이 남는다 —
        /// 지침 §7 ［08-30］「클래스는 있는데 호출자 0건」과 같은 모양이다.
        /// </summary>
        [MenuItem("MBI/Reimport Audio (규격 적용)")]
        public static void ReimportAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),
                    ImportAssetOptions.ForceUpdate);

            Debug.Log($"[MBI] 소리 자산 {guids.Length}개에 임포트 규격을 다시 먹였다 " +
                      $"(bgm = Streaming · sfx = Decompress on load).");
        }
    }
}
