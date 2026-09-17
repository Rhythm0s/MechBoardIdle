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
        public void 판_둘이_각자_자기_주인으로_왕복한다()
        {
            // **보드가 로봇별로 갈렸다**(2026-09-16). 왕복이 **둘 다** 서야 한다 —
            // 한쪽만 서면 나머지 하나는 매번 시작 보드로 돌아간다.
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                var src = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, owner);
                Assert.IsTrue(src.TryPlace(new Vector2Int(2, 3), Node("core"), out _));
                Assert.IsTrue(src.TryPlaceBelt(new Vector2Int(3, 3),
                    PortFace.West, PortFace.East, FlowKind.None, out _));

                BoardStateV1 state = BoardStateCodec.Capture(src);
                Assert.AreEqual((int)owner, state.owner, "판이 자기 주인을 안 싣는다");

                var dst = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, owner);
                Assert.IsTrue(BoardStateCodec.Fits(state, dst), $"{owner} 판이 자기 격자에 안 맞는다");
                Assert.AreEqual(0, BoardStateCodec.Restore(state, dst, NodeLookup(), ModuleLookup(), null, null));

                Assert.IsNotNull(dst.GetAt(new Vector2Int(2, 3)), $"{owner} 노드가 안 돌아왔다");
                Assert.IsNotNull(dst.GetBeltAt(new Vector2Int(3, 3)), $"{owner} 벨트가 안 돌아왔다");
            }
        }

        [Test]
        public void 남의_판은_안_푼다()
        {
            // ⚠️ 주인이 다르다. 이것을 안 보면 A 판이 B 자리에 **에러 없이** 들어앉고,
            //    그때 마운트가 엉뚱한 로봇에게 붙는다.
            //
            // 🗑️ 구 주석 「크기도 세대도 같다 — 주인만 다르다」는 **2026-09-17 에 반만
            //    맞는 말이 됐다.** 표식이 주인별이 되면서 남의 판은 세대도 함께 어긋난다.
            //    그래도 이 시험이 보는 것은 **까닭을 무엇이라 적는가**이고,
            //    답은 여전히 「주인」이어야 한다 — 그쪽이 고칠 자리를 가리킨다.
            var a = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotA);
            BoardStateV1 state = BoardStateCodec.Capture(a);

            var b = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotB);
            Assert.IsFalse(BoardStateCodec.Fits(state, b), "남의 판을 받아들였다");
            StringAssert.Contains("주인", BoardStateCodec.WhyNotFit(state, b),
                "왜 안 맞는지가 주인 때문이라고 안 적힌다");
        }

        [Test]
        public void 저장은_판_둘을_주인으로_찾는다()
        {
            // 차례가 아니라 **주인**으로 찾는다 — 한 칸 밀려도 안 섞인다.
            var save = new SaveDataV1();
            Assert.IsNull(save.BoardOf(MountOwner.RobotA), "빈 저장인데 판이 있다");

            var a = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotA);
            var b = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotB);
            save.SetBoard(MountOwner.RobotB, BoardStateCodec.Capture(b));
            save.SetBoard(MountOwner.RobotA, BoardStateCodec.Capture(a));

            Assert.AreEqual((int)MountOwner.RobotA, save.BoardOf(MountOwner.RobotA).owner);
            Assert.AreEqual((int)MountOwner.RobotB, save.BoardOf(MountOwner.RobotB).owner);
            Assert.AreEqual(2, save.boards.Count, "같은 주인이 두 번 들어갔다");

            // 같은 주인을 다시 넣으면 **갈아 끼운다** — 쌓이면 어느 것이 최신인지 답이 둘이 된다.
            save.SetBoard(MountOwner.RobotA, BoardStateCodec.Capture(a));
            Assert.AreEqual(2, save.boards.Count);
        }

        [Test]
        public void 구_저장의_한_판은_목록으로_옮겨진다()
        {
            // ⚠️ **이미 나간 저장이 있다.** 이주를 안 하면 그 사람들의 A 판이 조용히 사라진다.
            var a = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotA);
            var save = new SaveDataV1 { board = BoardStateCodec.Capture(a) };

            save.MigrateLegacyBoard();

            Assert.IsNull(save.board, "구 칸이 안 비워졌다 — 다음에 또 옮긴다");
            Assert.IsNotNull(save.BoardOf(MountOwner.RobotA), "구 판이 목록에 안 들어왔다");
        }

        [Test]
        public void 이주는_새_저장을_안_덮는다()
        {
            // 구 칸이 새 목록을 덮으면 **최신 판이 옛 판으로 되돌아간다.**
            var a = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotA);
            Assert.IsTrue(a.TryPlace(new Vector2Int(1, 1), Node("core"), out _));

            var save = new SaveDataV1();
            save.SetBoard(MountOwner.RobotA, BoardStateCodec.Capture(a)); // 새 목록 = 노드 하나

            var empty = new BoardGrid(Cols, Rows, 1f, Vector2.zero, null, MountOwner.RobotA);
            save.board = BoardStateCodec.Capture(empty);                  // 구 칸 = 빈 판

            save.MigrateLegacyBoard();

            Assert.AreEqual(1, save.BoardOf(MountOwner.RobotA).nodes.Count,
                "구 칸의 빈 판이 새 목록을 덮었다");
        }

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

        // ── 6-2. 시작 보드 세대 ────────────────────────────────────────────────

        [Test]
        public void 옛_세대의_저장은_버린다()
        {
            // ⚠️⚠️ **격자 크기만으로는 못 거른다.** 09-15 에 시작 보드가 네 줄 → 두 줄로
            //    바뀌었는데 크기는 그대로 12x14 였다 — 옛 배치가 새 규격 위에 **조용히**
            //    올라와 「벨트를 이었는데 마운트 적재 0」이 됐다(육안 9차 ⑦).
            BoardGrid before = Grid();
            before.TryPlace(new Vector2Int(2, 2), Node("N_old"), out _);
            BoardStateV1 saved = BoardStateCodec.Capture(before);

            saved.generation = "deadbeef";   // 옛 세대인 척한다

            BoardGrid after = Grid();
            Assert.That(BoardStateCodec.Fits(saved, after), Is.False, "옛 세대를 받아들였다");
            Assert.That(BoardStateCodec.Restore(saved, after, NodeLookup()), Is.EqualTo(-1));
            Assert.That(Count(after.Nodes), Is.EqualTo(0), "하나라도 놓았으면 안 된다");

            // 왜 버리는지 말할 수 있어야 한다 — 조용히 사라지면 플레이어가 이유를 못 본다.
            Assert.That(BoardStateCodec.WhyNotFit(saved, after), Does.Contain("세대"));
        }

        [Test]
        public void 같은_세대의_저장은_되살린다()
        {
            BoardGrid before = Grid();
            before.TryPlace(new Vector2Int(2, 2), Node("N_now"), out _);
            BoardStateV1 saved = BoardStateCodec.Capture(before);

            Assert.That(saved.generation, Is.EqualTo(StartingBoard.Generation),
                "새길 때 지금 세대를 안 찍었다");

            BoardGrid after = Grid();
            Assert.That(BoardStateCodec.Fits(saved, after), Is.True, "같은 세대인데 버렸다");
            Assert.That(BoardStateCodec.Restore(saved, after, NodeLookup()), Is.EqualTo(0));
            Assert.That(after.GetAt(new Vector2Int(2, 2)), Is.Not.Null);
            Assert.That(BoardStateCodec.WhyNotFit(saved, after), Is.Null, "맞는데 이유를 냈다");
        }

        [Test]
        public void 세대_표식이_없던_저장도_버린다()
        {
            // 이 기능 **전에** 쓰인 저장 — 필드가 비어 있다. 그것도 옛 것이다.
            BoardGrid before = Grid();
            BoardStateV1 saved = BoardStateCodec.Capture(before);
            saved.generation = null;

            Assert.That(BoardStateCodec.Fits(saved, Grid()), Is.False, "표식 없는 저장을 받아들였다");
        }

        [Test]
        public void 세대는_내용에서_나온다()
        {
            // 📌 손으로 올리는 버전 번호가 아니다 — 올리는 것을 잊으면 표식이 거짓말을 하고,
            //    그것은 표식이 없는 것보다 나쁘다. 배치가 바뀌면 해시가 저절로 바뀐다.
            Assert.That(StartingBoard.Generation, Is.Not.Null.And.Not.Empty);
            Assert.That(StartingBoard.Generation, Is.EqualTo(StartingBoard.Generation), "값이 흔들린다");
        }

        // ── 7. 왕복한 판이 실제로 나르는가 ──────────────────────────────────────

        /// <summary>
        /// 왕복 뒤에도 **물건이 흐르는가** (2026-09-16 · 육안 9차 ⑦ 「벨트를 이었는데 적재 0」).
        ///
        /// ⚠️⚠️ **수가 같은 것과 흐르는 것은 다른 일이다.** 앞의 시험들은 노드 수·벨트 수·면을
        /// 봤는데, 그것이 다 맞아도 **링크가 안 서면 한 알도 안 간다.** 면은 배치에서 다시
        /// 나오므로(`BeltAutoOrient` → `BeltFlow`) 부르는 쪽이 그것을 빠뜨리면 조용히 죽는다.
        ///
        /// 📌 그래서 여기서는 **링크 수**를 견준다 — 왕복 전과 후가 같아야 한다.
        /// </summary>
        [Test]
        public void 왕복한_판도_같은_링크를_만든다()
        {
            BoardGrid before = RealBoard();   // 튜토리얼을 마친 판

            int linksBefore = BeltRouting.BuildLinks(before).Count;
            Assert.That(linksBefore, Is.GreaterThan(0), "시험 전제가 깨졌다 — 원본에 링크가 없다");

            BoardGrid after = EmptyRealBoard();
            int missed = BoardStateCodec.Restore(
                BoardStateCodec.Capture(before), after, RealNodeLookup(), ModuleLookup());
            Assert.That(missed, Is.EqualTo(0), "왕복에서 못 놓은 것이 있다");

            // ⚠️ **부르는 쪽이 해야 하는 일**(설계 규칙 3) — 저장은 면·품목을 안 싣는다.
            BeltAutoOrient.Resolve(after);
            BeltFlow.Resolve(after);

            Assert.That(BeltRouting.BuildLinks(after).Count, Is.EqualTo(linksBefore),
                "왕복 뒤 링크 수가 달라졌다 — 판은 같아 보여도 **한 알도 안 간다**");
        }

        /// <summary>
        /// 왕복한 판에서도 **마운트에 닿는다** — 링크보다 한 단 더 간다.
        ///
        /// ⚠️ 링크가 서도 품목이 안 정해지면 안 흐른다. 실제로 틱을 돌려 도착을 센다.
        /// </summary>
        [Test]
        public void 왕복한_판도_마운트에_닿는다()
        {
            BoardGrid before = RealBoard();

            BoardGrid after = EmptyRealBoard();
            BoardStateCodec.Restore(BoardStateCodec.Capture(before), after, RealNodeLookup(), ModuleLookup());
            BeltAutoOrient.Resolve(after);
            BeltFlow.Resolve(after);

            Assert.That(ArrivalsIn(before), Is.GreaterThan(0), "원본이 안 나른다 — 전제가 깨졌다");
            Assert.That(ArrivalsIn(after), Is.GreaterThan(0),
                "왕복한 판이 마운트에 한 알도 안 보낸다 — 육안 ⑦ 의 모양이다");
        }

        /// <summary>
        /// **진짜 보드**를 짓는다 — 칸 수와 마스크가 `PartLayout` 에서 온다.
        ///
        /// ⚠️⚠️ 앞의 왕복 시험들은 14x14 짜리 맨 격자를 썼다. 앞뒤를 견주기만 하면
        /// 그것으로 되지만, **나르는지 보려면 안 된다** — 마운트 포트가 `PartLayout` 에
        /// 있어서 맨 격자에서는 **한 알도 안 닿는다.** 첫 판이 「원본이 안 나른다」로
        /// 떨어진 것이 그것이었고, 그것은 제품이 아니라 **이 시험의 결함**이었다.
        /// </summary>
        /// <summary>
        /// 시작 보드가 쓰는 **진짜 노드 자산**. 없으면 <c>null</c>.
        ///
        /// ⚠️⚠️ **가짜 노드로는 안 나른다.** 앞의 왕복 시험들은 `nodeId` 만 채운 빈
        /// `NodeDefinition` 을 쓴다 — 앞뒤를 견주기만 하면 그것으로 되지만, **포트도**
        /// **조합표도 없으므로 한 알도 안 만든다.** 09-16 첫 판이 「원본이 안 나른다」로
        /// 떨어진 진짜 이유였다(전투 신호는 그 다음 이유였다).
        /// </summary>
        private static NodeDefinition RealNode(string id)
            => UnityEditor.AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                "Assets/_Project/ScriptableObjects/Nodes/Node_" + id + ".asset");

        private static System.Func<string, NodeDefinition> RealNodeLookup() => RealNode;

        private BoardGrid RealBoard()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
            {
                NodeDefinition def = RealNode(s.nodeId);
                if (def == null) Assert.Ignore($"노드 자산이 없다({s.nodeId}) — 'MBI/Generate' 먼저");
                g.TryPlace(s.cell, def, out _);
            }
            foreach (StartingBoard.Run r in StartingBoard.Belts) Place(g, r);
            Place(g, StartingBoard.FillsEmptySlot);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>같은 규격의 빈 판 — 복원을 받을 자리.</summary>
        private static BoardGrid EmptyRealBoard()
            => new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());

        /// <summary>40초 동안 마운트에 닿은 횟수. 화면을 안 거친다.</summary>
        private static int ArrivalsIn(BoardGrid grid)
        {
            // ⚠️⚠️ **전투 신호를 켜야 마운트로 넘어간다.** 안 켜면 `PendingMountArrivals` 가
            //    영영 비고, 그러면 **제품이 아니라 이 시험이 0 을 만든다.**
            //    09-16 첫 판이 「원본이 안 나른다」로 떨어진 두 번째 이유였다.
            SupplySignals.Reset();
            SupplySignals.HasCombat = true;
            SupplySignals.ActiveOwner = MountOwner.RobotA;

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            int arrivals = 0;
            for (int i = 0; i < 800; i++)   // 0.05초 x 800 = 40초
            {
                BoardItemTick.Step(grid, flow, 0.05f, 1f);
                arrivals += flow.PendingMountArrivals.Count;
                flow.ClearPendingMountArrivals();
            }
            return arrivals;
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
