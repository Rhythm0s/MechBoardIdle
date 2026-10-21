using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>#257(2026-10-21 사용자 확정) — S5 도전 구성 = 장갑 4 + 보병 3 · 파밍 · 다른 판 불변.</summary>
    public sealed class S5CompTests
    {
        private const string Stages = "Assets/_Project/ScriptableObjects/Stages";
        private const float D = 1e-3f;

        private static StageComposition Find(StageComposition[] list, string key)
        {
            foreach (StageComposition c in list) if (c.enemyKey == key) return c;
            Assert.Fail(key + " 줄이 없다");
            return default;
        }

        [Test]
        public void S5_Challenge_FourArmorThreeInfantry()
        {
            var s5 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S5.asset");
            StageComposition[] comp = s5.composition.ToArray();
            Assert.AreEqual(2, comp.Length, "도전 구성 줄 수");
            StageComposition arm = Find(comp, "armor"), inf = Find(comp, "infantry");
            Assert.AreEqual(4, arm.count, "장갑 7 → 4");
            Assert.AreEqual(850f, arm.hp, D); Assert.AreEqual(15f, arm.def, D);
            Assert.AreEqual(3, inf.count, "보병 3");
            Assert.AreEqual(30f, inf.hp, D, "S5 파밍 표 값 ⚠️가정"); Assert.AreEqual(2.5f, inf.def, D);
        }

        [Test]
        public void S5_Farm_And_OtherStages_Unchanged()
        {
            var s5 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S5.asset");
            StageComposition[] farm = s5.farmComposition.ToArray();
            Assert.AreEqual(6, Find(farm, "infantry").count);
            Assert.AreEqual(2, Find(farm, "artillery").count);
            Assert.AreEqual(2, Find(farm, "armor").count);

            var s4 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S4.asset");
            var s6 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S6.asset");
            Assert.AreEqual(19, Find(s4.composition.ToArray(), "infantry").count);
            Assert.AreEqual(2, Find(s4.composition.ToArray(), "armor").count);
            Assert.AreEqual(15000f, Find(s6.composition.ToArray(), "boss").hp, D);
        }
    }
}
