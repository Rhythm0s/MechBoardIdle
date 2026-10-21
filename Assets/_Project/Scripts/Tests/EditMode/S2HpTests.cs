using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>#255(2026-10-21 사용자 확정) — S2 파밍 · 도전 몬스터 HP ×0.9 · 다른 판 불변.</summary>
    public sealed class S2HpTests
    {
        private const string Stages = "Assets/_Project/ScriptableObjects/Stages";
        private const float D = 1e-3f;

        private static float Hp(StageComposition[] list, string key)
        {
            foreach (StageComposition c in list) if (c.enemyKey == key) return c.hp;
            return -1f;
        }

        [Test]
        public void S2_ChallengeAndFarm_HpTimesPointNine()
        {
            var s2 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S2.asset");
            Assert.AreEqual(20.25f, Hp(s2.composition.ToArray(), "infantry"), D, "도전 보병 22.5 × 0.9");
            Assert.AreEqual(54f, Hp(s2.composition.ToArray(), "artillery"), D, "도전 포격 60 × 0.9");
            Assert.AreEqual(20.25f, Hp(s2.farmComposition.ToArray(), "infantry"), D, "파밍 보병");
            Assert.AreEqual(54f, Hp(s2.farmComposition.ToArray(), "artillery"), D, "파밍 포격");
        }

        [Test]
        public void OtherStages_Unchanged()
        {
            var s1 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S1.asset");
            var s3 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_S3.asset");
            Assert.AreEqual(20f, Hp(s1.composition.ToArray(), "infantry"), D);
            Assert.AreEqual(27.5f, Hp(s3.composition.ToArray(), "infantry"), D);
            Assert.AreEqual(82.5f, Hp(s3.composition.ToArray(), "artillery"), D);
        }
    }
}
