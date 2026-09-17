using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 요구치 배지가 **스스로 모순돼 보이지 않는가** (2026-09-17 · 사용자 육안).
    ///
    /// ⚠️⚠️ 화면에 **「요구 18 [부족 18]」**이 떴다. 판정은 맞았다 — 전투력 17.6 이므로
    /// 부족이 옳다. 틀린 것은 **읽히는 방식**이었다: 괄호 안 수는 「모자란 양」이 아니라
    /// **「지금 전투력」**인데, 「충족 20」은 「20 으로 충족」으로 읽히고 「부족 18」은
    /// 「18 만큼 모자라다」로 읽힌다. 거기에 반올림이 겹쳐 요구치와 같은 수가 됐다.
    ///
    /// 📌 그래서 이 시험이 지키는 것은 **「지금 값과 요구치가 둘 다 보이는가」**다.
    /// </summary>
    public sealed class StageRequirementBadgeTests
    {
        private static readonly Vector2 NoBand = Vector2.zero;

        [Test]
        public void 부족하면_지금_값과_요구치를_같이_보여_준다()
        {
            string s = StageRequirement.Badge(StageReqType.Fixed, 18f, NoBand, 17.6f);

            StringAssert.Contains("부족", s);
            StringAssert.Contains("17.6", s, "지금 전투력이 안 보인다");
            StringAssert.Contains("18.0", s, "견주는 요구치가 안 보인다");
        }

        [Test]
        public void 반올림이_요구치와_같은_수를_만들지_않는다()
        {
            // 구 꼴은 17.6 을 정수로 찍어 「요구 18 · 부족 18」이 됐다.
            string s = StageRequirement.Badge(StageReqType.Fixed, 18f, NoBand, 17.6f);

            Assert.IsFalse(s.Contains("부족 18]"),
                "부족이라면서 요구치와 같은 수를 찍는다 — 화면이 스스로 모순돼 보인다");
        }

        [Test]
        public void 충족도_같은_꼴로_읽힌다()
        {
            string s = StageRequirement.Badge(StageReqType.Fixed, 18f, NoBand, 20.2f);

            StringAssert.Contains("충족", s);
            StringAssert.Contains("20.2", s);
            StringAssert.Contains("18.0", s);
        }

        [Test]
        public void 요구치가_없는_스테이지는_배지를_안_단다()
        {
            // S5 공식형 · S6 예산형 — 견줄 스칼라가 원천에 없다. 없는 규칙을 만들지 않는다.
            Assert.AreEqual(string.Empty,
                StageRequirement.Badge(StageReqType.Formula, 0f, NoBand, 99f));
        }

        [Test]
        public void 밴드는_하단과_견준다()
        {
            var band = new Vector2(30f, 40f);

            StringAssert.Contains("30.0",
                StageRequirement.Badge(StageReqType.Band, 0f, band, 25f), "밴드 하단이 안 보인다");
            StringAssert.Contains("40.0",
                StageRequirement.Badge(StageReqType.Band, 0f, band, 45f), "밴드 상단이 안 보인다");
        }
    }
}
