using MBI.Core.Audio;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **생성기는 남의 자산을 안 건드린다** (2026-09-15 · 등재된 함정의 세 번째 재발을 끊는다).
    ///
    /// ⚠️ **무슨 일이 있었나.** `CombatAssetGenerator` 가 효과음을 `.wav` 로 찾고
    /// `AudioAssetGenerator` 는 `.ogg` 로 꽂아서, 전투 생성기를 돌릴 때마다
    /// **배선 여덟이 null 로 덮여 날아갔다**(09-14 · 09-15 두 번 · HANDOFF 등재).
    /// 적어 둔 것이 세 번째를 못 막아서 코드로 막는다.
    ///
    /// ⚠️ **이 시험은 자산을 실제로 건드리지 않는다** — 생성기를 돌리지 않고
    /// **소유 경계 자체**를 본다. 생성기를 돌리는 시험은 리포 상태를 바꿔 다른 시험을
    /// 흔들고, CI 에서 자산이 없는 클론이면 통째로 빨개진다.
    /// </summary>
    public sealed class GeneratorOwnershipTests
    {
        private const string AudioConfigPath =
            "Assets/_Project/ScriptableObjects/AudioConfig.asset";

        [Test]
        public void 전투_생성기는_AudioConfig_를_안_쓴다()
        {
            // 소스에 손이 닿는 자리가 남아 있으면 언젠가 다시 부른다.
            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Editor/CombatAssetGenerator.cs");

            Assert.That(src, Does.Not.Contain("LoadOrCreate<AudioConfig>"),
                "전투 생성기가 AudioConfig 를 만들거나 덮으면 사운드 배선이 날아간다");
            Assert.That(src, Does.Not.Contain("audio.sfxClips"),
                "효과음 칸은 AudioAssetGenerator 만 채운다");
            Assert.That(src, Does.Not.Contain("Audio/sfx/"),
                "효과음 경로가 두 곳에 살면 한쪽이 바뀔 때 다른 쪽이 조용히 어긋난다");
        }

        private const string SfxDir = "Assets/_Project/Audio/sfx";

        [Test]
        public void 소리_배선의_주인은_하나다()
        {
            string owner = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Editor/AudioAssetGenerator.cs");

            // 곡 둘도 주인이 꽂는다 — 종전에는 전투 생성기가 꽂았다.
            Assert.That(owner, Does.Contain("musicBattle"));
            Assert.That(owner, Does.Contain("musicBoss"));
            Assert.That(owner, Does.Contain("Audio/sfx"));
        }

        [Test]
        public void 지금_꽂혀_있는_배선이_온전하다()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(AudioConfigPath);
            if (config == null) Assert.Ignore("AudioConfig 가 없다 — 자산 생성 전 클론");

            Assert.That(config.sfxClips, Is.Not.Null, "효과음 배열 자체가 없으면 재생기가 통째로 논다");
            Assert.That(config.sfxClips.Length, Is.EqualTo(SoundIds.All.Length),
                "길이가 짧으면 뒤쪽 소리가 조용히 사라진다");

            // ⚠️ **꽂힘 수는 단정하지 않는다** — 효과음 파일은 리포 밖이라
            // 새 클론에서는 비는 것이 정상이다. 여기서 「아홉이어야 한다」로 적으면
            // 남의 클론에서 늘 빨간 시험이 된다.
            //
            // ⚠️ **「전부 아니면 전무」도 틀렸다**(2026-09-15 정정). 종전에는 이름 여덟에
            // 파일 여덟이라 그 둘이 같았다. 지금은 `sfx_ui_click` 처럼 **이름만 정하고
            // 파일은 아직 조달 전**인 소리가 있어서, 부분 배선이 정상 상태가 된다.
            //
            // 그래서 **디스크에 있는 파일 수**와 맞춘다 — 이러면 원래 잡으려던 것
            // (파일이 멀쩡히 있는데 칸이 빈 것 = 생성기가 덮었다)은 그대로 잡힌다.
            int wired = 0;
            foreach (AudioClip c in config.sfxClips) if (c != null) wired++;

            int onDisk = 0;
            foreach (string id in SoundIds.All)
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + id + ".ogg") != null) onDisk++;

            Assert.That(wired, Is.EqualTo(onDisk),
                $"파일 {onDisk}개 중 {wired}칸만 꽂혔다 — 생성기가 배선을 덮었다는 뜻이다 "
                + "(09-14·09-15 의 그 증상). 조달 전이라 파일 자체가 없는 소리는 여기 안 센다");
        }
    }
}
