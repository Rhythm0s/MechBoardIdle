using System.IO;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **모듈을 떼는 길이 화면에 붙어 있는가** (2026-10-13 · 09-15 `5683cd8` 로 들어온 것을 지킨다).
    ///
    /// ⚠️⚠️ **부품만 재면 안 된다.** <c>ModuleFrameTests</c> 가 <c>DetachModuleAt</c> 자체는
    /// 이미 재고 있는데, 그것이 초록이어도 **버튼이 그 줄을 안 부르면** 화면에서는 뗄 길이
    /// 없다. 오늘만 같은 모양의 구멍을 셋 보았다(판 여백 · 초기화 · 폐기한 스폰 길).
    ///
    /// 📌 **왜 이 시험이 지금 생겼나.** 09-12 실측이 「코드에 화면 조작 없음 ·
    /// `DetachModuleAt` 미호출」이라고 적었고, 09-15 에 노드 팝오버가 넓어지면서
    /// 「붙은 모듈 — 떼기」 줄이 들어왔다. **실측 쪽이 낡은 채로 남아 있었다** —
    /// 다시 그렇게 되지 않게 코드가 스스로 말하게 한다.
    /// </summary>
    public sealed class ModuleDetachWiredTests
    {
        private const string Board = "Assets/_Project/Scripts/Logistics/BoardController.cs";

        /// <summary>팝오버에 「떼기」 버튼이 있고, 그것이 떼는 줄을 부른다.</summary>
        [Test]
        public void 팝오버의_떼기가_실제로_뗀다()
        {
            string src = File.ReadAllText(Board);

            // ⚠️ **단추 꼴로 찾는다**(2026-10-15 · #139) — 팝오버가 칸을 글자에서 재면서
            //    「떼기」 글자를 **먼저 잰다**(`const string delLabel = "떼기"`). 글자만 찾으면 그 잼을
            //    단추로 잘못 짚는다. 단추는 `"떼기", style)` 꼴이다.
            int button = src.IndexOf("\"떼기\", style)", System.StringComparison.Ordinal);
            Assert.Greater(button, 0, "노드 팝오버에서 「떼기」 버튼을 못 찾았다");

            int call = src.IndexOf("DetachModuleAt", button, System.StringComparison.Ordinal);
            Assert.Greater(call, button,
                "「떼기」 버튼이 DetachModuleAt 을 안 부른다 — 눌러도 아무 일 없는 버튼이다");

            // 버튼과 호출이 **같은 조건식** 안에 있어야 한다. 사이가 멀면 다른 줄에
            // 걸린 호출을 잘못 본 것이다(한 줄 띄움까지만 허용).
            Assert.Less(call - button, 120,
                "「떼기」와 DetachModuleAt 이 너무 떨어져 있다 — 같은 버튼의 일이 맞나");
        }

        /// <summary>
        /// ⚠️ **뗀 뒤에 그림과 하류를 다시 세운다.** 모듈 기호는 노드 그림 위에 얹혀 있고
        /// 배수가 빠지면 하류 산출이 바뀐다 — 둘 중 하나만 하면 화면이 거짓을 말한다.
        /// </summary>
        [Test]
        public void 뗀_뒤에_그림과_하류를_다시_세운다()
        {
            string src = File.ReadAllText(Board);
            int call = src.IndexOf("DetachModuleAt", System.StringComparison.Ordinal);
            Assert.Greater(call, 0, "DetachModuleAt 호출을 못 찾았다");

            string after = src.Substring(call, System.Math.Min(400, src.Length - call));
            StringAssert.Contains("RebuildMarker", after, "뗀 뒤 노드 그림을 안 다시 짓는다 — 기호가 남는다");
            StringAssert.Contains("RefreshConnections", after, "뗀 뒤 하류를 안 다시 센다 — 배수가 남는다");
        }
    }
}
