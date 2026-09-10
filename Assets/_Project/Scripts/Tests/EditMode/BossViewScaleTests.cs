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
