using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **노드를 놓을 때 방향을 묻는 누름 자리** (2026-10-01 사용자 확정 · WBS 원문).
    ///
    /// 원문이 정한 것 넷을 그대로 잰다 —
    /// 3 × 3 에서 노드 칸을 뺀 자리 **전부**가 눌린다 · 대각선 X 자로 **넷**으로 나뉜다 ·
    /// 노드 칸은 **확정** · 3 × 3 **밖은 취소**.
    /// </summary>
    public sealed class NodeDirectionPadTests
    {
        private static DirectionPadZone Z(float dx, float dy) =>
            NodeDirectionPad.Resolve(dx, dy, out _);

        private static PortFace F(float dx, float dy)
        {
            NodeDirectionPad.Resolve(dx, dy, out PortFace f);
            return f;
        }

        [Test]
        public void 노드_칸은_확정이다()
        {
            Assert.AreEqual(DirectionPadZone.Confirm, Z(0f, 0f), "한가운데");
            Assert.AreEqual(DirectionPadZone.Confirm, Z(0.49f, 0.49f), "노드 칸 모서리 안쪽");
            Assert.AreEqual(DirectionPadZone.Confirm, Z(-0.5f, 0.5f), "노드 칸 경계");
        }

        [Test]
        public void 삼삼_밖은_취소다()
        {
            Assert.AreEqual(DirectionPadZone.Outside, Z(1.51f, 0f), "오른쪽 밖");
            Assert.AreEqual(DirectionPadZone.Outside, Z(0f, -1.51f), "아래 밖");
            Assert.AreEqual(DirectionPadZone.Outside, Z(1.6f, 1.6f), "대각 밖");
            Assert.AreEqual(DirectionPadZone.Outside, Z(0.2f, 9f), "한참 밖");
        }

        /// <summary>⚠️ **옆 칸 넷**은 당연히 그 쪽이다.</summary>
        [Test]
        public void 옆_칸_넷이_제_쪽을_가리킨다()
        {
            Assert.AreEqual(PortFace.East, F(1f, 0f));
            Assert.AreEqual(PortFace.West, F(-1f, 0f));
            Assert.AreEqual(PortFace.North, F(0f, 1f));
            Assert.AreEqual(PortFace.South, F(0f, -1f));
        }

        /// <summary>
        /// ⚠️⚠️ **대각 칸도 눌린다** — 원문이 「노드 칸을 뺀 자리 **전부**」라고 못 박았다.
        /// 비우면 그 자리를 누른 손이 아무 일도 못 한다.
        /// </summary>
        [Test]
        public void 대각_칸도_가까운_쪽으로_잡힌다()
        {
            // 오른쪽 위 칸 안에서 가로로 더 먼 점 → 동
            Assert.AreEqual(DirectionPadZone.Face, Z(1.3f, 0.8f));
            Assert.AreEqual(PortFace.East, F(1.3f, 0.8f));

            // 같은 칸 안에서 세로로 더 먼 점 → 북
            Assert.AreEqual(PortFace.North, F(0.8f, 1.3f));

            // 왼쪽 아래 칸
            Assert.AreEqual(PortFace.West, F(-1.3f, -0.8f));
            Assert.AreEqual(PortFace.South, F(-0.8f, -1.3f));
        }

        /// <summary>
        /// ⚠️ **정확히 대각선 위**는 어느 쪽에도 더 가깝지 않다. 어느 쪽으로 정하든
        /// 임의지만 **한 쪽으로 정해져** 있어야 같은 자리를 두 번 눌러 답이 달라지지 않는다.
        /// </summary>
        [Test]
        public void 대각선_위는_늘_같은_답이다()
        {
            Assert.AreEqual(F(1f, 1f), F(1f, 1f));
            Assert.AreEqual(PortFace.North, F(1f, 1f), "규칙: 대각선 위는 세로로 잡는다");
            Assert.AreEqual(PortFace.South, F(-1f, -1f));
        }

        /// <summary>3 × 3 안은 **노드 칸이 아니면 전부** 면이다 — 빈 자리가 없다.</summary>
        [Test]
        public void 삼삼_안에_빈_자리가_없다()
        {
            for (float dx = -1.5f; dx <= 1.5f; dx += 0.1f)
            for (float dy = -1.5f; dy <= 1.5f; dy += 0.1f)
            {
                DirectionPadZone z = Z(dx, dy);

                // ⚠️ **셈을 코드와 똑같이 한다.** 0.1 씩 더해 만든 수는 0.5 를 아주 조금
                //    넘거나 모자라므로, 여기서 느슨한 여유를 두면 경계에서 시험이 저 혼자
                //    다른 답을 기대한다(실제로 (-0.50, 0.50) 에서 빨개졌다).
                bool inNodeCell = UnityEngine.Mathf.Abs(dx) <= NodeDirectionPad.HalfCell
                               && UnityEngine.Mathf.Abs(dy) <= NodeDirectionPad.HalfCell;

                if (inNodeCell) Assert.AreEqual(DirectionPadZone.Confirm, z, $"({dx:F2},{dy:F2})");
                else Assert.AreEqual(DirectionPadZone.Face, z, $"({dx:F2},{dy:F2}) 가 비어 있다");
            }
        }

        // ──────────────────── 돌리기 ────────────────────

        [Test]
        public void 누른_쪽으로_출력이_가게_돌린다()
        {
            Assert.AreEqual(0, NodeDirectionPad.QuarterTurns(PortFace.North, PortFace.North));
            Assert.AreEqual(1, NodeDirectionPad.QuarterTurns(PortFace.North, PortFace.East));
            Assert.AreEqual(2, NodeDirectionPad.QuarterTurns(PortFace.North, PortFace.South));
            Assert.AreEqual(3, NodeDirectionPad.QuarterTurns(PortFace.North, PortFace.West));
        }

        /// <summary>⚠️ 돌린 결과가 실제로 그 면이 되는가 — 회전 규칙과 **맞물리는지** 본다.</summary>
        [Test]
        public void 돌린_뒤_출력이_정말_그_면이다()
        {
            foreach (PortFace from in new[] { PortFace.North, PortFace.East, PortFace.South, PortFace.West })
            foreach (PortFace to in new[] { PortFace.North, PortFace.East, PortFace.South, PortFace.West })
            {
                int q = NodeDirectionPad.QuarterTurns(from, to);
                Assert.AreEqual(to, NodeConnectionRules.Rotate(from, q),
                    $"{from} → {to} 로 {q} 번 돌렸는데 다른 면이 됐다");
            }
        }

        /// <summary>네 바퀴면 제자리다 — 돌림 수가 0~3 밖으로 안 나간다.</summary>
        [Test]
        public void 돌림_수는_0에서_3_사이다()
        {
            foreach (PortFace from in new[] { PortFace.North, PortFace.East, PortFace.South, PortFace.West })
            foreach (PortFace to in new[] { PortFace.North, PortFace.East, PortFace.South, PortFace.West })
            {
                int q = NodeDirectionPad.QuarterTurns(from, to);
                Assert.GreaterOrEqual(q, 0);
                Assert.LessOrEqual(q, 3);
            }
        }
    }
}
