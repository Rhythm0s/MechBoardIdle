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
            TutorialSignals.BoardInBuildMode = true;
            Assert.AreEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current);

            TutorialSignals.HighlightBoardButton = true;
            Assert.AreEqual(TutorialGate.Phase.EnterBoard, TutorialGate.Current);
        }

        /// <summary>
        /// ⚠️ **2026-09-11 재육안 결함 ①.** 고스트가 떠 있는데 **이동 모드**면 종전에는
        /// 「놓기」 국면으로 읽어 **모드 버튼을 잠갔다** — 조립 모드로 들어갈 길이 없어
        /// 강제가 아니라 **정지**였다.
        ///
        /// 시험이 못 잡은 이유도 같다 — 신호만 넣고 **모드를 안 넣어** 없는 상태 조합을
        /// 검사했다. 이제 모드가 네 번째 입력이다.
        /// </summary>
        [Test]
        public void 이동_모드면_고스트가_떠_있어도_모드_국면이다()
        {
            TutorialSignals.GhostCell = new Vector2Int(7, 8);
            TutorialSignals.BoardInBuildMode = false;   // 이동 모드

            Assert.AreEqual(TutorialGate.Phase.BuildMode, TutorialGate.Current);
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.ModeToggle),
                "여기서 모드 버튼이 잠기면 조립 모드로 들어갈 길이 없다");

            TutorialSignals.BoardInBuildMode = true;    // 조립 모드로 바꿨다
            Assert.AreEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current);
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.ModeToggle),
                "놓기 국면에서는 이동 모드로 되돌아가지 않는다");
        }

        [Test]
        public void 채우면_붙잡지_않는다()
        {
            TutorialSignals.GhostCell = new Vector2Int(7, 8);
            TutorialSignals.BoardInBuildMode = true;
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
            TutorialSignals.BoardInBuildMode = false;

            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.ModeToggle));
            // ⚠️ 전투로 돌아가는 것도 막는다 — 나가면 아무것도 빛나지 않는 화면이 된다.
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteMunitions));
        }

        [Test]
        public void 놓기_국면은_보드만_켠다()
        {
            TutorialSignals.GhostCell = new Vector2Int(6, 5);
            TutorialSignals.BoardInBuildMode = true;

            // ⚠️ **팔레트를 전부 막는다**(2026-09-11 설계 확정 (가)). 놓을 것이 노드가
            // 아니라 **벨트**이고 직선 벨트는 팔레트에 버튼이 없다 — 드래그가 만든다.
            // 기초 군수를 켜 두면 **엉뚱한 것을 놓으라고 가리키는 셈**이다.
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteMunitions),
                "이제 놓을 것은 벨트다");
            Assert.IsTrue(TutorialGate.AllowsBoardTap, "보드는 열려 있어야 드래그로 깐다");
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteOther));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.BeltElement));
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.Remove),
                "놓으라고 해 놓고 지울 수 있으면 지금 할 일이 둘이 된다");

            // ⚠️ **모드 버튼을 막는다.** 이 국면은 이미 조립 모드라는 뜻이고,
            // 이동 모드로 돌아가면 보드를 눌러도 안 놓여 다시 막힌다.
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.ModeToggle));
        }

        [Test]
        public void 어느_입력_조합에도_할_일이_남아_있다()
        {
            // ⚠️ **구 시험은 국면만 돌았다** — 그래서 「이동 모드 + 고스트」라는 **실제로
            // 일어난 조합**을 한 번도 안 넣었고, 막다른 자리를 놓쳤다(2026-09-11 결함 ①).
            // 이제 **입력 네 개의 모든 조합**을 돈다.
            for (int bits = 0; bits < 16; bits++)
            {
                bool enter = (bits & 1) != 0, urgeMode = (bits & 2) != 0;
                bool ghost = (bits & 4) != 0, build = (bits & 8) != 0;

                TutorialGate.Phase p = TutorialGate.Resolve(enter, urgeMode, ghost, build);

                bool any = false;
                foreach (TutorialGate.Control c in System.Enum.GetValues(typeof(TutorialGate.Control)))
                    if (TutorialGate.Allows(p, c)) any = true;

                Assert.IsTrue(any,
                    $"진입{enter} 모드강조{urgeMode} 고스트{ghost} 조립모드{build} → {p} : 할 수 있는 일이 없다");

                // ⚠️ **놓기 국면은 조립 모드일 때만 나온다.** 이동 모드에서 놓기로 읽으면
                // 모드 버튼이 잠긴 채 보드가 안 먹어 그대로 정지다.
                if (p == TutorialGate.Phase.PlaceNode)
                    Assert.IsTrue(build, "이동 모드인데 놓기 국면으로 읽혔다");
            }
        }

        [Test]
        public void 보기_돕는_것은_안_막는다()
        {
            // 배율과 미니맵은 **보는 것**이지 놓는 것이 아니다. 막으면 고스트가 화면 밖에
            // 있을 때 찾아갈 길이 없어진다 — 놓기 국면에서만 열어 둔다.
            TutorialSignals.GhostCell = new Vector2Int(7, 8);
            TutorialSignals.BoardInBuildMode = true;
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.Zoom));
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Control.MiniMap));
        }
    }
}
