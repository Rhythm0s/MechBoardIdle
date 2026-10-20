using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#239 — 벨트 속도 배율: 기본 1 = 종전 거동 · 과부하 동안 러너가 2 를 넣는다.</summary>
    public sealed class OverloadBeltTests
    {
        [TearDown] public void Reset() => BeltItemFlow.SpeedMultiplier = 1f;

        [Test]
        public void Default_IsOne() => Assert.AreEqual(1f, BeltItemFlow.SpeedMultiplier, 1e-6f);

        [Test]
        public void Runner_SetsBeltDuringOverloadOnly()
        {
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("BeltItemFlow.SpeedMultiplier = _sim.IsOverloaded && bal != null ? Mathf.Max(1f, bal.overloadBeltMult) : 1f;", sr);
            string bf = System.IO.File.ReadAllText("Assets/_Project/Scripts/Core/BeltItemFlow.cs");
            StringAssert.Contains("float step = CellsPerSecondTbd * SpeedMultiplier * dt;", bf);
        }
    }
}
