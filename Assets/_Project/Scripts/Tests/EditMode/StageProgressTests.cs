using MBI.Core;
using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **스테이지 진행 띠** (2026-09-30 사용자 확정 ⑦ — 「Stage k/6」 + 맵 이름 + 막대).
    ///
    /// ⚠️ 지켜야 하는 것 둘 — **겹치지 않는다**(칩 줄 아래 · 배지 위)와
    /// **여섯에 안 드는 판은 번호가 0 이다**(튜토리얼 전용 스테이지).
    /// </summary>
    public sealed class StageProgressTests
    {
        // 기준 캔버스와 실제로 쓰는 창 넷에서 다 본다 — 한 창에서만 맞는 자리는 자리가 아니다.
        private static readonly Vector2[] Screens =
        {
            new Vector2(1440f, 2560f), new Vector2(720f, 1280f),
            new Vector2(1080f, 1920f), new Vector2(1536f, 2730f),
        };

        [Test]
        public void 칩_줄_아래_배지_위에_선다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect chip = UiLayout.ChipBarRect(s.x, s.y);
                Rect prog = UiLayout.StageProgressRect(s.x, s.y);
                Rect badge = UiLayout.StageBadgeRect(s.x, s.y);

                Assert.LessOrEqual(chip.yMax, prog.y, $"{s} — 진행 띠가 칩 줄과 겹친다");
                Assert.LessOrEqual(prog.yMax, badge.y, $"{s} — 진행 띠가 배지와 겹친다");
            }
        }

        /// <summary>⚠️ 아래 딸린 것도 함께 밀렸는가 — 「i」 손잡이는 배지에서 자리를 받는다.</summary>
        [Test]
        public void 아래_딸린_것도_함께_밀린다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect badge = UiLayout.StageBadgeRect(s.x, s.y);
                Rect dev = UiLayout.DevToggleRect(s.x, s.y);
                Assert.LessOrEqual(badge.yMax, dev.y, $"{s} — 「i」 손잡이가 배지와 겹친다");
            }
        }

        [Test]
        public void 화면_안에_든다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect prog = UiLayout.StageProgressRect(s.x, s.y);
                Assert.GreaterOrEqual(prog.x, 0f, $"{s} — 왼쪽으로 나갔다");
                Assert.LessOrEqual(prog.xMax, s.x, $"{s} — 오른쪽으로 나갔다");
                Assert.Greater(prog.height, 0f, $"{s} — 높이가 0 이다");
            }
        }

        [Test]
        public void 스테이지_번호를_읽는다()
        {
            Assert.AreEqual(1, StageMapName.NumberOf("S1"));
            Assert.AreEqual(6, StageMapName.NumberOf("S6"));
        }

        /// <summary>
        /// ⚠️ **여섯에 안 드는 것은 0 이다** — 그리는 쪽이 0 을 받으면 띠를 안 그린다.
        /// 「Stage 0/6」 같은 것을 지어내면 안 된다.
        /// </summary>
        [Test]
        public void 여섯에_안_드는_것은_0_이다()
        {
            Assert.AreEqual(0, StageMapName.NumberOf("S0"), "튜토리얼 전용 판이 여섯에 들었다");
            Assert.AreEqual(0, StageMapName.NumberOf("S7"), "여섯을 넘는 번호가 들었다");
            Assert.AreEqual(0, StageMapName.NumberOf(""), "빈 이름이 번호가 됐다");
            Assert.AreEqual(0, StageMapName.NumberOf(null));
            Assert.AreEqual(0, StageMapName.NumberOf("tutorial"));
            Assert.AreEqual(0, StageMapName.NumberOf("S1a"), "숫자가 아닌 꼬리를 읽었다");
        }

        [Test]
        public void 이름_여섯이_다_있고_빈_것이_없다()
        {
            for (int k = 1; k <= StageMapName.TotalStages; k++)
                Assert.IsNotEmpty(StageMapName.Of(k), $"S{k} 의 맵 이름이 비었다");
        }

        /// <summary>범위 밖은 **빈 글자** — 모르는 판에 이름을 지어 붙이지 않는다.</summary>
        [Test]
        public void 범위_밖은_빈_글자다()
        {
            Assert.IsEmpty(StageMapName.Of(0));
            Assert.IsEmpty(StageMapName.Of(StageMapName.TotalStages + 1));
        }

        /// <summary>⚠️ 이름에 번호를 안 넣는다 — 화면이 「Stage k/6」을 따로 붙인다(지침 §7).</summary>
        [Test]
        public void 이름에_번호가_안_들어간다()
        {
            for (int k = 1; k <= StageMapName.TotalStages; k++)
                Assert.IsFalse(StageMapName.Of(k).Contains(k.ToString()),
                    $"S{k} 이름에 숫자가 들었다 — 같은 수가 두 곳에 산다");
        }
    }
}
