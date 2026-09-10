using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 요구치 판정(§5-6 F). 척도일 뿐 관문이 아니므로 승패에는 쓰이지 않는다.
    ///
    /// ⚠️ **여기 있는 숫자는 원천 값이 아니라 함수를 재는 표본이다.** 원천 값을 그대로 쓰면
    /// 요구치가 바뀔 때마다 판정 함수 시험이 같이 빨개진다 — 함수는 안 바뀌었는데.
    /// 원천 값을 붙드는 것은 `BalanceAnchorTests` 다.
    ///
    /// ⚠️ **구 머리말 폐기** — 「S1 90 / S2 105 / S3 130 · S4 [186,215]」는 세 번 낡았다.
    /// 정본은 **S1 54 · S2 54 · S3 126 · S4 Fixed 183**(2026-09-10 · `260910_W02` 2-1)이며
    /// **S4 는 더 이상 밴드가 아니다.** 아래 Band 시험들은 **함수가 밴드를 여전히 처리하는지**를
    /// 재는 것이고, 그 유형을 쓰는 스테이지는 지금 없다.
    /// </summary>
    public sealed class StageRequirementTests
    {
        private static readonly Vector2 S4Band = new Vector2(186f, 215f);

        [Test]
        public void Fixed_S3_130_MetAt145()
        {
            // 표본: 출력 145 가 요구치 130 을 넘는다. ⚠️ 구 「= s3Break」 표기는 폐기(260910_W02 2-2)이며
            // 이 둘은 원천 값이 아니라 함수를 재는 표본이다 — 정본 S3 는 126 이다.
            Assert.AreEqual(ReqStatus.Met,
                StageRequirement.Evaluate(StageReqType.Fixed, 130f, Vector2.zero, 145f));
        }

        [Test]
        public void Fixed_S3_Below_At120()
        {
            Assert.AreEqual(ReqStatus.Below,
                StageRequirement.Evaluate(StageReqType.Fixed, 130f, Vector2.zero, 120f));
        }

        [Test]
        public void Fixed_ExactlyAtReq_IsMet()
        {
            Assert.AreEqual(ReqStatus.Met,
                StageRequirement.Evaluate(StageReqType.Fixed, 90f, Vector2.zero, 90f), "경계는 충족");
        }

        [Test]
        public void Band_S4_MetAt210_25()
        {
            // 강화 ×1.45 도달치 210.25가 밴드 [186,215] 안.
            Assert.AreEqual(ReqStatus.Met,
                StageRequirement.Evaluate(StageReqType.Band, 0f, S4Band, 210.25f));
        }

        [Test]
        public void Band_S4_BelowAt145()
        {
            // 강화 없이는 S4를 못 넘는다(강화-only 벽).
            Assert.AreEqual(ReqStatus.Below,
                StageRequirement.Evaluate(StageReqType.Band, 0f, S4Band, 145f));
        }

        [Test]
        public void Band_S4_AboveBandAt230()
        {
            Assert.AreEqual(ReqStatus.AboveBand,
                StageRequirement.Evaluate(StageReqType.Band, 0f, S4Band, 230f));
        }

        [Test]
        public void Formula_And_Budget_ReturnNotApplicable()
        {
            // S5·S6에는 비교할 스칼라 req가 원천에 없다 — 없는 규칙을 만들지 않는다.
            Assert.AreEqual(ReqStatus.NotApplicable,
                StageRequirement.Evaluate(StageReqType.Formula, 0f, Vector2.zero, 999f));
            Assert.AreEqual(ReqStatus.NotApplicable,
                StageRequirement.Evaluate(StageReqType.Budget, 0f, Vector2.zero, 999f));
        }
    }
}
