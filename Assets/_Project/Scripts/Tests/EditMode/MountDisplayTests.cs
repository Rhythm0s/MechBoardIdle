using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 마운트 표시 — **자리 · 슬롯 수 · 점멸** (2026-09-14 신설 · 플랜 §71-41 · UI 문서 12-1·12-4).
    ///
    /// **무엇을 지키는가.** 마운트 본체가 **실루엣 바깥**에 서는가, 슬롯이 **A 4 · B 8**인가,
    /// 재고가 0일 때 **점멸이 서는가** 셋이다.
    ///
    /// ⚠️ 칸 크기·간격·띄움은 전부 **가정**이라 시험이 값을 못 박지 않는다 —
    /// 못 박으면 설계가 UI 문서 12-4 에 값을 적는 순간 **시험이 먼저 빨강**이 된다.
    /// </summary>
    public sealed class MountDisplayTests
    {
        /// <summary>
        /// **마운트는 실루엣 바깥에 선다** — 보드 없는 소비 파츠다(조립 기획서 6장).
        /// 격자 칸에 넣으면 **노드를 놓을 수 있는 자리**가 되어 버린다.
        /// </summary>
        [Test]
        public void 마운트_자리는_포트_바깥_한_칸이다()
        {
            // 로봇 A — (0,6) 서면 → (−1,6). **음수 x 가 나오는 것이 맞다**(격자 밖이라는 뜻).
            Assert.AreEqual(new Vector2Int(-1, 6),
                MountDisplay.VirtualCell(new Vector2Int(0, 6), PortFace.West));

            // 로봇 B — (0,10) 서면 · (11,10) 동면. 어깨가 둘이라 자리도 둘이다.
            Assert.AreEqual(new Vector2Int(-1, 10),
                MountDisplay.VirtualCell(new Vector2Int(0, 10), PortFace.West));
            Assert.AreEqual(new Vector2Int(12, 10),
                MountDisplay.VirtualCell(new Vector2Int(11, 10), PortFace.East));

            // 실제 포트 셋이 전부 격자 밖으로 나가는지 — 하나라도 안에 남으면 노드 자리와 겹친다.
            foreach (MountPort mp in PartLayout.MountPorts)
            {
                Vector2Int v = MountDisplay.VirtualCell(mp.cell, mp.face);
                Assert.IsTrue(v.x < 0 || v.x >= PartLayout.Columns,
                    $"{mp.owner} 의 마운트가 격자 안에 남았다: {v}");
            }
        }

        /// <summary>
        /// **슬롯 수는 코어가 든 값을 가리킨다** — 여기서 새로 정하면 두 곳이 갈린다.
        /// </summary>
        [Test]
        public void 슬롯은_A_넷_B_여덟이다()
        {
            Assert.AreEqual(4, MountDisplay.SlotsOf(MountOwner.RobotA));
            Assert.AreEqual(8, MountDisplay.SlotsOf(MountOwner.RobotB));

            Assert.AreEqual(MountLoad.SlotsRobotA, MountDisplay.SlotsOf(MountOwner.RobotA),
                "코어의 값과 갈리면 안 된다");
            Assert.AreEqual(MountLoad.SlotsRobotB, MountDisplay.SlotsOf(MountOwner.RobotB));

            // B 는 두 줄이다 — 여덟을 한 줄로 늘어놓으면 그리드가 마운트 그림보다 길어진다.
            Assert.AreEqual(1, MountDisplay.RowsOf(MountOwner.RobotA));
            Assert.AreEqual(2, MountDisplay.RowsOf(MountOwner.RobotB));
            Assert.AreEqual(4, MountDisplay.ColumnsOf(MountOwner.RobotA));
            Assert.AreEqual(4, MountDisplay.ColumnsOf(MountOwner.RobotB), "여덟을 두 줄이면 넷씩");

            // 줄 × 칸이 슬롯을 덮어야 한다 — 모자라면 **뒤쪽 슬롯이 화면에서 사라진다.**
            foreach (MountOwner o in new[] { MountOwner.RobotA, MountOwner.RobotB })
                Assert.GreaterOrEqual(MountDisplay.RowsOf(o) * MountDisplay.ColumnsOf(o),
                    MountDisplay.SlotsOf(o), $"{o} 의 그리드가 슬롯을 다 못 담는다");
        }

        /// <summary>
        /// **0이면 점멸한다** (UI 문서 12-1). 이것이 전투 화면의 결과를 조립 화면으로
        /// 끌고 오는 **유일한 고리**다.
        ///
        /// ⚠️ **전투가 안 돌 때는 안 깜빡인다** — 씬을 열자마자 경고가 뜨면 안 된다.
        /// 없는 값과 0을 가르는 자리다.
        /// </summary>
        [Test]
        public void 재고가_0이면_점멸한다()
        {
            Assert.IsTrue(MountDisplay.Blinks(hasCombat: true, mountTotal: 0f));
            Assert.IsFalse(MountDisplay.Blinks(hasCombat: true, mountTotal: 1f));

            // 전투가 아직 안 돌면 0 은 「떨어졌다」가 아니라 「아직 없다」다.
            Assert.IsFalse(MountDisplay.Blinks(hasCombat: false, mountTotal: 0f));

            // 판정을 새로 짓지 않고 규칙을 부른다 — 두 곳이 갈리면 박자가 어긋난다.
            Assert.AreEqual(SupplyStopRules.MountIsEmpty(true, 0f),
                MountDisplay.Blinks(true, 0f));
        }

        /// <summary>
        /// 채움의 분모는 **스택 상한**이다. ⚠️ **상한이 0이면 0** — 나누지 않는다.
        /// 무한대가 나오면 칸이 통째로 차 보여 **재고가 없는데 가득 찬 그리드**가 된다.
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
