using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **「처음부터」 뒤에는 로봇 A 로 선다** (2026-10-13 사용자 결함 · 빌드 cf82fb6).
    ///
    /// ⚠️⚠️ **무엇이 B 를 세웠나.** 초기화는 새 시뮬을 짓고 새 시뮬의 활성은 A 다
    /// (<see cref="TagBattle"/> 생성자). 그런데 **마운트 둘이 다 빈 채로** 서므로
    /// <c>TagSystem.EvaluateAuto</c> 의 `activeDepleted` 가 **첫 틱에 참**이 되고,
    /// 자동 교대가 켜져 있으면 곧바로 B 로 넘어간다. 편집 탭은 활성 로봇을 따라가므로
    /// (<see cref="BoardTabFollow"/>) 조립 화면도 B 판을 연다.
    ///
    /// 📌 **탭을 따로 안 잰다.** 탭은 활성 로봇의 그림자다 — 활성이 A 면 탭도 A 다.
    /// 둘을 각자 재면 한쪽만 고쳐지는 날이 온다(지침 §7).
    /// </summary>
    public sealed class ResetStartsOnRobotATests
    {
        private bool _saved;

        [SetUp]
        public void Save() => _saved = TagAutoMode.Enabled;

        [TearDown]
        public void Restore() => TagAutoMode.Enabled = _saved;

        /// <summary>새 판의 활성은 A 다 — 초기화가 짓는 것이 이 판이다.</summary>
        [Test]
        public void 새_판의_활성은_A_다()
        {
            var battle = new TagBattle(new MountLoad(MountLoad.SlotsRobotA), new MountLoad(MountLoad.SlotsRobotB));
            Assert.AreEqual(0, battle.ActiveIndex, "새 판이 B 로 섰다");
        }

        /// <summary>
        /// ⚠️ **이것이 결함의 고리다** — 빈 마운트는 「말랐다」로 읽히고,
        /// 자동 교대가 켜져 있으면 그 자리에서 교대가 일어난다.
        /// </summary>
        [Test]
        public void 빈_마운트는_첫_틱에_교대_사유가_된다()
        {
            TagEntry reason = TagSystem.EvaluateAuto(standbyMountFull: false, activeDepleted: true);
            Assert.AreEqual(TagEntry.Depleted, reason,
                "빈 마운트가 교대 사유가 아니면 이 결함의 설명이 틀린 것이다");
        }

        /// <summary>**초기화는 자동 교대를 기본값으로 되돌린다.**</summary>
        [Test]
        public void 초기화는_자동_교대를_되돌린다()
        {
            TagAutoMode.Enabled = !TagAutoMode.DefaultEnabled;
            Assert.AreNotEqual(TagAutoMode.DefaultEnabled, TagAutoMode.Enabled, "안 바뀌었다 — 이 시험이 아무것도 안 잰다");

            TagAutoMode.Reset();

            Assert.AreEqual(TagAutoMode.DefaultEnabled, TagAutoMode.Enabled, "초기화가 기본값으로 안 돌아간다");

            // 🗑️ 구 단언 「기본값은 꺼짐」 폐기(사용자 확정 #158 · 2026-10-16 · 기본 켜짐).
            //    이 시험이 지키던 것 — 「처음부터 뒤 튜토리얼이 B 로 서지 않는다」 — 는 이제 **B 해금 문**이 지킨다:
            //    자동 교대는 `TagAutoMode.Enabled && RobotBGate.IsUnlocked` 일 때만 켜지고, 「처음부터」는 B 를 다시 잠근다.
            string runner = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("_sim.AutoTagEnabled = TagAutoMode.Enabled && RobotBGate.IsUnlocked;", runner);
        }

        /// <summary>
        /// ⚠️⚠️ **버튼이 실제로 그것을 부르는가.** 위 시험 셋은 부품을 재고,
        /// 사람이 누르는 것은 **버튼**이다 — 부르는 줄이 빠지면 셋 다 초록인 채로
        /// 화면은 그대로 B 로 선다(판 여백 결함과 같은 종류다).
        /// </summary>
        [Test]
        public void 처음부터_버튼이_자동_교대를_되돌린다()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Combat/ReviewerShortcuts.cs");

            int button = src.IndexOf("처음부터 (저장 초기화)", System.StringComparison.Ordinal);
            Assert.Greater(button, 0, "「처음부터」 버튼을 못 찾았다 — 문구가 바뀌었나");

            int call = src.IndexOf("TagAutoMode.Reset()", button, System.StringComparison.Ordinal);
            Assert.Greater(call, button,
                "「처음부터」가 자동 교대를 안 되돌린다 — 초기화 뒤 튜토리얼이 B 로 선다");
        }
    }
}
