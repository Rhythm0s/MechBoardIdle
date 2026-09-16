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
            // A — 포트가 **(1,4) 남면**으로 내려왔다(2026-09-14 · §72-24). 표시 열은
            // **그대로 x1 · y0~3** 이고, 이제 도착 칸 (1,3) 이 **묶음 맨 윗 칸과 같다.**
            //
            // ⚠️ **위부터 찬다** — 물건이 위에서 떨어지므로 **첫 슬롯이 맨 윗 칸(y3)** 이다.
            // 구판은 포트가 팔R 바깥이라 열과 도착 칸이 따로 놀았고 첫 슬롯이 y0 이었다(폐기).
            Assert.AreEqual(new Vector2Int(1, 3),
                MountDisplay.SlotCell(new Vector2Int(1, 4), PortFace.South, MountOwner.RobotA, 0));
            Assert.AreEqual(new Vector2Int(1, 0),
                MountDisplay.SlotCell(new Vector2Int(1, 4), PortFace.South, MountOwner.RobotA, 3));

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

            // ⚠️ **그림 칸은 폐기됐다**(§72-13) — 그림은 묶음 네 칸 **위에 겹쳐** 그린다.
            // 그러므로 「그림이 슬롯을 덮는가」는 이제 물음이 아니다. 대신 묶음이
            // **네 칸을 차지하는지**를 본다 — 그림 높이가 여기에 맞춰 늘어난다.
            Assert.AreEqual(MountDisplay.SlotsPerPort, MountDisplay.GroupHeightCells,
                "그림은 슬롯 넷만큼 늘어난다");
            Assert.AreEqual(PartLayout.MountPorts.Count * MountDisplay.SlotsPerPort, used.Count,
                "포트마다 넷씩, 겹침 없이");
        }

        /// <summary>
        /// **바깥으로 나가는 것이 하나도 없다** (2026-09-14 · A 묶음도 격자 안으로).
        ///
        /// 그래서 **가로 여유가 0** 이 됐다 — 격자 폭 그대로 스크롤을 조인다.
        /// A 는 팔R 아래 빈 칸(x1 · y0~3) · B 는 머리 옆 빈 열(x3 · x8)이다.
        /// </summary>
        [Test]
        public void 묶음이_모두_격자_안에_있다()
        {
            foreach (MountPort mp in PartLayout.MountPorts)
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                {
                    Vector2Int c = MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, i);

                    Assert.IsTrue(c.x >= 0 && c.x < PartLayout.Columns,
                        $"{mp.owner} 의 묶음이 가로로 격자 밖이다: {c}");
                    Assert.IsTrue(c.y >= 0 && c.y < PartLayout.Rows,
                        $"{mp.owner} 의 묶음이 세로로 격자 밖이다: {c}");

                    // 그러면서도 **노드는 못 놓는 자리**여야 한다 — 둘 다 지켜야 한다.
                    Assert.IsFalse(PartLayout.IsValid(c),
                        $"{mp.owner} 의 슬롯 {c} 에 노드를 놓을 수 있다");
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

            // A — 묶음이 팔R **바로 아래**라 몸이 **위**에 있다. 좌우로 가를 것이 없다.
            Assert.IsFalse(MountDisplay.FlipX(
                new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA));

            // ⚠️ **B 둘이 같으면 한쪽이 안 뒤집힌 것이다** — 대칭이 깨진다.
            bool leftPlain = false, rightFlipped = false;
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                if (mp.owner != MountOwner.RobotB) continue;
                bool f = MountDisplay.FlipX(mp.cell, mp.face, mp.owner);
                bool onLeft = MountDisplay.SlotColumn(mp.cell, mp.face) * 2
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

        // ── 묶음이 서는 자리 (2026-09-16 · 육안 9차 ⑥) ─────────────────────────

        /// <summary>
        /// **채우는 차례와 서는 자리는 다른 물음이다.**
        ///
        /// ⚠️⚠️ A 는 위부터 찬다(§72-24) — 슬롯 0번이 묶음의 **맨 윗 칸**이다.
        /// 그림을 세우는 코드가 그것을 밑변으로 알고 위로 반 묶음을 올려서,
        /// 화면에서 **총 그림이 격자 위쪽에 떠 있고 네 칸은 비어** 보였다.
        ///
        /// 📌 여기서 지키는 것은 「밑변은 언제나 묶음의 가장 아랫 줄」 하나다.
        /// </summary>
        [Test]
        public void A묶음의_밑변은_슬롯0번이_아니다()
        {
            var port = new Vector2Int(1, 4);

            Vector2Int slot0 = MountDisplay.SlotCell(port, PortFace.South, MountOwner.RobotA, 0);
            Vector2Int bottom = MountDisplay.GroupBottomCell(port, PortFace.South, MountOwner.RobotA);

            Assert.That(slot0.y, Is.GreaterThan(bottom.y),
                "A 는 위부터 차므로 슬롯 0번이 밑변보다 위여야 한다 — 같으면 이 시험의 전제가 깨졌다");
            Assert.That(bottom.y, Is.EqualTo(MountDisplay.SlotRowStart(MountOwner.RobotA)),
                "밑변은 묶음의 가장 아랫 줄이다");
        }

        [Test]
        public void 밑변은_네_칸_중_가장_아래다()
        {
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                var port = new Vector2Int(2, 10);
                Vector2Int bottom = MountDisplay.GroupBottomCell(port, PortFace.West, owner);

                int lowest = int.MaxValue;
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                    lowest = Mathf.Min(lowest, MountDisplay.SlotCell(port, PortFace.West, owner, i).y);

                Assert.That(bottom.y, Is.EqualTo(lowest), $"{owner} — 밑변이 네 칸의 최저가 아니다");
            }
        }

        [Test]
        public void 밑변의_열은_묶음의_열과_같다()
        {
            var port = new Vector2Int(9, 10);
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                Vector2Int bottom = MountDisplay.GroupBottomCell(port, PortFace.East, owner);
                Assert.That(bottom.x, Is.EqualTo(MountDisplay.GroupColumn(port, PortFace.East, owner)),
                    $"{owner} — 밑변이 다른 열에 있다");
            }
        }

    }
}
