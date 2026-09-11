using System.Collections.Generic;
using System.IO;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 보스 화면 크기 — **벌은 256 이고 코드가 정수 2배로 키운다**
    /// (2026-09-10 사용자 확정).
    ///
    /// 여기서 재는 것은 **배율이 어디서 오는가**와 **키운 결과가 규격과 맞는가**다.
    /// 실제로 커 보이는지는 화면에서 사람이 본다(금요일 2차 육안).
    /// </summary>
    public sealed class BossViewScaleTests
    {
        /// <summary>
        /// **적 넷이 다 그림을 받았다** (2026-09-10 · 플랜 §66-16 · 촬영 B·C 구간 결함).
        ///
        /// 종전에는 <c>Bind</c> 에 그림 인자가 아예 없어 **적이 전부 색 사각**이었다.
        /// 승인·설치까지 끝난 벌이 폴더에 있는데 읽는 코드가 0건이었던 자리다.
        ///
        /// ⚠️ **벌 개수를 규격으로 재지는 않는다.** 방향과 상태가 아직 다 안 뽑혀 있고,
        /// 없는 방향은 스틸이 그대로 남는 것이 규정이다. 여기서 재는 것은 **자리가 찼는가**다.
        /// 폴더와 SO 가 어긋났는지는 <see cref="GeneratorWasRerunAfterInstall"/> 가 따로 본다.
        /// </summary>
        [Test]
        public void EveryEnemyGotItsArt()
        {
            foreach (string key in new[] { "infantry", "artillery", "armor", "boss" })
            {
                EnemyDefinition e = Load(key);
                Assert.NotNull(e, key);
                Assert.NotNull(e.sprite, $"{key} 스틸이 비었다 — 색 사각으로 떨어진다");
                Assert.IsNotEmpty(e.animClips, $"{key} 벌이 비었다");
            }
        }

        /// <summary>
        /// **설치했는데 생성기를 안 돌린 자리를 잡는다** (2026-09-11 신설).
        ///
        /// 아트가 벌 폴더를 새로 넣어도 `EnemyDefinition.animClips` 는 **생성기를 다시 돌려야**
        /// 바뀐다. 그 사이에는 **폴더에 있는 그림이 화면에 안 나온다** — 승인·설치까지 끝난 벌이
        /// 폴더에 있는데 읽는 코드가 0건이었던 09-10 의 자리와 **같은 뿌리**다.
        ///
        /// ⚠️ **자리 채움 시험은 이것을 못 본다.** `IsNotEmpty` 는 벌이 하나라도 있으면 통과한다.
        /// 그래서 **폴더 수와 벌 수가 같은가**를 잰다 — 어긋나면 「생성기를 돌려라」가 답이다.
        ///
        /// ⚠️ **서면은 세지 않는다.** 좌우 대칭 기체는 동면을 `flipX` 로 뒤집어 쓰므로
        /// 서면 폴더가 **없는 것이 규정**이고(`CombatEntityView.PlayState`), 생성기도 없는 것을
        /// 안 만든다. 폴더가 없으니 양쪽 셈에서 똑같이 빠진다.
        /// </summary>
        [Test]
        public void GeneratorWasRerunAfterInstall()
        {
            foreach (string key in new[] { "infantry", "artillery", "armor", "boss" })
            {
                EnemyDefinition e = Load(key);
                Assert.NotNull(e, key);

                List<string> folders = ClipFolders(ArtName(key));
                Assert.AreEqual(folders.Count, e.animClips.Count,
                    $"{key}: 폴더 {folders.Count}벌인데 SO 는 {e.animClips.Count}벌이다 — " +
                    "설치 뒤 'MBI/Generate Combat Data' 를 다시 돌려야 화면에 나온다. " +
                    "폴더: " + string.Join(", ", folders));
            }
        }

        /// <summary>
        /// **보스 서면 폴더는 없어야 한다** (2026-09-11 · 「서면은 코드 flipX 그대로」).
        ///
        /// 있으면 같은 그림이 두 벌이 되어 미러 규정이 죽고, 어느 쪽이 쓰이는지가
        /// 벌 목록 차례로 정해진다 — `UnitAnimWiringTests` 가 로봇에 거는 것과 같은 잣대다.
        /// </summary>
        [Test]
        public void BossWestIsMirrored_NotItsOwnFolder()
        {
            Assert.IsFalse(Directory.Exists($"{AnimRoot}/boss_Move/west"),
                "보스 서면은 동면을 flipX 로 뒤집어 쓴다 — 폴더를 만들면 안 된다");
        }

        private const string AnimRoot = "Assets/_Project/Art/Anim";

        /// <summary>적 키 → 아트 이름. 생성기의 `ArtNameFor` 와 **같은 표**여야 한다.</summary>
        private static string ArtName(string key)
        {
            switch (key)
            {
                case "infantry": return "mob_infantry";
                case "artillery": return "mob_cannon";
                case "armor": return "mob_armor";
                default: return "boss";
            }
        }

        /// <summary>그림이 실제로 든 벌 폴더만 센다 — 빈 폴더는 생성기도 건너뛴다.</summary>
        private static List<string> ClipFolders(string artName)
        {
            var found = new List<string>();
            if (!Directory.Exists(AnimRoot)) return found;

            foreach (string stateDir in Directory.GetDirectories(AnimRoot, artName + "_*"))
            foreach (string dirDir in Directory.GetDirectories(stateDir))
            {
                if (Directory.GetFiles(dirDir, "frame_*.png").Length == 0) continue;
                found.Add(Path.GetFileName(stateDir) + "/" + Path.GetFileName(dirDir));
            }
            found.Sort(System.StringComparer.Ordinal);
            return found;
        }

        /// <summary>
        /// **보스 스틸은 256 이어야 한다.** 512(`Units/boss.png`)를 걸면 캔버스만으로
        /// 이미 2.667칸인데 배율 2 가 또 곱해져 **5.33칸**이 된다 — 화면 절반을 덮는다.
        /// </summary>
        [Test]
        public void TheBossStillIsThe256Canvas_NotThe512One()
        {
            EnemyDefinition boss = Load("boss");
            Assert.NotNull(boss.sprite, "보스 스틸");

            Assert.AreEqual(ArtSpec.RobotCanvas, (int)boss.sprite.rect.width,
                "보스 전투 스틸은 256 이다 — 512 는 스틸·컷인용");
            Assert.AreEqual(ArtSpec.RobotCanvas, (int)boss.sprite.rect.height);
        }

        private const float D = 0.001f;
        private const string EnemiesDir = "Assets/_Project/ScriptableObjects/Enemies";

        private static EnemyDefinition Load(string key) =>
            AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EnemiesDir}/Enemy_{key}.asset");

        /// <summary>
        /// **보스만 2 이고 나머지는 1 이다.** 값이 SO 에 실려 있어야 코드가 배율을
        /// 직접 들고 있지 않다(지침 §3).
        /// </summary>
        [Test]
        public void OnlyTheBossIsScaled_AndItIsExactlyTwo()
        {
            EnemyDefinition boss = Load("boss");
            Assert.NotNull(boss, "먼저 'MBI/Generate Combat Data' 실행");
            Assert.AreEqual(2, boss.viewScale, "보스 배율 = 2");
            Assert.AreEqual(ArtSpec.BossViewScale, boss.viewScale, "SO 가 상수를 미러한다");

            foreach (string key in new[] { "infantry", "artillery", "armor" })
            {
                EnemyDefinition e = Load(key);
                Assert.NotNull(e, key);
                Assert.AreEqual(1, e.viewScale, $"{key} 는 키우지 않는다");
            }
        }

        /// <summary>
        /// **로봇은 1 이다** — 256 캔버스가 그대로 1.333칸이라 키울 것이 없다.
        /// 보스와 로봇이 같은 256 을 쓰면서 화면 크기가 갈리는 것이 이번 결정의 전부다.
        /// </summary>
        [Test]
        public void TheRobotIsNotScaled_ThoughItSharesTheSameCanvas()
        {
            Assert.AreEqual(1.333f, ArtSpec.RobotSize, D, "로봇 256px = 1.333칸");
            Assert.AreEqual(ArtSpec.RobotSize, ArtSpec.WorldSize(ArtSpec.RobotCanvas), D,
                "로봇은 캔버스 그대로다 — 배율이 끼지 않는다");

            Assert.AreEqual(ArtSpec.RobotSize * ArtSpec.BossViewScale, ArtSpec.BossViewSize, D,
                "보스 = 같은 캔버스 × 배율");
        }

        /// <summary>
        /// **정수배만 쓴다.** 1.5 같은 값을 곱하면 도트가 이웃 픽셀에 반씩 걸쳐
        /// 실루엣 가장자리가 뭉개진다 — 2 배는 한 픽셀이 정확히 네 픽셀이 된다.
        /// </summary>
        [Test]
        public void TheScaleIsAWholeNumber_AndAtLeastOne()
        {
            Assert.GreaterOrEqual(ArtSpec.BossViewScale, 1, "0 이나 음수면 보스가 사라진다");
            Assert.AreEqual(ArtSpec.BossViewScale, (int)ArtSpec.BossViewScale, "정수다");
        }

        /// <summary>
        /// **키운 결과가 종전 자리와 같다** — 2.667칸.
        ///
        /// ⚠️ **숫자가 같은 것은 우연이 아니라 노린 것이다.** 종전에는 512 벌을 전제로
        /// <c>LargeSize</c> 를 썼고 그림자·HP 바·충돌 반경이 전부 그 값을 공유했다.
        /// 벌을 256 으로 가면서 **그 자리를 안 흔들려고** 배율을 2 로 골랐다 —
        /// 셋을 따로 고칠 필요가 없어진다.
        /// </summary>
        [Test]
        public void ScalingUpLandsOnTheSameFootprintAsBefore()
        {
            Assert.AreEqual(2.667f, ArtSpec.BossViewSize, D);
            Assert.AreEqual(ArtSpec.LargeSize, ArtSpec.BossViewSize, D,
                "구 512 전제와 같은 자리 — 그림자·HP 바·충돌 반경이 안 흔들린다");
        }
    }
}
