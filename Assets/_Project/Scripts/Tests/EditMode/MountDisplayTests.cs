using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 마운트 표시 — **자리 · 슬롯 · 채움 · 점멸**
    /// (2026-09-14 신설 · §71-41 → §72-5 → **§72-6 사용자 확정**).
    ///
    /// **무엇을 지키는가.** 묶음이 **노드를 못 놓는 자리**에 서는가, **1슬롯 = 한 칸**으로 넷씩
    /// 쌓이는가, B 의 왼쪽·오른쪽이 **같은 여덟을 나눠 비추는가**, 재고 0 에 **점멸이 서는가**다.
    ///
    /// ⚠️ §72-5 의 가정 넷(칸 크기·틈·띄움·폴백 크기)은 폐기됐다 —
    /// 슬롯이 보드 칸과 같으면 잴 자가 화면에 이미 있다.
    /// </summary>
    public sealed class MountDisplayTests
    {
        /// <summary>
        /// **A 는 실루엣 바깥 · B 는 머리 옆 빈 열**(§72-6).
        ///
        /// 구판은 B 도 바깥이었는데 **양쪽 어깨 밖**이라 한 화면에 둘 다 못 넣었다.
        /// </summary>
        [Test]
        public void 슬롯_묶음은_A는_바깥_B는_머리_옆이다()
        {
            // A — 포트 (0,6) 서면 → x −1 · y 5~8.
            Assert.AreEqual(new Vector2Int(-1, 5),
                MountDisplay.SlotCell(new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA, 0));
            Assert.AreEqual(new Vector2Int(-1, 8),
                MountDisplay.SlotCell(new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA, 3));

            // B 왼쪽 — 어깨R 안쪽 면 (2,10) 동면 → x 3 · y 10~13.
            Assert.AreEqual(new Vector2Int(3, 10),
                MountDisplay.SlotCell(new Vector2Int(2, 10), PortFace.East, MountOwner.RobotB, 0));
            Assert.AreEqual(new Vector2Int(3, 13),
                MountDisplay.SlotCell(new Vector2Int(2, 10), PortFace.East, MountOwner.RobotB, 3));

            // B 오른쪽 — 어깨L 안쪽 면 (9,10) 서면 → x 8 · y 10~13.
            Assert.AreEqual(new Vector2Int(8, 10),
                MountDisplay.SlotCell(new Vector2Int(9, 10), PortFace.West, MountOwner.RobotB, 0));
        }

        /// <summary>
        /// **어디에도 노드를 놓을 수 없다** — 마운트는 보드 없는 소비 파츠다(조립 6장).
        ///
        /// ⚠️ B 가 격자 **안**으로 들어왔으므로(§72-6) 「격자 밖인가」로는 이제 못 잰다.
        /// 재야 하는 것은 **놓을 수 있는 자리인가**다.
        /// </summary>
        [Test]
        public void 슬롯_자리에는_노드를_못_놓는다()
        {
            foreach (MountPort mp in PartLayout.MountPorts)
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                {
                    Vector2Int c = MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, i);
                    Assert.IsFalse(PartLayout.IsValid(c),
                        $"{mp.owner} 의 슬롯 {c} 에 노드를 놓을 수 있다");
                }
        }

        /// <summary>슬롯끼리도, 슬롯과 그림도 겹치지 않는다 — 겹치면 적재가 가려진다.</summary>
        [Test]
        public void 묶음과_그림이_겹치지_않는다()
        {
            var used = new System.Collections.Generic.HashSet<Vector2Int>();

            foreach (MountPort mp in PartLayout.MountPorts)
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                {
                    Vector2Int c = MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, i);
                    Assert.IsTrue(used.Add(c), $"슬롯 칸이 겹친다: {c}");
                }

            foreach (MountPort mp in PartLayout.MountPorts)
            {
                Vector2Int b = MountDisplay.BodyCell(mp.cell, mp.face, mp.owner);
                Assert.IsFalse(used.Contains(b), $"그림 칸이 슬롯을 덮는다: {b}");
                Assert.IsFalse(PartLayout.IsValid(b), $"그림 칸 {b} 에 노드를 놓을 수 있다");
            }
        }

        /// <summary>
        /// 그림 칸 — **A 는 x−2 · B 는 마운트 전용 줄(y13)의 x2 · x9**(§72-6).
        ///
        /// ⚠️ **B 는 격자 안에 든다** — 그래서 **세로 여유가 필요 없다.**
        /// 가로 여유 두 칸은 A 하나 때문에 남는다.
        /// </summary>
        [Test]
        public void 그림_칸은_A만_격자_밖이다()
        {
            int top = PartLayout.Rows - 1;

            Assert.AreEqual(new Vector2Int(-2, 6),
                MountDisplay.BodyCell(new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA));
            Assert.AreEqual(new Vector2Int(2, top),
                MountDisplay.BodyCell(new Vector2Int(2, 10), PortFace.East, MountOwner.RobotB));
            Assert.AreEqual(new Vector2Int(9, top),
                MountDisplay.BodyCell(new Vector2Int(9, 10), PortFace.West, MountOwner.RobotB));

            foreach (MountPort mp in PartLayout.MountPorts)
            {
                Vector2Int b = MountDisplay.BodyCell(mp.cell, mp.face, mp.owner);
                bool inside = b.x >= 0 && b.x < PartLayout.Columns
                              && b.y >= 0 && b.y < PartLayout.Rows;

                if (mp.owner == MountOwner.RobotB)
                    Assert.IsTrue(inside, $"B 의 그림이 격자 밖으로 나갔다: {b}");
                else
                    Assert.AreEqual(-2, b.x, "A 의 그림은 왼쪽 두 칸 여유 안이다");
            }
        }

        /// <summary>
        /// **왼쪽이 0~3 · 오른쪽이 4~7** — 표시 순서일 뿐 분배가 아니다(§72-5·§72-6).
        ///
        /// ⚠️ **면이 아니라 자리로 가른다.** §72-6 에서 B 포트가 어깨 안쪽으로 옮겨져
        /// **왼쪽 묶음이 동면**이 됐다 — 면으로 가르면 왼쪽이 4~7 을 비춘다.
        /// </summary>
        [Test]
        public void B는_같은_여덟을_왼쪽_넷_오른쪽_넷으로_비춘다()
        {
            Assert.AreEqual(4, MountDisplay.SlotsOf(MountOwner.RobotA));
            Assert.AreEqual(8, MountDisplay.SlotsOf(MountOwner.RobotB));
            Assert.AreEqual(MountLoad.SlotsRobotA, MountDisplay.SlotsOf(MountOwner.RobotA),
                "코어의 값과 갈리면 안 된다");
            Assert.AreEqual(MountLoad.SlotsRobotB, MountDisplay.SlotsOf(MountOwner.RobotB));

            Assert.AreEqual(0, MountDisplay.FirstSlotOf(3), "왼쪽 묶음이 0~3");
            Assert.AreEqual(4, MountDisplay.FirstSlotOf(8), "오른쪽 묶음이 4~7");
            Assert.AreEqual(0, MountDisplay.FirstSlotOf(-1), "A 도 0~3");

            // 두 어깨가 비추는 번호를 합치면 **정확히 여덟**이고 겹치지 않는다.
            var shown = new System.Collections.Generic.HashSet<int>();
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                if (mp.owner != MountOwner.RobotB) continue;
                int first = MountDisplay.FirstSlotOf(MountDisplay.SlotColumn(mp.cell, mp.face));
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                    Assert.IsTrue(shown.Add(first + i), "같은 슬롯을 두 번 비춘다");
            }
            Assert.AreEqual(MountLoad.SlotsRobotB, shown.Count, "여덟이 한 번씩 다 보인다");
        }

        /// <summary>
        /// **결합부가 몸 쪽을 보게 뒤집는다** (2026-09-14 실측).
        ///
        /// 그림 한 장을 양쪽에 쓰므로 **한쪽은 반드시 뒤집혀야** 대칭이 선다.
        /// 규격이 「실루엣에 붙는 변 여백 0」이라 여백을 재면 갈린다 —
        /// `mount_dronebay_b` 는 **왼쪽 0 · 오른쪽 18** 이므로 왼쪽이 붙는 변이다.
        ///
        /// ⚠️ A 는 규격으로 안 갈린다(`mount_gun_a` 는 좌우 여백이 둘 다 0) —
        /// 실루엣 **바깥**이라 몸이 늘 안쪽이라는 것으로 정했다.
        /// </summary>
        [Test]
        public void 그림은_몸_쪽으로_뒤집는다()
        {
            // B 왼쪽 — 어깨R(x0~2)이 왼쪽이다. 그대로.
            Assert.IsFalse(MountDisplay.FlipX(
                new Vector2Int(2, 10), PortFace.East, MountOwner.RobotB));

            // B 오른쪽 — 어깨L(x9~11)이 오른쪽이다. 뒤집는다.
            Assert.IsTrue(MountDisplay.FlipX(
                new Vector2Int(9, 10), PortFace.West, MountOwner.RobotB));

            // A — 실루엣 **바깥**이라 몸이 늘 안쪽(오른쪽)이다.
            Assert.IsTrue(MountDisplay.FlipX(
                new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA));

            // ⚠️ **B 둘이 같으면 한쪽이 안 뒤집힌 것이다** — 대칭이 깨진다.
            bool leftPlain = false, rightFlipped = false;
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                if (mp.owner != MountOwner.RobotB) continue;
                bool f = MountDisplay.FlipX(mp.cell, mp.face, mp.owner);
                bool onLeft = MountDisplay.BodyCell(mp.cell, mp.face, mp.owner).x * 2
                              < PartLayout.Columns;
                if (onLeft) leftPlain = !f;
                else rightFlipped = f;
            }
            Assert.IsTrue(leftPlain && rightFlipped, "B 양쪽의 방향이 서로 반대여야 한다");
        }

        /// <summary>
        /// **0이면 점멸한다** (UI 문서 12-1). 점멸 단위는 **묶음 전체**다.
        /// ⚠️ 전투가 안 돌 때는 안 깜빡인다 — 씬을 열자마자 경고가 뜨면 안 된다.
        /// </summary>
        [Test]
        public void 재고가_0이면_점멸한다()
        {
            Assert.IsTrue(MountDisplay.Blinks(hasCombat: true, mountTotal: 0f));
            Assert.IsFalse(MountDisplay.Blinks(hasCombat: true, mountTotal: 1f));
            Assert.IsFalse(MountDisplay.Blinks(hasCombat: false, mountTotal: 0f));

            Assert.AreEqual(SupplyStopRules.MountIsEmpty(true, 0f),
                MountDisplay.Blinks(true, 0f));
        }

        /// <summary>
        /// 채움의 분모는 **스택 상한**이다(§72-6 「AmountAt ÷ 10」).
        /// ⚠️ **상한이 0이면 0** — 나누지 않는다.
        /// </summary>
        [Test]
        public void 채움은_스택_상한으로_나눈다()
        {
            Assert.AreEqual(0.5f, MountDisplay.FillRatio(5f, 10f), 0.0001f);
            Assert.AreEqual(1f, MountDisplay.FillRatio(30f, 10f), 0.0001f, "넘쳐도 한 칸을 안 넘는다");
            Assert.AreEqual(0f, MountDisplay.FillRatio(5f, 0f), 0.0001f, "상한이 없으면 안 나눈다");
            Assert.AreEqual(0f, MountDisplay.FillRatio(-1f, 10f), 0.0001f);
        }

        /// <summary>
        /// 색은 **담긴 품목의 품목색**이다(§72-6) — 벨트와 같은 표를 쓴다.
        /// 같은 탄이 벨트 위와 마운트에서 다른 색이면 둘이 같은 것인지 안 읽힌다.
        /// </summary>
        [Test]
        public void 슬롯_색은_벨트_품목과_같은_갈래다()
        {
            Assert.AreEqual(FlowKind.StandardAmmo, MountDisplay.FlowOf(MountItem.Standard));
            Assert.AreEqual(FlowKind.PierceAmmo, MountDisplay.FlowOf(MountItem.Pierce));
            Assert.AreEqual(FlowKind.ExplosiveAmmo, MountDisplay.FlowOf(MountItem.Explosive));

            // 빈 슬롯은 갈래가 없다 — 색을 주면 **없는 것이 있는 것처럼** 보인다.
            Assert.AreEqual(FlowKind.None, MountDisplay.FlowOf(MountItem.None));
        }
    }
}
