using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 병목 힌트(260831_V02 §3 확정 · 주간 일정표 B컷 급소).
    ///
    /// 원칙 둘: **행동을 쓴다**(왜 막혔는지가 아니라 무엇을 하면 되는지) ·
    /// **정답은 말하지 않는다 — 방향만 준다**(지침 §3 물류 무개입).
    /// </summary>
    public sealed class BottleneckHintTests
    {
        private static NodeDiagnostic Diag(ConstraintCause cause, int x = 0) => new NodeDiagnostic
        {
            cell = new Vector2Int(x, 0),
            type = NodeType.MunitionsBasic,
            targetRate = 1f,
            actualRate = 0f,
            cause = cause,
        };

        // ---- 문구 ----

        /// <summary>확정 문구 4종. 설계 트랙 원문이라 여기서 손보지 않는다.</summary>
        [Test]
        public void ConfirmedTexts()
        {
            Assert.AreEqual("나가는 곳이 없다. 벨트를 잇거나 쓰는 노드를 늘려라",
                BottleneckHint.TextOf(ConstraintCause.Blocked));
            Assert.AreEqual("들어오는 것이 없다. 앞 단계 노드를 확인해라",
                BottleneckHint.TextOf(ConstraintCause.NoInput));
            Assert.AreEqual("전력이 모자라 느려졌다. 발전소를 늘리거나 노드를 줄여라",
                BottleneckHint.TextOf(ConstraintCause.Power));
            // ⚠️ 모듈이 영상 이후로 연기되면서 문구가 교체됐다(260831_V07) —
            // 없는 물건을 가리키면 안 된다. 모듈이 들어오면 원래 문구로 되돌린다.
            Assert.AreEqual("열이 올라 느려졌다. 열이 몰린 곳의 노드를 덜어내라",
                BottleneckHint.TextOf(ConstraintCause.Heat));
        }

        /// <summary>막힌 곳이 없으면 아무 말도 하지 않는다 — 늘 떠 있는 줄은 읽히지 않는다.</summary>
        [Test]
        public void NoCause_SaysNothing()
        {
            Assert.AreEqual("", BottleneckHint.TextOf(ConstraintCause.None));
            Assert.AreEqual("", BottleneckHint.For(ConstraintCause.None, new List<NodeDiagnostic>()));
            Assert.AreEqual("", BottleneckHint.For(ConstraintCause.None, null));
        }

        /// <summary>
        /// **네 문구 모두 행동으로 끝난다.** 「전력이 부족합니다」로 끝나면 원인 표시이지 힌트가 아니다.
        /// 그리고 어느 문구도 **정확한 개수를 말하지 않는다** — 방향만 준다(물류 무개입).
        /// </summary>
        [Test]
        public void EveryHint_TellsWhatToDo_WithoutGivingTheAnswer()
        {
            foreach (ConstraintCause c in new[]
                     {
                         ConstraintCause.Blocked, ConstraintCause.NoInput,
                         ConstraintCause.Power, ConstraintCause.Heat,
                     })
            {
                string t = BottleneckHint.TextOf(c);
                Assert.IsNotEmpty(t, $"{c}");

                bool actionable = t.Contains("라") || t.Contains("해라");
                Assert.IsFalse(t.Contains("모듈"),
                    $"{c}: 아직 없는 물건을 가리키면 안 된다 — \"{t}\"");
                Assert.IsTrue(actionable, $"{c}: 행동으로 끝나야 한다 — \"{t}\"");

                foreach (char digit in "0123456789")
                    Assert.IsFalse(t.Contains(digit.ToString()),
                        $"{c}: 숫자를 말하면 정답을 주는 것이다 — \"{t}\"");
            }
        }

        // ---- 무엇부터 ----

        /// <summary>
        /// **전역이 개별을 이긴다.** 전력·발열은 보드 전체가 함께 느려지는 것이고,
        /// 막힘은 그 노드 하나가 선 것이라 파급이 다르다.
        /// </summary>
        [Test]
        public void GlobalCause_BeatsPerNode()
        {
            var diags = new List<NodeDiagnostic> { Diag(ConstraintCause.Blocked) };

            Assert.AreEqual(ConstraintCause.Power,
                BottleneckHint.MostUrgent(ConstraintCause.Power, diags));
            Assert.AreEqual(ConstraintCause.Heat,
                BottleneckHint.MostUrgent(ConstraintCause.Heat, diags));
        }

        /// <summary>전역이 멀쩡하면 개별을 본다.</summary>
        [Test]
        public void WithoutGlobalCause_FallsBackToPerNode()
        {
            var diags = new List<NodeDiagnostic> { Diag(ConstraintCause.NoInput) };

            Assert.AreEqual(ConstraintCause.NoInput,
                BottleneckHint.MostUrgent(ConstraintCause.None, diags));
        }

        /// <summary>
        /// 개별끼리는 **막힘이 먼저다.** 앞은 만들고 있는데 갈 곳이 없는 쪽이,
        /// 뒤가 안 와서 노는 쪽보다 손해가 크다 — 만든 것이 버려진다.
        /// </summary>
        [Test]
        public void BlockedBeatsNoInput()
        {
            var diags = new List<NodeDiagnostic>
            {
                Diag(ConstraintCause.NoInput, 0),
                Diag(ConstraintCause.Blocked, 1),
            };

            Assert.AreEqual(ConstraintCause.Blocked,
                BottleneckHint.MostUrgent(ConstraintCause.None, diags));
        }

        /// <summary>정상인 노드만 있으면 힌트가 없다.</summary>
        [Test]
        public void HealthyBoard_HasNoHint()
        {
            var diags = new List<NodeDiagnostic> { Diag(ConstraintCause.None) };

            Assert.AreEqual(ConstraintCause.None,
                BottleneckHint.MostUrgent(ConstraintCause.None, diags));
            Assert.AreEqual("", BottleneckHint.For(ConstraintCause.None, diags));
        }

        /// <summary>
        /// **한 번에 하나만** 나온다. 넷을 늘어놓으면 「무엇부터」가 사라져
        /// 힌트가 아니라 목록이 된다.
        /// </summary>
        [Test]
        public void OnlyOneHint_EvenWhenEverythingIsBroken()
        {
            var diags = new List<NodeDiagnostic>
            {
                Diag(ConstraintCause.Blocked, 0),
                Diag(ConstraintCause.NoInput, 1),
                Diag(ConstraintCause.Blocked, 2),
            };

            string hint = BottleneckHint.For(ConstraintCause.Heat, diags);

            Assert.AreEqual(BottleneckHint.TextOf(ConstraintCause.Heat), hint);
            Assert.IsFalse(hint.Contains("\n"), "여러 줄이 아니다");
        }

        /// <summary>
        /// 힌트와 **전역 원인 배지가 같은 것을 가리킨다.** 배지는 Power → Heat 순서를 쓰므로
        /// 힌트도 그대로 잇는다 — 두 곳이 다른 말을 하면 어느 쪽을 믿을지가 문제가 된다.
        /// </summary>
        [Test]
        public void HintAgreesWithTheGlobalBadge()
        {
            var diags = new List<NodeDiagnostic>();

            Assert.AreEqual(ConstraintCause.Power,
                BottleneckHint.MostUrgent(ConstraintCause.Power, diags),
                "배지가 전력이면 힌트도 전력");
        }
    }
}
