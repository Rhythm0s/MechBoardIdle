using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// **팔레트에 무엇이 서는가** (2026-09-16 · 사용자 확정 · 플랜 §74-3 #32).
    ///
    /// ⚠️⚠️ **복합 군수가 빠져 있었다.** 팔레트가 여섯이라 플레이어가 그 노드를
    /// 못 놓았는데, **관통탄·폭발탄·누적 드론·광역 드론이 전부 그것을 거친다** —
    /// 만들 수 있는 것의 절반이 놓을 수 없는 노드 뒤에 있었다.
    /// 이 결함은 로그도 예외도 안 냈다. 화면에 **없는 버튼**이라 아무 신호가 없다.
    ///
    /// 📌 씬을 **직접 읽는다** — 생성기를 안 돌린 씬도 잡는다(`startingNodePool` 시험과 같은 길).
    /// </summary>
    public sealed class PaletteContentsTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        /// <summary>
        /// 팔레트에 서야 하는 노드들.
        ///
        /// ⚠️ **코어는 없다** — 시작 보드 고정이라 팔레트에 안 뜬다.
        /// ⚠️ 쉴드(`shield`)도 없다 — 자산은 있으나 MVP 범위 밖이다(넣지 않는다).
        /// </summary>
        private static readonly string[] Expected =
            { "proc", "muni", "munix", "ener", "stor", "boost" };

        private static string PaletteBlock()
        {
            string text = System.IO.File.ReadAllText(ScenePath);
            int at = text.IndexOf("\n  palette:", System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "씬에 palette 목록이 없다");

            int end = text.IndexOf("\n  modulePalette:", at, System.StringComparison.Ordinal);
            Assert.Greater(end, at, "palette 목록의 끝을 못 찾았다 — 씬 꼴이 바뀌었다");
            return text.Substring(at, end - at);
        }

        [Test]
        public void 복합_군수가_팔레트에_있다()
        {
            // **이 한 줄이 결함 그 자체다.**
            string guid = AssetDatabase.AssetPathToGUID($"{NodeRoot}/Node_munix.asset");
            Assert.IsNotEmpty(guid, "복합 군수 자산이 없다");
            StringAssert.Contains(guid, PaletteBlock(),
                "복합 군수가 팔레트에 없다 — 플레이어가 관통·폭발·드론 줄을 못 짓는다");
        }

        [Test]
        public void 팔레트에_있어야_할_것이_전부_있다()
        {
            string block = PaletteBlock();
            foreach (string id in Expected)
            {
                string guid = AssetDatabase.AssetPathToGUID($"{NodeRoot}/Node_{id}.asset");
                Assert.IsNotEmpty(guid, $"자산이 없다 — Node_{id}.asset");
                StringAssert.Contains(guid, block, $"팔레트에 '{id}' 가 없다");
            }
        }

        [Test]
        public void 복합_군수는_복합_탭에_선다()
        {
            // ⚠️ 탭을 새로 만들지 않았다 — 「복합」 탭이 이미 있고 소속은
            //    **입력면 수**가 정한다(이름이 아니라 수라서 새 노드도 저절로 간다).
            var def = AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_munix.asset");
            Assert.IsNotNull(def);

            Assert.GreaterOrEqual(PaletteCategories.InputFaceCount(def), 2,
                "복합 군수의 입력면이 둘 미만이다 — 그러면 기초 탭으로 간다");
            Assert.AreEqual(PaletteCategory.Complex, PaletteCategories.Of(def));
            Assert.IsTrue(PaletteCategories.Shows(PaletteCategory.Complex, def));
            Assert.IsTrue(PaletteCategories.Shows(PaletteCategory.All, def));
            Assert.IsFalse(PaletteCategories.Shows(PaletteCategory.Basic, def),
                "기초 탭에도 뜬다 — 두 탭에 같은 것이 서면 둘 중 어디가 제자리인지 흐려진다");
        }

        [Test]
        public void 복합_군수가_드론_둘을_다_돌릴_수_있다()
        {
            // 팔레트에 넣은 까닭이 이것이다 — 드론 두 종이 이 노드에서 갈린다.
            var def = AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_munix.asset");
            Assert.IsNotNull(def);
            Assert.IsNotNull(def.recipes, "조합표가 없다");

            var kinds = new HashSet<RecipeKind>();
            foreach (NodeRecipe r in def.recipes) kinds.Add(r.kind);

            Assert.IsTrue(kinds.Contains(RecipeKind.StackDrone), "누적형 조합표가 없다");
            Assert.IsTrue(kinds.Contains(RecipeKind.AoeDrone), "광역형 조합표가 없다");
        }
    }
}
