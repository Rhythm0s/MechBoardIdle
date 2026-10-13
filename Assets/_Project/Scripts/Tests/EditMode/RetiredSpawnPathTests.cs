using System.IO;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **폐기한 스폰 길을 게임 코드가 안 쓴다** (2026-10-13 사용자 확정 #97).
    ///
    /// ⚠️⚠️ **왜 시험이 필요한가.** 적을 만드는 길이 둘이었고, 둘이 각자 `EnemySpawn` 의
    /// 칸을 옮겨 적다가 `isBoss` 가 한쪽에서 빠졌다 — 보스가 보스로 안 섰다(⑫).
    /// 지금은 `MakeEnemy` 하나가 두 자리를 다 들지만, **아무도 안 쓰는 길이 남아 있으면
    /// 다음 사람이 그 길로 다시 들어온다.** 폐기는 삭제가 아니므로 메서드는 남아 있고,
    /// 남은 것을 게임 코드가 다시 부르기 시작하면 여기서 빨개진다.
    ///
    /// 📌 시험은 불러도 된다 — 재는 것이 일이다.
    /// </summary>
    public sealed class RetiredSpawnPathTests
    {
        private const string ScriptRoot = "Assets/_Project/Scripts";
        private const string Retired = "SpawnBatch";

        /// <summary>선언한 그 파일만 빼고, 시험 밖 어디에서도 안 불린다.</summary>
        [Test]
        public void 게임_코드가_폐기한_스폰_길을_안_부른다()
        {
            var offenders = new System.Collections.Generic.List<string>();

            foreach (string file in Directory.GetFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace(Path.DirectorySeparatorChar, '/');
                if (norm.Contains("/Tests/")) continue;                 // 시험은 불러도 된다
                if (norm.EndsWith("/Core/Combat/CombatSimulation.cs")) continue; // 선언한 자리

                string src = File.ReadAllText(file);
                if (src.Contains(Retired + "(")) offenders.Add(norm);
            }

            Assert.IsEmpty(offenders,
                "폐기한 " + Retired + " 을 게임 코드가 부른다 — 적 만드는 길이 다시 둘이 된다: "
                + string.Join(", ", offenders));
        }

        /// <summary>
        /// ⚠️ **폐기 표기가 붙어 있는가.** 표기가 사라지면 다음 사람은 이것을 살아 있는
        /// 길로 읽는다 — 폐기는 지우는 것이 아니라 **적어 두는 것**이다.
        /// </summary>
        [Test]
        public void 폐기_표기가_붙어_있다()
        {
            string src = File.ReadAllText(ScriptRoot + "/Core/Combat/CombatSimulation.cs");
            int at = src.IndexOf("public void " + Retired + "(", System.StringComparison.Ordinal);
            Assert.Greater(at, 0, Retired + " 선언을 못 찾았다 — 이름이 바뀌었나");

            // 선언 바로 위 주석 덩어리에 폐기 표기가 있어야 한다.
            string above = src.Substring(0, at);
            int doc = above.LastIndexOf("/// <summary>", System.StringComparison.Ordinal);
            Assert.Greater(doc, 0, Retired + " 에 주석이 없다");

            StringAssert.Contains("폐기", above.Substring(doc),
                Retired + " 에 폐기 표기가 없다 — 살아 있는 길로 읽힌다");
        }
    }
}
