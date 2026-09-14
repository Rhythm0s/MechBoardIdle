using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 마운트 표시 — **자리 · 슬롯 · 채움 · 점멸**
    /// (2026-09-14 신설 · 플랜 §71-41 → **§72-5 사용자 확정으로 개정**).
    ///
    /// **무엇을 지키는가.** 묶음이 **실루엣 바깥**에 서는가, **1슬롯 = 한 칸**으로 넷씩 쌓이는가,
    /// B 의 왼쪽·오른쪽이 **같은 여덟을 나눠 비추는가**, 재고 0 에 **점멸이 서는가**다.
    ///
    /// ⚠️ 첫 판의 가정 넷(칸 크기·틈·띄움·폴백 크기)은 §72-5 로 **폐기됐다** —
    /// 슬롯이 보드 칸과 같으면 잴 자가 화면에 이미 있다.
    /// </summary>
    public sealed class MountDisplayTests
    {
        /// <summary>
        /// **묶음은 실루엣 바깥 열에 선다** — 마운트는 보드 없는 소비 파츠다(조립 6장).
        /// 격자 칸에 넣으면 **노드를 놓을 수 있는 자리**가 되어 버린다.
        /// </summary>
        [Test]
        public void 슬롯_묶음은_바깥_열에_넷씩_쌓인다()
        {
            // 로봇 A — 포트 (0,6) 서면 → x −1 · y 5~8.
            Assert.AreEqual(new Vector2Int(-1, 5),
                MountDisplay.SlotCell(new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA, 0));
            Assert.AreEqual(new Vector2Int(-1, 8),
                MountDisplay.SlotCell(new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA, 3));

            // 로봇 B — 왼쪽 어깨 (0,10) 서면 → x −1 · y 9~12.
            Assert.AreEqual(new Vector2Int(-1, 9),
                MountDisplay.SlotCell(new Vector2Int(0, 10), PortFace.West, MountOwner.RobotB, 0));
            Assert.AreEqual(new Vector2Int(-1, 12),
                MountDisplay.SlotCell(new Vector2Int(0, 10), PortFace.West, MountOwner.RobotB, 3));

            // 로봇 B — 오른쪽 어깨 (11,10) 동면 → x 12 · y 9~12.
            Assert.AreEqual(new Vector2Int(12, 9),
                MountDisplay.SlotCell(new Vector2Int(11, 10), PortFace.East, MountOwner.RobotB, 0));

            // 포트 셋 전부가 격자 밖으로 나가는지 — 하나라도 안에 남으면 노드 자리와 겹친다.
            foreach (MountPort mp in PartLayout.MountPorts)
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                {
                    Vector2Int c = MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, i);
                    Assert.IsTrue(c.x < 0 || c.x >= PartLayout.Columns,
                        $"{mp.owner} 의 슬롯이 격자 안에 남았다: {c}");
                }
        }

        /// <summary>
        /// **A 와 B 는 같은 열에서 위아래로 맞닿는다** — A y5~8 · B y9~12.
        ///
        /// ⚠️ 이것이 **그림 칸을 한 칸 더 바깥으로 뺀 이유**다. 겹치면 묶음 하나가
        /// 다른 묶음의 슬롯을 가린다.
        /// </summary>
        [Test]
        public void 두_묶음이_겹치지_않는다()
        {
            var used = new System.Collections.Generic.HashSet<Vector2Int>();

            foreach (MountPort mp in PartLayout.MountPorts)
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                {
                    Vector2Int c = MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, i);
                    Assert.IsTrue(used.Add(c), $"슬롯 칸이 겹친다: {c}");
                }

            // 그림 칸도 슬롯과 겹치면 안 된다 — 겹치면 적재가 그림에 가려진다.
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                Vector2Int b = MountDisplay.BodyCell(mp.cell, mp.face, mp.owner);
                Assert.IsFalse(used.Contains(b), $"그림 칸이 슬롯을 덮는다: {b}");
            }
        }

        /// <summary>
        /// 그림 칸은 묶음보다 **한 칸 더 바깥**이다 — 그래서 스크롤 여유가 **두 칸** 있어야 한다.
        /// </summary>
        [Test]
        public void 그림_칸은_묶음보다_한_칸_더_바깥이다()
        {
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                int slotX = MountDisplay.SlotColumn(mp.cell, mp.face);
                Vector2Int body = MountDisplay.BodyCell(mp.cell, mp.face, mp.owner);

                Assert.AreEqual(1, Mathf.Abs(body.x - slotX), "그림은 묶음 옆 한 칸이다");
                Assert.IsTrue(body.x == -2 || body.x == PartLayout.Columns + 1,
                    $"그림 칸이 두 칸 여유 밖으로 나갔다: {body}");
            }
        }

        /// <summary>
        /// **슬롯 수는 코어가 든 값을 가리킨다** — 여기서 새로 정하면 두 곳이 갈린다.
        ///
        /// ⚠️ B 의 왼쪽·오른쪽은 **표시 순서**를 나눈 것이지 적재를 나눈 것이 아니다(§72-5).
        /// </summary>
        [Test]
        public void B는_같은_여덟을_왼쪽_넷_오른쪽_넷으로_비춘다()
        {
            Assert.AreEqual(4, MountDisplay.SlotsOf(MountOwner.RobotA));
            Assert.AreEqual(8, MountDisplay.SlotsOf(MountOwner.RobotB));
            Assert.AreEqual(MountLoad.SlotsRobotA, MountDisplay.SlotsOf(MountOwner.RobotA),
                "코어의 값과 갈리면 안 된다");
            Assert.AreEqual(MountLoad.SlotsRobotB, MountDisplay.SlotsOf(MountOwner.RobotB));

            Assert.AreEqual(0, MountDisplay.FirstSlotOf(PortFace.West), "왼쪽이 0~3");
            Assert.AreEqual(4, MountDisplay.FirstSlotOf(PortFace.East), "오른쪽이 4~7");

            // 두 어깨가 비추는 번호를 합치면 **정확히 여덟**이고 겹치지 않는다.
            var shown = new System.Collections.Generic.HashSet<int>();
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                if (mp.owner != MountOwner.RobotB) continue;
                int first = MountDisplay.FirstSlotOf(mp.face);
                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                    Assert.IsTrue(shown.Add(first + i), "같은 슬롯을 두 번 비춘다");
            }
            Assert.AreEqual(MountLoad.SlotsRobotB, shown.Count, "여덟이 한 번씩 다 보인다");
        }

        /// <summary>
        /// **0이면 점멸한다** (UI 문서 12-1). 이것이 전투 화면의 결과를 조립 화면으로
        /// 끌고 오는 **유일한 고리**다. 점멸 단위는 **묶음 전체**다(§72-5).
        ///
        /// ⚠️ **전투가 안 돌 때는 안 깜빡인다** — 씬을 열자마자 경고가 뜨면 안 된다.
        /// </summary>
        [Test]
        public void 재고가_0이면_점멸한다()
        {
            Assert.IsTrue(MountDisplay.Blinks(hasCombat: true, mountTotal: 0f));
            Assert.IsFalse(MountDisplay.Blinks(hasCombat: true, mountTotal: 1f));
            Assert.IsFalse(MountDisplay.Blinks(hasCombat: false, mountTotal: 0f));

            // 판정을 새로 짓지 않고 규칙을 부른다 — 두 곳이 갈리면 박자가 어긋난다.
            Assert.AreEqual(SupplyStopRules.MountIsEmpty(true, 0f),
                MountDisplay.Blinks(true, 0f));
        }

        /// <summary>
        /// 채움의 분모는 **스택 상한**이다(§72-5 「AmountAt ÷ 10」).
        /// ⚠️ **상한이 0이면 0** — 나누지 않는다. 무한대가 나오면 칸이 통째로 차 보여
        /// **재고가 없는데 가득 찬 묶음**이 된다.
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
        /// 색은 **벨트와 같은 표**에서 온다 — 같은 탄이 벨트 위와 마운트에서 다른 색이면
        /// 둘이 같은 것이라는 게 화면에서 안 읽힌다.
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
