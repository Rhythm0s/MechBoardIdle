using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **방향 표시는 한 벌을 쓴다 — 입력 파랑 · 출력 주황** (2026-10-13 사용자 확정 #96).
    ///
    /// ⚠️ 쓰는 곳이 여럿이다(면 화살표 · 미연결 벨트 화살표 · 앞으로 올 방향 묻기).
    /// 각자 제 색을 들면 같은 뜻이 화면에서 다른 색으로 나온다(지침 §7).
    /// </summary>
    public sealed class PortDirectionColorTests
    {
        [Test]
        public void 입력과_출력이_다른_색이다()
        {
            Assert.AreNotEqual(PortDirectionColor.Input, PortDirectionColor.Output,
                "입력과 출력이 같은 색이면 가르는 뜻이 없다");
        }

        [Test]
        public void 바깥을_보면_출력색이다()
        {
            Assert.AreEqual(PortDirectionColor.Output, PortDirectionColor.Of(outward: true));
            Assert.AreEqual(PortDirectionColor.Input, PortDirectionColor.Of(outward: false));
        }

        /// <summary>
        /// ⚠️ **말이 색을 정한다** — 사용자가 정한 것은 「파랑 · 주황」이다.
        /// 값이 바뀌어도 **파랑은 파랑답고 주황은 주황다워야** 한다.
        /// </summary>
        [Test]
        public void 파랑은_파랗고_주황은_주황이다()
        {
            Color blue = PortDirectionColor.Input;
            Assert.Greater(blue.b, blue.r, "입력색의 파랑이 빨강보다 약하다 — 파랑이 아니다");
            Assert.Greater(blue.b, blue.g, "입력색의 파랑이 초록보다 약하다 — 파랑이 아니다");

            Color orange = PortDirectionColor.Output;
            Assert.Greater(orange.r, orange.b, "출력색의 빨강이 파랑보다 약하다 — 주황이 아니다");
            Assert.Greater(orange.g, orange.b, "출력색의 초록이 파랑보다 약하다 — 주황이 아니다");
            Assert.Greater(orange.r, orange.g, "빨강이 초록보다 약하다 — 노랑에 가깝다");
        }

        [Test]
        public void 둘_다_불투명하다()
        {
            Assert.AreEqual(1f, PortDirectionColor.Input.a, 0.001f);
            Assert.AreEqual(1f, PortDirectionColor.Output.a, 0.001f);
        }

        /// <summary>
        /// ⚠️⚠️ **구 색 둘을 아무도 안 읽는가.** 폐기는 삭제가 아니라 표기이므로 칸은
        /// 남아 있다 — 남은 칸을 누가 다시 읽기 시작하면 색이 두 벌로 갈린다.
        /// 그 일이 일어나면 여기서 빨개진다.
        /// </summary>
        [Test]
        public void 폐기한_색_둘을_읽는_곳이_없다()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Logistics/BoardController.cs");

            foreach (string name in new[] { "BeltArrowColor", "WiringArrowColor" })
            {
                int declared = src.IndexOf("Color " + name + " =", System.StringComparison.Ordinal);
                Assert.Greater(declared, 0, $"{name} 선언을 못 찾았다 — 이름이 바뀌었나");

                int after = src.IndexOf(name, declared + name.Length, System.StringComparison.Ordinal);
                Assert.AreEqual(-1, after,
                    $"{name} 을 선언 뒤에 다시 읽는다 — 방향 색이 두 벌로 갈린다");
            }
        }
    }
}
