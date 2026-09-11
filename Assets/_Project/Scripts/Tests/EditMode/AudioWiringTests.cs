using System.IO;
using MBI.Core.Audio;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 효과음 배선 (2026-09-11 신설 · 플랜 §71-33 ① · 아트 실측 O-1).
    ///
    /// **무엇을 지키는가.** 「파일은 있는데 칸이 비어 있다」를 잡는다.
    /// 09-11 에 여덟 파일이 다 있는데 `sfxClips` 여덟이 전부 `fileID 0` 이었고,
    /// **에러가 하나도 안 났다** — 재생기가 `clip == null` 을 조용히 건너뛴다.
    /// **안 나는 소리는 스스로 알려 주지 않는다.**
    ///
    /// ⚠️ **파일이 없는 것은 실패가 아니다.** 효과음은 출처 규약(재배포 금지)으로 리포 밖이라
    /// 새 클론에서는 없는 것이 정상이다 — 그때는 **건너뛴다.** 늘 빨간 시험은 안 본다.
    /// </summary>
    public sealed class AudioWiringTests
    {
        private const string SfxDir = "Assets/_Project/Audio/sfx";
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/AudioConfig.asset";

        private static AudioConfig Config =>
            AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);

        [Test]
        public void 칸_수가_소리_수와_같다()
        {
            AudioConfig c = Config;
            Assert.NotNull(c, "AudioConfig 가 있어야 한다");

            // ⚠️ 짧으면 `SfxPlayer` 가 `Min(All.Length, sfxClips.Length)` 로 잘라
            // **뒤쪽 소리가 조용히 사라진다.**
            Assert.AreEqual(SoundIds.All.Length, c.sfxClips.Length,
                "칸은 SoundIds.All 과 **차례로** 맞물린다 — 길이가 다르면 뒤가 잘린다");
        }

        /// <summary>
        /// **파일이 있으면 칸도 차 있어야 한다** — 이것이 09-11 을 잡는 시험이다.
        /// </summary>
        [Test]
        public void 파일이_있으면_칸도_차_있다()
        {
            AudioConfig c = Config;
            Assert.NotNull(c);

            var onDisk = new System.Collections.Generic.List<string>();
            var empty = new System.Collections.Generic.List<string>();

            for (int i = 0; i < SoundIds.All.Length && i < c.sfxClips.Length; i++)
            {
                string id = SoundIds.All[i];
                if (!File.Exists($"{SfxDir}/{id}.ogg")) continue;

                onDisk.Add(id);
                if (c.sfxClips[i] == null) empty.Add(id);
            }

            // 리포 밖 자산이라 **없는 것이 정상인 클론**이 있다 — 그때는 볼 것이 없다.
            if (onDisk.Count == 0)
                Assert.Ignore("효과음 파일이 없다 — 리포 밖 자산이다(Docs/audio_manifest.md 로 재조달).");

            Assert.IsEmpty(empty,
                "파일은 있는데 칸이 비었다 — 「MBI/Generate Audio Wiring」 을 돌린다: "
                + string.Join(", ", empty));
        }

        /// <summary>
        /// **차례가 곧 배선이다** — 한 칸만 밀려도 **발사에서 병목 소리**가 난다.
        /// 꽂힌 클립의 이름이 그 자리의 소리 이름과 같은지 본다.
        /// </summary>
        [Test]
        public void 칸과_이름이_안_밀렸다()
        {
            AudioConfig c = Config;
            Assert.NotNull(c);

            int checkedCount = 0;
            for (int i = 0; i < SoundIds.All.Length && i < c.sfxClips.Length; i++)
            {
                AudioClip clip = c.sfxClips[i];
                if (clip == null) continue;

                Assert.AreEqual(SoundIds.All[i], clip.name,
                    $"{i}번 칸에 다른 소리가 꽂혔다 — 차례가 밀리면 발사에서 병목이 난다");
                checkedCount++;
            }

            if (checkedCount == 0) Assert.Ignore("꽂힌 클립이 없다 — 리포 밖 자산이다.");
        }
    }
}
