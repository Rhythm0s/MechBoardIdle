using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **포트 커넥터가 흐르는 쪽을 가리키는가** (2026-10-13 · 커넥터 배선).
    ///
    /// 자산은 한 쪽을 보는 한 장씩이다 — 입력은 ↑, 출력은 ↓. 네 면은 코드가 돌린다.
    ///
    /// 📌 **입력과 출력이 같은 각을 쓴다.** 「↑ 를 −면으로 보내는 회전」이 ↓ 를 **+면**으로
    /// 보내기 때문이다. 각을 둘로 갈라 적으면 한쪽만 고쳐지는 날이 온다 — 그래서
    /// 여기서 **둘 다** 잰다.
    /// </summary>
    public sealed class PortArrowFacingTests
    {
        private static readonly PortFace[] Faces =
        {
            PortFace.North, PortFace.East, PortFace.South, PortFace.West,
        };

        /// <summary>입력 화살은 **셀 안쪽**을 가리킨다 — 밖에서 안으로 들어온다.</summary>
        [Test]
        public void 입력_화살이_안쪽을_가리킨다()
        {
            foreach (PortFace f in Faces)
            {
                Vector2 inflow = PortArrowFacing.InflowDirection(f);
                Vector2 outward = PortArrowFacing.Offset(f);

                Assert.AreEqual(-outward.x, inflow.x, 0.001f, $"{f} — 입력 화살 x");
                Assert.AreEqual(-outward.y, inflow.y, 0.001f, $"{f} — 입력 화살 y");
            }
        }

        /// <summary>출력 화살은 **셀 바깥**을 가리킨다 — 안에서 밖으로 나간다.</summary>
        [Test]
        public void 출력_화살이_바깥을_가리킨다()
        {
            foreach (PortFace f in Faces)
            {
                Vector2 outflow = PortArrowFacing.OutflowDirection(f);
                Vector2 outward = PortArrowFacing.Offset(f);

                Assert.AreEqual(outward.x, outflow.x, 0.001f, $"{f} — 출력 화살 x");
                Assert.AreEqual(outward.y, outflow.y, 0.001f, $"{f} — 출력 화살 y");
            }
        }

        /// <summary>
        /// ⚠️ **둘이 정확히 반대**여야 한다 — 같은 각을 쓰는데 서로 반대로 가는 것이
        /// 이 규칙의 전부다. 하나라도 어긋나면 벨트를 거꾸로 붙이게 된다.
        /// </summary>
        [Test]
        public void 입력과_출력이_서로_반대다()
        {
            foreach (PortFace f in Faces)
            {
                Vector2 i = PortArrowFacing.InflowDirection(f);
                Vector2 o = PortArrowFacing.OutflowDirection(f);

                Assert.AreEqual(-i.x, o.x, 0.001f, $"{f} — x 가 반대가 아니다");
                Assert.AreEqual(-i.y, o.y, 0.001f, $"{f} — y 가 반대가 아니다");
            }
        }

        /// <summary>면 넷이 **다 다른 각**이다 — 둘이 같으면 한 면이 거짓을 말한다.</summary>
        [Test]
        public void 면_넷이_다른_각을_쓴다()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (PortFace f in Faces)
                Assert.IsTrue(seen.Add(Mathf.RoundToInt(PortArrowFacing.ZDegrees(f))),
                    $"{f} 의 각이 다른 면과 같다");
        }

        /// <summary>⚠️ 면 → 방향 규칙이 보드의 `FaceOffset` 과 같은가(지침 §7).</summary>
        [Test]
        public void 면_방향이_보드와_같다()
        {
            Assert.AreEqual(new Vector2(0f, 1f), PortArrowFacing.Offset(PortFace.North));
            Assert.AreEqual(new Vector2(1f, 0f), PortArrowFacing.Offset(PortFace.East));
            Assert.AreEqual(new Vector2(0f, -1f), PortArrowFacing.Offset(PortFace.South));
            Assert.AreEqual(new Vector2(-1f, 0f), PortArrowFacing.Offset(PortFace.West));
        }
    }
}
