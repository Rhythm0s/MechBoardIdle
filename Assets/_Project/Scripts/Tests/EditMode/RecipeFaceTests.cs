using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **면은 자산이 정하고 품목은 조합표가 정한다** (2026-09-14 · §72-19 · 사용자 판정 (2)).
    ///
    /// ⚠️ **왜 신설하는가.** 종전에는 <c>BeltRouting.HasInputPort</c> 가 **포트에 적힌
    /// 품목까지** 대조했다. 포트 품목은 노드마다 하나로 박혀 있어(가공 = 기초재료·부품 ·
    /// 복합 군수 남면 = 기초재료·부품), 조합표를 발전재료로 바꿔도 면이 그것을 안 받았다.
    ///
    /// 그 결과 **조합표에는 있는데 격자 위에는 한 줄도 못 서는 것이 넷**이었다 —
    /// 폭발탄 · 누적형 드론 · 광역형 드론 · 배터리. 같은 판에서 조합표만 바꿔 재면
    /// **관통탄 53개 · 폭발탄 0개**였다(`ShootBoardProbe` 2026-09-14).
    ///
    /// 여기 있는 시험은 그 자리가 다시 막히면 빨개진다.
    /// </summary>
    public sealed class RecipeFaceTests
    {
        private const string NodesDir = "Assets/_Project/ScriptableObjects/Nodes";

        private NodeDefinition _core, _proc, _muni, _munix;

        [SetUp]
        public void SetUp()
        {
            _core = Load("core");
            _proc = Load("proc");
            _muni = Load("muni");
            _munix = Load("munix");
            if (_core == null || _proc == null || _muni == null || _munix == null)
                Assert.Ignore("노드 자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static NodeDefinition Load(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesDir}/Node_{id}.asset");

        private static BoardGrid Grid() => new BoardGrid(10, 10, 1f, Vector2.zero);

        private static bool HasLink(BoardGrid g, Vector2Int from, Vector2Int to)
        {
            foreach (BeltLink l in BeltRouting.BuildLinks(g))
                if (l.fromCell == from && l.toCell == to) return true;
            return false;
        }

        // ────────────────────────────────────────────────────────────────
        //  1. 폭발탄 — 가공(발전재료) → 복합 군수(폭발탄)
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 복합 군수의 남면 포트는 **기초재료·부품**으로 적혀 있다. 그런데 폭발탄 조합표가
        /// 먹는 것은 **발전재료**다 — 포트를 잣대로 쓰면 이 줄이 영영 안 선다.
        /// </summary>
        [Test]
        public void PowerMaterial_FeedsExplosiveAmmo_ThroughAPortLabelledBasicParts()
        {
            var g = Grid();

            // 가공을 발전재료로 돌린다 — 출력 포트는 여전히 「기초재료·부품」이다.
            g.TryPlace(new Vector2Int(1, 1), _proc, out NodeInstance proc);
            Assert.IsTrue(proc.SelectRecipe(RecipeKind.PowerMaterial));
            Assert.AreEqual(FlowKind.PowerMaterial, BeltFlow.OutputKindOf(proc),
                "산출은 조합표가 정한다");

            // 벨트 한 칸이 꺾어 올려 복합 군수 남면에 넣는다(가공은 동으로 낸다).
            g.TryPlaceBelt(new Vector2Int(2, 1), PortFace.West, PortFace.North, FlowKind.None, out _);
            g.TryPlace(new Vector2Int(2, 2), _munix, out NodeInstance munix);
            Assert.IsTrue(munix.SelectRecipe(RecipeKind.ExplosiveAmmo));

            BeltFlow.Resolve(g);

            Assert.AreEqual(FlowKind.PowerMaterial, BeltFlow.KindAt(g, new Vector2Int(2, 1)),
                "벨트가 발전재료를 나른다");
            Assert.IsTrue(HasLink(g, new Vector2Int(2, 1), new Vector2Int(2, 2)),
                "포트에 안 적힌 품목이라도 **조합표가 먹으면** 링크가 선다");
        }

        /// <summary>
        /// 되짚기 — **조합표가 안 먹는 것은 여전히 거절한다.** 면을 열어 준 것이지
        /// 아무것이나 받게 한 것이 아니다.
        /// </summary>
        [Test]
        public void ARecipeThatDoesNotEatIt_StillRefuses()
        {
            var g = Grid();

            g.TryPlace(new Vector2Int(1, 1), _proc, out NodeInstance proc);
            proc.SelectRecipe(RecipeKind.PowerMaterial);

            g.TryPlaceBelt(new Vector2Int(2, 1), PortFace.West, PortFace.North, FlowKind.None, out _);
            g.TryPlace(new Vector2Int(2, 2), _munix, out NodeInstance munix);

            // 관통탄은 표준탄 + 기초재료·부품을 먹는다 — 발전재료는 안 먹는다.
            Assert.IsTrue(munix.SelectRecipe(RecipeKind.PierceAmmo));
            BeltFlow.Resolve(g);

            Assert.IsFalse(HasLink(g, new Vector2Int(2, 1), new Vector2Int(2, 2)),
                "안 먹는 품목은 면이 열려 있어도 안 받는다");
        }

        // ────────────────────────────────────────────────────────────────
        //  2. 누적형 드론 — 배터리 + 드론 몸체 부품
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 드론 줄은 **두 재료 다** 포트에 안 적힌 품목이다(배터리 · 드론 몸체 부품).
        /// 2026-09-14 사용자 보고 「로봇 B 드론 출력이 안 됨」의 뿌리가 여기였다.
        /// </summary>
        [Test]
        public void Battery_And_DroneBodyParts_FeedStackDrone()
        {
            var g = Grid();

            // 서면: 기초 군수가 드론 몸체 부품을 낸다.
            g.TryPlace(new Vector2Int(1, 2), _muni, out NodeInstance muni);
            Assert.IsTrue(muni.SelectRecipe(RecipeKind.DroneBody));
            Assert.AreEqual(FlowKind.DroneBodyParts, BeltFlow.OutputKindOf(muni));

            // 남면: 가공이 배터리를 낸다 — 벨트가 꺾어 올린다.
            g.TryPlace(new Vector2Int(1, 1), _proc, out NodeInstance proc);
            Assert.IsTrue(proc.SelectRecipe(RecipeKind.Battery));
            g.TryPlaceBelt(new Vector2Int(2, 1), PortFace.West, PortFace.North, FlowKind.None, out _);

            g.TryPlace(new Vector2Int(2, 2), _munix, out NodeInstance munix);
            Assert.IsTrue(munix.SelectRecipe(RecipeKind.StackDrone));

            BeltFlow.Resolve(g);

            Assert.IsTrue(HasLink(g, new Vector2Int(1, 2), new Vector2Int(2, 2)),
                "드론 몸체 부품이 서면으로 든다");
            Assert.IsTrue(HasLink(g, new Vector2Int(2, 1), new Vector2Int(2, 2)),
                "배터리가 남면으로 든다");
            Assert.AreEqual(FlowKind.StackDrone, BeltFlow.OutputKindOf(munix),
                "누적형 드론이 나온다");
        }

        // ────────────────────────────────────────────────────────────────
        //  3. 조합표를 바꾸면 벨트가 나르는 것도 다시 전파된다
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 조합표를 바꾸면 **하류 벨트가 나르는 것이 통째로 바뀐다.** 안 바뀌면 화면에서는
        /// 「바꿨는데 아무 일도 안 난다」로 보이고, 아래 노드는 옛 품목을 기다리며 선다.
        ///
        /// `BoardController` 는 조합표 버튼 뒤에 `RefreshConnections()` 를 부르고,
        /// 그것이 `BeltFlow.Resolve` → `BeltRouting.BuildLinks` 를 다시 돌린다.
        /// </summary>
        [Test]
        public void ChangingARecipe_RepropagatesBeltKinds()
        {
            var g = Grid();

            g.TryPlace(new Vector2Int(1, 1), _core, out _);
            g.TryPlace(new Vector2Int(2, 1), _proc, out NodeInstance proc);
            g.TryPlaceBelt(new Vector2Int(3, 1), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(4, 1), PortFace.West, PortFace.East, FlowKind.None, out _);

            BeltFlow.Resolve(g);
            Assert.AreEqual(FlowKind.BasicParts, BeltFlow.KindAt(g, new Vector2Int(3, 1)),
                "기본 조합표는 기초재료·부품");
            Assert.AreEqual(FlowKind.BasicParts, BeltFlow.KindAt(g, new Vector2Int(4, 1)),
                "두 칸 아래까지 내려간다");

            Assert.IsTrue(proc.SelectRecipe(RecipeKind.PowerMaterial));
            BeltFlow.Resolve(g);

            Assert.AreEqual(FlowKind.PowerMaterial, BeltFlow.KindAt(g, new Vector2Int(3, 1)),
                "바꾼 품목이 바로 내려간다");
            Assert.AreEqual(FlowKind.PowerMaterial, BeltFlow.KindAt(g, new Vector2Int(4, 1)),
                "체인 끝까지 다시 전파된다");
        }
    }
}
