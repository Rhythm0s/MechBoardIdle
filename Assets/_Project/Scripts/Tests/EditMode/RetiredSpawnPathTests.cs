using System.IO;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **적을 만드는 길이 둘이어도 칸은 한 곳에서 베낀다** (2026-10-13).
    ///
    /// 📜 **이 시험은 지키는 것을 한 번 바꿨다.**
    /// · 10-13 낮: `SpawnBatch` 가 **폐기**였다 — 그때는 「게임 코드가 안 부른다」를 지켰다(#97).
    /// · 10-13 밤: 상주 파밍 층이 서면서 **되살아났다**(#111). 「안 부른다」는 이제 거짓이고,
    ///   그것을 그대로 두면 **시험이 거짓을 지키게 된다.**
    ///
    /// ⚠️⚠️ **그래서 지킬 것을 옮겼다.** 위험은 「길이 둘이다」가 아니라
    /// **「길이 둘인데 칸을 각자 베낀다」**였다 — 종전에 그래서 `isBoss` 가 한쪽에서 빠졌고
    /// 보스가 보스로 안 섰다(⑫). 지금은 <c>MakeEnemy</c> 하나가 두 자리를 다 든다.
    /// **그 하나가 둘로 갈라지는 순간 여기서 빨개진다.**
    ///
    /// 📌 칸이 **실제로** 건너가는지는 `EnemySpawnFieldsTests` 가 두 길 모두에서 잰다.
    /// 여기는 **구조**를 지킨다 — 베끼는 자리가 하나인가.
    /// </summary>
    public sealed class RetiredSpawnPathTests
    {
        private const string Sim =
            "Assets/_Project/Scripts/Core/Combat/CombatSimulation.cs";

        /// <summary>적을 짓는 자리는 <c>MakeEnemy</c> **하나**다.</summary>
        [Test]
        public void 적을_짓는_자리가_하나다()
        {
            string src = File.ReadAllText(Sim);

            // ⚠️ **로봇은 세지 않는다.** `new CombatEntity` 는 둘인데 하나는 로봇이고,
            //    ⑫ 가 난 자리는 **적** 쪽이다. 적을 짓는 자리만 센다.
            int made = Count(src, "faction = Faction.Enemy");
            Assert.AreEqual(1, made,
                "적을 짓는 자리가 " + made + " 곳이다 — 칸을 각자 베끼면 ⑫ 가 되돌아온다. "
                + "MakeEnemy 하나로 모으라.");
        }

        /// <summary>
        /// 두 길이 **그 하나를 지난다** — 묶음(파밍)도 줄줄이(도전)도 `MakeEnemy` 를 부른다.
        /// </summary>
        [Test]
        public void 두_길이_같은_자리를_지난다()
        {
            string src = File.ReadAllText(Sim);
            Assert.GreaterOrEqual(Count(src, "MakeEnemy("), 3,
                "MakeEnemy 를 부르는 곳이 모자란다 — 선언 하나 + 두 길(묶음·줄줄이)이라야 한다");
        }

        /// <summary>
        /// ⚠️ 되살아난 길이 **정말 살아 있는가** — 폐기 표기가 남아 있으면 다음 사람이
        /// 「안 쓰는 길」로 읽고 지운다.
        /// </summary>
        [Test]
        public void 되살아난_길에_폐기_표기가_없다()
        {
            string src = File.ReadAllText(Sim);
            int at = src.IndexOf("public void SpawnBatch(", System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "SpawnBatch 선언을 못 찾았다 — 이름이 바뀌었나");

            string above = src.Substring(0, at);
            int doc = above.LastIndexOf("/// <summary>", System.StringComparison.Ordinal);
            Assert.Greater(doc, 0, "SpawnBatch 에 주석이 없다");

            string block = above.Substring(doc);
            StringAssert.DoesNotContain("🗑️🗑️ **폐기", block,
                "되살렸는데 폐기 표기가 남아 있다 — 다음 사람이 안 쓰는 길로 읽는다");
        }

        private static int Count(string src, string needle)
        {
            int n = 0, i = 0;
            while ((i = src.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }
    }
}
