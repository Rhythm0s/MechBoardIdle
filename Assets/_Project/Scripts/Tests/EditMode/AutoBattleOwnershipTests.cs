using System.Collections.Generic;
using MBI.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 자동 전투는 **제 목록의 판만** 몬다 (2026-09-15 오후 · 육안 ③④).
    ///
    /// ⚠️⚠️ **왜 이것이 필요했나.** 씬은 러너에 튜토리얼(S0)을 꽂고 `Stage0Session` 이
    /// 그것을 몰고 간다. 그런데 `AutoBattleController` 는 S0 이 끝나는 것을 보고
    /// **제 목록(S1~Sn)의 다음 판을 러너에 덮어썼다** — 튜토리얼 밑에서 판이 바뀐다.
    ///
    /// 화면에서는 **S2 전투 위에 튜토리얼 체크리스트가 남고**(③), 튜토리얼이 영영
    /// 안 끝나니 비워 둔 칸 (6,5) 가 안 채워져 **73 초에 창고 0/40 · 저장고 0.0 발/초**
    /// 였다(④). 에러는 한 줄도 안 났다 — `runner.stage` 의 **주인이 둘**이었을 뿐이다.
    ///
    /// 📌 여기서 지키는 것은 하나다 — **목록에 없는 판은 내 판이 아니다.**
    /// </summary>
    public sealed class AutoBattleOwnershipTests
    {
        private static StageDefinition Stage(string id)
        {
            var s = ScriptableObject.CreateInstance<StageDefinition>();
            s.stageId = id;
            return s;
        }

        [Test]
        public void 제_목록의_판이면_몬다()
        {
            StageDefinition s1 = Stage("S1"), s2 = Stage("S2");
            var list = new List<StageDefinition> { s1, s2 };

            Assert.That(AutoBattleController.Owns(list, s1), Is.True, "S1 은 내 판이다");
            Assert.That(AutoBattleController.Owns(list, s2), Is.True, "S2 도 내 판이다");
        }

        [Test]
        public void 튜토리얼_판은_안_몬다()
        {
            StageDefinition s0 = Stage("S0"), s1 = Stage("S1");
            var list = new List<StageDefinition> { s1 };

            // ⚠️ **이 한 줄이 09-15 의 ③④ 였다.** 종전에는 S0 이 목록에 없다는 것을
            // 무시하고 진행을 계속해 튜토리얼 밑에서 판을 갈아 끼웠다.
            Assert.That(AutoBattleController.Owns(list, s0), Is.False,
                "튜토리얼은 목록에 없다 — 남의 판이므로 손을 떼야 한다");
        }

        [Test]
        public void 판이_없거나_목록이_비면_안_몬다()
        {
            // 「못 찾았다」와 「첫 번째다」를 같은 값으로 접지 않는다 —
            // 종전 Start 가 idx -1 을 무시해 CurrentIndex 가 기본값 0(=S1) 으로 남았다.
            Assert.That(AutoBattleController.Owns(null, Stage("S1")), Is.False, "목록이 없다");
            Assert.That(AutoBattleController.Owns(new List<StageDefinition>(), Stage("S1")), Is.False, "목록이 비었다");
            Assert.That(AutoBattleController.Owns(new List<StageDefinition> { Stage("S1") }, null), Is.False, "판이 없다");
        }
    }
}
