using System.Collections.Generic;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// **파밍 층 적도 그림이 있다** (사용자 확정 #170 · 2026-10-19 · #111 부터 파밍 종류 그림 미등록 → 빨간 사각형).
    /// </summary>
    public sealed class FarmEnemyArtTests
    {
        private static List<EnemyDefinition> Catalog()
        {
            var list = new List<EnemyDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/_Project/ScriptableObjects/Enemies" }))
                list.Add(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            return list;
        }

        [Test]
        public void S1부터_S6_파밍_종류는_전부_그림이_있는_정의로_알려진다()
        {
            List<EnemyDefinition> catalog = Catalog();
            if (catalog.Count == 0) Assert.Ignore("적 자산 없음");
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>("Assets/_Project/ScriptableObjects/CombatTuning.asset");

            int seen = 0;
            foreach (string id in new[] { "S1", "S2", "S3", "S4", "S5", "S6" })
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>($"Assets/_Project/ScriptableObjects/Stages/Stage_{id}.asset");
                if (stage == null || stage.farmComposition == null) continue;
                foreach (StageComposition c in stage.farmComposition)
                {
                    EnemyDefinition got = null; string label = null;
                    StageSpawnFactory.FarmSpawn(c, catalog, tuning, (l, d) => { label = l; got = d; });
                    Assert.IsNotNull(got, $"{id} 파밍 「{c.enemyKey}」 정의를 못 찾음");
                    Assert.IsNotNull(got.sprite, $"{id} 파밍 「{c.enemyKey}」 그림 없음");
                    Assert.AreEqual(got.displayName, label, "라벨 = 표시명(싸우는 몸이 드는 이름)");
                    seen++;
                }
            }
            Assert.Greater(seen, 0, "파밍 구성이 하나도 없다");
        }

        [Test]
        public void 러너는_판이_설_때와_리필_때_둘_다_파밍_종류를_등록한다_소스_고정()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("StageSpawnFactory.FarmSpawn(c, enemyCatalog, tuning, RegisterEnemyArt);", src);
            StringAssert.Contains("StageSpawnFactory.FarmSpawn(r.entry, enemyCatalog, tuning, RegisterEnemyArt)", src);
        }
    }
}
