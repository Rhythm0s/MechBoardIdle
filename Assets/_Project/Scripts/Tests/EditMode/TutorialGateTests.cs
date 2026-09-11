using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 튜토리얼 국면별 허용 조작 (2026-09-11 신설 · 튜토리얼 기획서 2장 · 플랜 §71-16 ④).
    ///
    /// **무엇을 지키는가.** 「강제」가 **하나만 켜는 것**인지다. 종전에는 눌러야 할 버튼을
    /// 빛나게만 했고 나머지도 전부 눌려서, 다른 것을 누르면 **국면 밖으로 나가 버렸다.**
    ///
    /// ⚠️ **막다른 자리를 만들지 않는 것**도 함께 본다 — 국면이 허용하는 것이 하나도 없으면
    /// 플레이어가 할 수 있는 일이 사라진다.
    /// </summary>
    public sealed class TutorialGateTests
    {
        [SetUp]
        [TearDown]
        public void Clear() => TutorialSignals.Reset();

        [Test]
        public void 신호가_없으면_전부_눌린다()
        {
            Assert.AreEqual(TutorialGate.Phase.None, TutorialGate.Current);
            Assert.IsFalse(TutorialGate.Locked);
            foreach (TutorialGate.Control c in System.Enum.GetValues(typeof(TutorialGate.Control)))
                Assert.IsTrue(TutorialGate.Allows(c), c.ToString());
            Assert.IsTrue(TutorialGate.AllowsBoardTap);
        }

        [Test]
        public void 국면은_차례가_있다()
        {
            // 앞선 것을 못 했으면 뒤엣것을 물을 이유가 없다 — 셋이 동시에 켜져도 앞이 이긴다.
            TutorialSignals.GhostCell = new Vector2Int(7, 8);
            Assert.AreEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current);

            // ⚠️ 이동 모드면 보드를 눌러도 안 놓인다 — 그 자리에서 막힌 것이 T-7 의 근거다.
            TutorialSignals.HighlightBuildMode = true;
            Assert.AreEqual(TutorialGate.Phase.BuildMode, TutorialGate.Current);

            TutorialSignals.HighlightBoardButton = true;
            Assert.AreEqual(TutorialGate.Phase.EnterBoard, TutorialGate.Current);
        }

        [Test]
        public void 채우면_붙잡지_않는다()
        {
            TutorialSignals.GhostCell = new Vector2Int(7, 8);
            Assert.IsTrue(TutorialGate.Locked);

            TutorialSignals.GhostCellFilled = true;
            Assert.AreEqual(TutorialGate.Phase.None, TutorialGate.Current,
                "채우면 그 국면은 끝난다 — 막도 함께 걷힌다(⑤와 같은 판단)");
        }

        [Test]
        public void 조립_진입_국면은_진입_하나만_켠다()
        {
            TutorialSignals.HighlightBoardButton = true;

            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.EnterBoard));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.ModeToggle));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteMunitions));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.Remove));
            Assert.IsFalse(TutorialGate.AllowsBoardTap, "보드를 눌러도 아무 일이 없어야 한다");
        }

        [Test]
        public void 모드_국면은_모드_버튼_하나만_켠다()
        {
            TutorialSignals.HighlightBuildMode = true;

            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.ModeToggle));
            // ⚠️ 전투로 돌아가는 것도 막는다 — 나가면 아무것도 빛나지 않는 화면이 된다.
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteMunitions));
        }

        [Test]
        public void 놓기_국면은_기초_군수와_보드만_켠다()
        {
            TutorialSignals.GhostCell = new Vector2Int(7, 8);

            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.PaletteMunitions));
            Assert.IsTrue(TutorialGate.AllowsBoardTap);
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteOther));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.BeltElement));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.Remove),
                "놓으라고 해 놓고 지울 수 있으면 지금 할 일이 둘이 된다");

            // ⚠️ **모드 버튼을 막는다.** 이 국면은 이미 조립 모드라는 뜻이고,
            // 이동 모드로 돌아가면 보드를 눌러도 안 놓여 다시 막힌다.
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.ModeToggle));
        }

        [Test]
        public void 어느_국면에도_할_일이_남아_있다()
        {
            // **막다른 자리가 없는가.** 허용이 하나도 없는 국면이 있으면 플레이어가
            // 할 수 있는 일이 사라진다 — 강제가 아니라 정지다.
            foreach (TutorialGate.Phase p in System.Enum.GetValues(typeof(TutorialGate.Phase)))
            {
                bool any = TutorialGate.AllowsBoardTap && p == TutorialGate.Phase.PlaceNode;
                foreach (TutorialGate.Control c in System.Enum.GetValues(typeof(TutorialGate.Control)))
                    if (TutorialGate.Allows(p, c)) any = true;
                Assert.IsTrue(any, $"{p} 국면에 할 수 있는 일이 없다");
            }
        }

        [Test]
        public void 보기_돕는_것은_안_막는다()
        {
            // 배율과 미니맵은 **보는 것**이지 놓는 것이 아니다. 막으면 고스트가 화면 밖에
            // 있을 때 찾아갈 길이 없어진다 — 놓기 국면에서만 열어 둔다.
            TutorialSignals.GhostCell = new Vector2Int(7, 8);
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.Zoom));
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.MiniMap));
        }
    }
}
