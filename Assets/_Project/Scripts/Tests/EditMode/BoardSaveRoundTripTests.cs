using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 보드 저장 왕복 — 시험 여섯 (`Docs/board_save_design.md` 4장 · 2026-09-16).
    ///
    /// ⚠️⚠️ **왜 이것이 필요했나.** 보드 배치가 저장되지 않아 **재입장마다 시작 보드로
    /// 되돌아갔다.** 플레이어가 늘린 판은 물론이고, 09-15 에는 스테이지가 넘어갈 때마다
    /// 튜토리얼로 채운 칸까지 사라져 **운반로가 끊긴 채 생산이 0** 이 되는 자리까지 갔다.
    ///
    /// 📌 여기서 지키는 것은 **「왕복해도 같은 판인가」** 하나다. 화면은 안 거친다 —
    /// 09-15 에 「프로브는 초록인데 화면은 낡았다」를 여러 번 겪었으므로, 값이 오가는
    /// 자리와 그림이 서는 자리를 갈라 두고 **이쪽은 값만** 본다.
    /// </summary>
    public sealed class BoardSaveRoundTripTests
    {
        private const int Cols = 14;
        private const int Rows = 14;

        private readonly List<NodeDefinition> _nodes = new List<NodeDefinition>();
        private readonly List<ModuleDefinition> _modules = new List<ModuleDefinition>();

        private static BoardGrid Grid(int cols = Cols, int rows = Rows)
            => new BoardGrid(cols, rows, 1f, Vector2.zero);

        private NodeDefinition Node(string id)
        {
            var d = ScriptableObject.CreateInstance<NodeDefinition>();
            d.nodeId = id;
            _nodes.Add(d);
            return d;
        }

        private ModuleDefinition Module(string id)
        {
            var m = ScriptableObject.CreateInstance<ModuleDefinition>();
            m.moduleId = id;
            _modules.Add(m);
            return m;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (NodeDefinition d in _nodes) Object.DestroyImmediate(d);
            foreach (ModuleDefinition m in _modules) Object.DestroyImmediate(m);
            _nodes.Clear();
            _modules.Clear();
        }

        private System.Func<string, NodeDefinition> NodeLookup()
        {
            var map = new Dictionary<string, NodeDefinition>();
            foreach (NodeDefinition d in _nodes) map[d.nodeId] = d;
            return id => map.TryGetValue(id ?? string.Empty, out NodeDefinition d) ? d : null;
        }

        private System.Func<string, ModuleDefinition> ModuleLookup()
        {
            var map = new Dictionary<string, ModuleDefinition>();
            foreach (ModuleDefinition m in _modules) map[m.moduleId] = m;
            return id => map.TryGetValue(id ?? string.Empty, out ModuleDefinition m) ? m : null;
        }

        // ── 1. 시작 보드 왕복 = 동일 ────────────────────────────────────────────

        [Test]
        public void 시작_보드를_왕복해도_노드와_벨트가_그대로다()
        {
            BoardGrid before = Grid();
            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
                before.TryPlace(s.cell, Node(s.nodeId), out _);
            foreach (StartingBoard.Run r in StartingBoard.Belts)
                Place(before, r);

            int nodeCount = Count(before.Nodes);
            int beltCount = Count(before.Belts);
            Assert.That(nodeCount, Is.GreaterThan(0), "시작 보드에 노드가 없다 — 시험 전제가 깨졌다");

            BoardStateV1 saved = BoardStateCodec.Capture(before);
            BoardGrid after = Grid();
            int missed = BoardStateCodec.Restore(saved, after, NodeLookup(), ModuleLookup());

            Assert.That(missed, Is.EqualTo(0), "왕복에서 못 놓은 것이 있다");
            Assert.That(Count(after.Nodes), Is.EqualTo(nodeCount), "노드 수");
            Assert.That(Count(after.Belts), Is.EqualTo(beltCount), "벨트 수");

            // 좌표와 면까지 본다 — 수만 맞고 자리가 밀리는 것이 가장 나쁜 실패다.
            foreach (BeltInstance b in before.Belts)
            {
                BeltInstance a = after.GetBeltAt(b.Cell);
                Assert.That(a, Is.Not.Null, $"{b.Cell} 벨트가 사라졌다");
                Assert.That(BoardStateCodec.MaskOf(a.OutFaces),
                    Is.EqualTo(BoardStateCodec.MaskOf(b.OutFaces)), $"{b.Cell} 나가는 면");
                Assert.That(BoardStateCodec.MaskOf(a.InFaces),
                    Is.EqualTo(BoardStateCodec.MaskOf(b.InFaces)), $"{b.Cell} 받는 면");
            }
        }

        // ── 2. 회전 ────────────────────────────────────────────────────────────

        [Test]
        public void 회전을_넣고_왕복하면_회전이_살아남는다()
        {
            // ⚠️ **이 묶음이 생긴 이유가 이 시험이다.** 회전이 안 실리면 재입장에 판이
            //    통째로 돌아간 채 서고, 그것은 에러 없이 조용히 틀린 판이다.
            BoardGrid before = Grid();
            NodeDefinition def = Node("N_rot");
            var cell = new Vector2Int(3, 4);
            Assert.That(before.TryPlace(cell, def, out NodeInstance placed), Is.True);
            placed.Rotation = 3;

            BoardGrid after = Grid();
            int missed = BoardStateCodec.Restore(
                BoardStateCodec.Capture(before), after, NodeLookup(), ModuleLookup());

            Assert.That(missed, Is.EqualTo(0));
            Assert.That(after.GetAt(cell)?.Rotation, Is.EqualTo(3), "회전이 안 실렸다");
        }

        // ── 3. 탄종·모듈 ───────────────────────────────────────────────────────

        [Test]
        public void 탄종과_모듈이_칸_순서까지_살아남는다()
        {
            BoardGrid before = Grid();
            NodeDefinition def = Node("N_mod");
            ModuleDefinition m0 = Module("MOD_A");
            ModuleDefinition m1 = Module("MOD_B");

            var cell = new Vector2Int(5, 5);
            before.TryPlace(cell, def, out NodeInstance placed);
            placed.AmmoKind = AmmoKind.Explosive;
            placed.TryAttachModuleAt(0, m0);
            placed.TryAttachModuleAt(1, m1);

            BoardGrid after = Grid();
            int missed = BoardStateCodec.Restore(
                BoardStateCodec.Capture(before), after, NodeLookup(), ModuleLookup());

            NodeInstance back = after.GetAt(cell);
            Assert.That(missed, Is.EqualTo(0));
            Assert.That(back.AmmoKind, Is.EqualTo(AmmoKind.Explosive), "탄종");

            // ⚠️ **칸 순서를 못 박는다** — 빈 칸을 앞에서부터 메우는 식으로 복원하면
            //    0번이 비고 1번만 차 있던 판이 0번에 붙은 판으로 바뀐다.
            Assert.That(back.ModuleAt(0)?.moduleId, Is.EqualTo("MOD_A"), "모듈 0번 칸");
            Assert.That(back.ModuleAt(1)?.moduleId, Is.EqualTo("MOD_B"), "모듈 1번 칸");
        }

        [Test]
        public void 한_칸만_찬_모듈은_그_칸에_그대로_돌아온다()
        {
            BoardGrid before = Grid();
            NodeDefinition def = Node("N_mod1");
            ModuleDefinition m = Module("MOD_B");

            var cell = new Vector2Int(6, 6);
            before.TryPlace(cell, def, out NodeInstance placed);
            placed.TryAttachModuleAt(1, m);   // 0번은 비운 채 1번만

            BoardGrid after = Grid();
            BoardStateCodec.Restore(BoardStateCodec.Capture(before), after, NodeLookup(), ModuleLookup());

            NodeInstance back = after.GetAt(cell);
            Assert.That(back.ModuleAt(0), Is.Null, "0번 칸은 비어 있어야 한다");
            Assert.That(back.ModuleAt(1)?.moduleId, Is.EqualTo("MOD_B"), "1번 칸");
        }

        // ── 4. 격자 크기가 다르면 버린다 ───────────────────────────────────────

        [Test]
        public void 격자_크기가_다르면_안_푼다()
        {
            // ⚠️ 09-14 에 Rows 가 13 → 14 로 늘었다. 크기를 안 보면 **옛 저장이 조용히
            //    어긋난 자리에 놓인다** — 에러가 안 나서 가장 늦게 발견된다.
            BoardGrid before = Grid(14, 13);
            before.TryPlace(new Vector2Int(2, 2), Node("N_x"), out _);
            BoardStateV1 saved = BoardStateCodec.Capture(before);

            BoardGrid after = Grid(14, 14);
            Assert.That(BoardStateCodec.Fits(saved, after), Is.False, "크기가 다른데 맞다고 했다");
            Assert.That(BoardStateCodec.Restore(saved, after, NodeLookup(), ModuleLookup()),
                Is.EqualTo(-1), "안 푼다는 표시(-1)가 와야 한다");
            Assert.That(Count(after.Nodes), Is.EqualTo(0), "하나라도 놓았으면 안 된다");
        }

        // ── 5. 없거나 깨진 저장 ────────────────────────────────────────────────

        [Test]
        public void 저장이_없으면_안_푼다()
        {
            BoardGrid after = Grid();
            Assert.That(BoardStateCodec.Fits(null, after), Is.False);
            Assert.That(BoardStateCodec.Restore(null, after, NodeLookup()), Is.EqualTo(-1));
            Assert.That(Count(after.Nodes), Is.EqualTo(0));

            // 깨진 JSON 은 `JsonUtility` 가 빈 객체로 준다 — 그 꼴이 이것이다(크기 0).
            var broken = new BoardStateV1();
            Assert.That(BoardStateCodec.Fits(broken, after), Is.False, "크기 0 짜리를 받아들였다");
        }

        [Test]
        public void 모르는_노드_하나는_건너뛰고_나머지는_선다()
        {
            // ⚠️ **통째로 버리지 않는다.** 자산 하나가 사라졌다고 플레이어의 나머지 판까지
            //    날리는 편이 더 나쁘다 — 다만 몇 개를 못 놓았는지는 센다.
            BoardGrid before = Grid();
            before.TryPlace(new Vector2Int(1, 1), Node("N_keep"), out _);
            BoardStateV1 saved = BoardStateCodec.Capture(before);
            saved.nodes.Add(new BoardNodeEntry { nodeId = "N_gone", x = 2, y = 2 });

            BoardGrid after = Grid();
            int missed = BoardStateCodec.Restore(saved, after, NodeLookup(), ModuleLookup());

            Assert.That(missed, Is.EqualTo(1), "못 놓은 하나를 세야 한다");
            Assert.That(after.GetAt(new Vector2Int(1, 1)), Is.Not.Null, "아는 노드는 섰어야 한다");
            Assert.That(after.GetAt(new Vector2Int(2, 2)), Is.Null, "모르는 노드가 섰다");
        }

        // ── 6. 면 비트 ─────────────────────────────────────────────────────────

        [Test]
        public void 면은_비트로_접었다_펴도_같은_집합이다()
        {
            // 면 배열을 정수로 접는 것은 JsonUtility 가 배열의 배열을 못 삼키기 때문이다.
            PortFace[] all = { PortFace.North, PortFace.East, PortFace.South, PortFace.West };
            Assert.That(BoardStateCodec.MaskOf(all), Is.EqualTo(1 | 2 | 4 | 8), "N=1 E=2 S=4 W=8");
            Assert.That(BoardStateCodec.FacesOf(0).Length, Is.EqualTo(0), "0 은 빈 집합");

            foreach (PortFace f in all)
            {
                PortFace[] one = BoardStateCodec.FacesOf(BoardStateCodec.BitOf(f));
                Assert.That(one.Length, Is.EqualTo(1), $"{f} 하나만 나와야 한다");
                Assert.That(one[0], Is.EqualTo(f), $"{f} 가 다른 면으로 폈다");
            }
        }

        // ── 거들 ───────────────────────────────────────────────────────────────

        private static int Count<T>(IEnumerable<T> items)
        {
            int n = 0;
            foreach (T _ in items) n++;
            return n;
        }

        private static void Place(BoardGrid g, StartingBoard.Run run)
        {
            if (run.merger)
                g.TryPlaceBeltElement(run.cell, BeltElementKind.Merger,
                    StartingBoard.MergerInFaces(run.outFace), new[] { run.outFace },
                    FlowKind.None, out _);
            else
                g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
        }
    }
}
