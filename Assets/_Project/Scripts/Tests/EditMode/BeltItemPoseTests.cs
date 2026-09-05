using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 벨트 칸 안 아이템 위치(2026-09-05). 값이 아니라 **기하**를 고정한다.
    ///
    /// 여기서 지키는 것 하나: **코너에서 물건이 칸을 가로지르지 않는다.** 직선처럼 두 점을
    /// 이으면 지름길이 생겨 벨트 밖을 스치고, 그러면 「이 라인이 어디로 가는가」가 화면에서
    /// 안 읽힌다.
    /// </summary>
    public sealed class BeltItemPoseTests
    {
        private const float D = 1e-4f;

        private static Vector2 At(PortFace inF, PortFace outF, float t) =>
            BeltItemPose.LocalOffset(inF, outF, t);

        // ---- 직선 ----

        /// <summary>서→동 직선. 왼쪽 가장자리에서 오른쪽 가장자리로 곧게 간다.</summary>
        [Test]
        public void Straight_RunsEdgeToEdge_ThroughTheCentre()
        {
            Assert.AreEqual(new Vector2(-0.5f, 0f), At(PortFace.West, PortFace.East, 0f));
            Assert.AreEqual(Vector2.zero, At(PortFace.West, PortFace.East, 0.5f));
            Assert.AreEqual(new Vector2(0.5f, 0f), At(PortFace.West, PortFace.East, 1f));
        }

        /// <summary>
        /// **직선에서는 두 구간 보간이 선형과 같다.** 세 점이 한 줄에 놓이기 때문이다 —
        /// 그래서 코너를 위한 식 하나로 직선까지 덮을 수 있고 분기가 없다.
        /// </summary>
        [Test]
        public void Straight_IsIndistinguishableFromLinear()
        {
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                Vector2 got = At(PortFace.West, PortFace.East, t);
                Assert.AreEqual(Mathf.Lerp(-0.5f, 0.5f, t), got.x, D, $"t={t:F2}");
                Assert.AreEqual(0f, got.y, D, $"t={t:F2} — 직선은 옆으로 안 샌다");
            }
        }

        [Test]
        public void Straight_SouthToNorth()
        {
            Assert.AreEqual(new Vector2(0f, -0.5f), At(PortFace.South, PortFace.North, 0f));
            Assert.AreEqual(new Vector2(0f, 0.5f), At(PortFace.South, PortFace.North, 1f));
        }

        // ---- 코너 ----

        /// <summary>
        /// 남에서 받아 서로 내는 코너. 아래 가장자리에서 들어와 중심에서 꺾여 왼쪽으로 나간다.
        /// </summary>
        [Test]
        public void Corner_TurnsAtTheCentre()
        {
            Assert.AreEqual(new Vector2(0f, -0.5f), At(PortFace.South, PortFace.West, 0f));
            Assert.AreEqual(Vector2.zero, At(PortFace.South, PortFace.West, 0.5f));
            Assert.AreEqual(new Vector2(-0.5f, 0f), At(PortFace.South, PortFace.West, 1f));
        }

        /// <summary>
        /// **앞 절반은 세로로만, 뒤 절반은 가로로만 움직인다.** 두 축이 동시에 변하면
        /// 대각선으로 질러가는 것이고, 그것이 이 클래스가 있는 이유다.
        /// </summary>
        [Test]
        public void Corner_DoesNotCutAcross()
        {
            for (float t = 0f; t < 0.5f; t += 0.05f)
                Assert.AreEqual(0f, At(PortFace.South, PortFace.West, t).x, D,
                    $"t={t:F2} — 꺾이기 전에는 가로로 안 움직인다");

            for (float t = 0.55f; t <= 1f; t += 0.05f)
                Assert.AreEqual(0f, At(PortFace.South, PortFace.West, t).y, D,
                    $"t={t:F2} — 꺾인 뒤에는 세로로 안 움직인다");
        }

        /// <summary>경로 길이가 한 칸이다 — 반 칸 들어와 반 칸 나간다.</summary>
        [Test]
        public void Corner_PathLength_IsOneCell()
        {
            float len = 0f;
            Vector2 prev = At(PortFace.South, PortFace.West, 0f);
            for (int i = 1; i <= 200; i++)
            {
                Vector2 cur = At(PortFace.South, PortFace.West, i / 200f);
                len += Vector2.Distance(prev, cur);
                prev = cur;
            }
            Assert.AreEqual(1f, len, 0.01f, "반 칸 + 반 칸");
        }

        // ---- 경계 ----

        /// <summary>
        /// 0~1 밖은 잘라 쓴다. 한 틱에 여러 칸을 건너뛰면 progress가 1을 넘을 수 있는데,
        /// 그때 그림이 칸 밖으로 튀어나가면 안 된다.
        /// </summary>
        [Test]
        public void OutOfRange_IsClamped_SoNothingLeavesTheCell()
        {
            Assert.AreEqual(new Vector2(-0.5f, 0f), At(PortFace.West, PortFace.East, -3f));
            Assert.AreEqual(new Vector2(0.5f, 0f), At(PortFace.West, PortFace.East, 7f));
        }

        /// <summary>어느 면 조합이든 칸 밖으로 안 나간다.</summary>
        [Test]
        public void EveryFacePair_StaysInsideTheCell()
        {
            foreach (PortFace i in System.Enum.GetValues(typeof(PortFace)))
            foreach (PortFace o in System.Enum.GetValues(typeof(PortFace)))
            for (float t = 0f; t <= 1f; t += 0.1f)
            {
                Vector2 p = At(i, o, t);
                Assert.LessOrEqual(Mathf.Abs(p.x), 0.5f + D, $"{i}→{o} t={t:F1}");
                Assert.LessOrEqual(Mathf.Abs(p.y), 0.5f + D, $"{i}→{o} t={t:F1}");
            }
        }
    }
}
