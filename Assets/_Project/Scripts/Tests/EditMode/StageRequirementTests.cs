using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 요구치 판정(§5-6 F). 앵커는 balance_v4.json 확정치(reqConfirmed: true):
    /// S1 90 / S2 105 / S3 130(fixed) · S4 [186,215](band) · S5 formula · S6 budget.
    /// 척도일 뿐 관문이 아니므로 승패에는 쓰이지 않는다.
    /// </summary>
    public sealed class StageRequirementTests
    {
        private static readonly Vector2 S4Band = new Vector2(186f, 215f);

        [Test]
        public void Fixed_S3_130_MetAt145()
        {
            // 대표 출력 145가 S3 요구치 130을 넘는다 = s3Break.
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
