using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **끊을 자리는 뜻이 있는 자리다** (2026-09-19 사용자 육안 ⑤).
    ///
    /// 화면에서 온 보고는 셋이었다 — 「기초 가공 / 소」 · 「태그 — 교 / 대」 ·
    /// 「합체 게이 / 지 8%」. 셋 다 **한 낱말 한가운데**가 갈렸다.
    /// 여기서 지키는 것은 **어디서 끊는가** 하나이고, 「몇 줄에 드는가」는 그리는 쪽이 잰다.
    /// </summary>
    public sealed class LabelWrapRuleTests
    {
        [Test]
        public void 띄어쓰기에서_끊는다()
        {
            Assert.AreEqual("기초\n가공소", LabelWrapRule.AtSpace("기초 가공소"));
            Assert.AreEqual("복합\n가공소", LabelWrapRule.AtSpace("복합 가공소"));
        }

        [Test]
        public void 끊을_자리가_없으면_그대로다()
        {
            // ⚠️ 억지로 끊으면 그것이 바로 **아무 데서나 끊는 것**이다 — 고치려던 병이다.
            Assert.AreEqual("변환기", LabelWrapRule.AtSpace("변환기"));
            Assert.AreEqual("", LabelWrapRule.AtSpace(""));
            Assert.IsNull(LabelWrapRule.AtSpace(null));
        }

        [Test]
        public void 가운데에_가장_가까운_띄어쓰기를_고른다()
        {
            // 한쪽만 길면 그 줄이 다시 넘쳐 **결국 세 줄**이 된다.
            Assert.AreEqual("가 나\n다 라", LabelWrapRule.AtSpace("가 나 다 라"));
        }

        [Test]
        public void 이미_줄이_나뉘어_있으면_손대지_않는다()
        {
            // 부르는 쪽이 이미 정한 자리다 — 겹쳐 끊으면 세 줄이 된다.
            Assert.AreEqual("태그\n교대", LabelWrapRule.AtSpace("태그\n교대"));
        }

        [Test]
        public void 정한_토막에서_끊는다()
        {
            // 📌 문구 상수는 **한 곳**에 그대로 두고 줄만 나눈다(지침 §7).
            Assert.AreEqual("탭 = 교대\n길게 = 자동 켬/끔",
                LabelWrapRule.At("탭 = 교대 · 길게 = 자동 켬/끔", "·"));
        }

        [Test]
        public void 토막이_없으면_띄어쓰기로_떨어진다()
        {
            Assert.AreEqual("기초\n가공소", LabelWrapRule.At("기초 가공소", "·"));
        }

        [Test]
        public void 토막_앞뒤의_여백은_먹는다()
        {
            // 「가 · 나」 처럼 토막 양옆에 여백이 있으면 **줄 끝과 줄 앞에 빈 칸이 남는다** —
            // 가운데 맞춤에서는 그 빈 칸만큼 글자가 한쪽으로 밀린다.
            Assert.AreEqual("가\n나", LabelWrapRule.At("가 · 나", "·"));
        }

        // 🗑️ **폐기 — `자동_교대_안내가_두_줄로_나뉜다`**(2026-09-19 사용자 확정 ④).
        //    `TagAutoMode.Hint` 를 두 줄로 그리던 자리가 화면에서 걷혔다. 그리지 않는
        //    것을 지키는 시험은 **없는 화면을 지킨다** — 통과해도 아무것도 안 막는다.
        //    상수 자체(손짓과 같은 말인가)는 `TagAutoModeTests` 가 계속 든다.
    }
}
