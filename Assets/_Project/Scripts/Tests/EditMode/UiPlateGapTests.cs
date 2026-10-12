using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **판끼리 겹치지 않는가** (2026-10-13 신설 · 사용자 스크린샷 ① ② · 빌드 2687da8).
    ///
    /// ⚠️⚠️ **왜 시험이 또 필요한가.** <see cref="StageProgressTests"/> 가 이미
    /// 「칩 줄 아래 · 배지 위」를 지키고 있었고 **초록이었다.** 그런데 화면에서는 진행 띠와
    /// 배지 카드가 겹쳐 있었다 — 그 시험이 견준 것은 <c>Rect</c> 이고, 사람이 보는 것은
    /// <see cref="UiPlate"/> 가 <see cref="UiPlate.Pad"/> 만큼 **넓혀서 그린 판**이다.
    /// 양쪽이 각각 6 을 두르므로 틈이 12 를 안 넘으면 판은 겹친다. 틈은 8 이었다.
    ///
    /// 📌 **재는 문이 사람이 지나는 문과 같아야 한다.** 이 시험은 `Padded` 를 지나서 견준다.
    ///
    /// ⚠️ **넓은 창을 넣는다.** 종전 창 넷은 전부 **세로로 긴** 것이라 배율이 0.5 밑으로
    /// 안 내려갔다. 겹침이 가장 심한 곳은 넓고 낮은 창(세로 690 에서 9.8 픽셀)이었고,
    /// 사용자가 촬영 전에 쓴 창이 그것이다 — **한 꼴에서만 맞는 자리는 자리가 아니다.**
    /// </summary>
    public sealed class UiPlateGapTests
    {
        /// <summary>세로로 긴 창 넷 + **넓고 낮은 창 셋**.</summary>
        private static readonly Vector2[] Screens =
        {
            new Vector2(1440f, 2560f), new Vector2(720f, 1280f),
            new Vector2(1080f, 1920f), new Vector2(1536f, 2730f),
            new Vector2(1600f, 690f), new Vector2(1920f, 900f),
            new Vector2(1280f, 720f),
        };

        private static Rect Plate(Rect r, float screenHeight) =>
            UiPlate.Padded(r, UiLayout.Scale(screenHeight));

        /// <summary>
        /// 위아래로 쌓인 넷 — 칩 줄 · 진행 띠 · 배지 카드 · 「i」 손잡이.
        /// **판끼리** 견준다.
        /// </summary>
        [Test]
        public void 쌓인_판들이_안_겹친다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect chip = Plate(UiLayout.ChipBarRect(s.x, s.y), s.y);
                Rect prog = Plate(UiLayout.StageProgressRect(s.x, s.y), s.y);
                Rect badge = Plate(UiLayout.StageBadgeRect(s.x, s.y), s.y);
                Rect dev = Plate(UiLayout.DevToggleRect(s.x, s.y), s.y);

                Assert.LessOrEqual(chip.yMax, prog.y, $"{s} — 칩 줄 판이 진행 띠 판과 겹친다");
                Assert.LessOrEqual(prog.yMax, badge.y, $"{s} — 진행 띠 판이 배지 카드 판과 겹친다");
                Assert.LessOrEqual(badge.yMax, dev.y, $"{s} — 배지 카드 판이 「i」 손잡이 판과 겹친다");
            }
        }

        /// <summary>
        /// **소리 패널이 진행 띠를 안 지난다** — 사용자 스크린샷 ①.
        ///
        /// ⚠️ 띠는 화면 **가로 전체**를 쓰므로 세로만 견주면 된다. 패널이 띠 아랫변보다
        /// 위에서 시작하면 그 자리는 반드시 가려진다.
        /// </summary>
        [Test]
        public void 소리_패널이_진행_띠_아래에서_시작한다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect prog = Plate(UiLayout.StageProgressRect(s.x, s.y), s.y);
                Rect panel = UiLayout.SoundPanelRect(s.x, s.y);
                Rect button = UiLayout.ChipSoundRect(s.x, s.y);

                Assert.LessOrEqual(prog.yMax, panel.y, $"{s} — 소리 패널이 진행 띠와 겹친다");
                Assert.LessOrEqual(button.yMax, panel.y, $"{s} — 소리 패널이 제 버튼을 덮는다");
            }
        }

        /// <summary>
        /// **소리 패널과 배지 카드가 가로로 안 겹친다** — 둘은 같은 높이에서 시작한다.
        /// </summary>
        [Test]
        public void 소리_패널이_배지_카드_옆에_선다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect badge = Plate(UiLayout.StageBadgeRect(s.x, s.y), s.y);
                Rect panel = UiLayout.SoundPanelRect(s.x, s.y);

                Assert.LessOrEqual(badge.xMax, panel.x, $"{s} — 소리 패널이 배지 카드와 겹친다");
            }
        }

        /// <summary>패널이 화면 안에 드는가 — 오른쪽·아래로 안 넘친다.</summary>
        [Test]
        public void 소리_패널이_화면_안에_든다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect panel = UiLayout.SoundPanelRect(s.x, s.y);
                Assert.GreaterOrEqual(panel.x, 0f, $"{s} — 왼쪽으로 나갔다");
                Assert.LessOrEqual(panel.xMax, s.x, $"{s} — 오른쪽으로 나갔다");
                Assert.LessOrEqual(panel.yMax, s.y, $"{s} — 아래로 나갔다");
            }
        }

        /// <summary>
        /// ⚠️ **틈은 여백에서 난다** — <c>Pad</c> 를 키웠는데 틈이 그대로면 또 겹친다.
        /// 값이 두 곳에 살지 않는 것을 시험이 붙든다(지침 §7).
        /// </summary>
        [Test]
        public void 틈이_여백_둘보다_넓다()
        {
            Assert.Greater(UiLayout.PlateGap, UiPlate.Pad * 2f,
                "틈이 판 여백 둘을 안 넘으면 쌓인 판은 반드시 겹친다");
        }

        /// <summary>
        /// ⚠️ **여백이 배율을 탄다** — 날 픽셀이면 작은 창에서 판이 내용보다 커진다.
        /// </summary>
        [Test]
        public void 여백이_배율을_탄다()
        {
            var r = new Rect(100f, 100f, 200f, 200f);
            Rect big = UiPlate.Padded(r, 1f);
            Rect small = UiPlate.Padded(r, 0.25f);

            Assert.Greater(big.width, small.width, "배율이 작아도 여백이 그대로다 — 날 픽셀");
            Assert.AreEqual(UiPlate.Pad * 0.25f, UiPlate.PadPx(0.25f), 0.001f);
        }
    }
}
