using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 보드 스크롤(UI 문서 9-3). 실루엣 12×13 · 한 칸 192px · 기준 화면 1440×2560 기준으로
    /// 가로 864px · 세로 1,144px의 스크롤 여유가 나온다는 것이 문서 확정값이다.
    /// </summary>
    public sealed class BoardPanTests
    {
        private const float D = 0.001f;

        // 월드 유닛 = 칸(셀 크기 1). 보드 12×13, 가시 7.5 × 7칸.
        private static BoardPan Standard() =>
            new BoardPan(new Vector2(12f, 13f), new Vector2(7.5f, 7f));

        /// <summary>문서 9-3 표를 픽셀로 재현 — 칸 × 192px.</summary>
        [Test]
        public void ScrollRange_MatchesUiDocument()
        {
            BoardPan pan = Standard();

            Assert.AreEqual(4.5f, pan.Range.x, D, "12 − 7.5칸");
            Assert.AreEqual(6f, pan.Range.y, D, "13 − 7칸");

            Assert.AreEqual(864f, pan.Range.x * 192f, 0.5f, "가로 864px");
            Assert.AreEqual(1152f, pan.Range.y * 192f, 0.5f, "세로 약 1,144px (문서는 가시 1,352px 기준)");
        }

        [Test]
        public void Drag_MovesBoard_AndClampsAtEdges()
        {
            BoardPan pan = Standard();

            pan.Drag(new Vector2(1f, 1f));
            Assert.AreEqual(1f, pan.Offset.x, D);

            pan.Drag(new Vector2(100f, 100f)); // 한참 밀어도
            Assert.AreEqual(2.25f, pan.Offset.x, D, "여유의 절반에서 멈춘다");
            Assert.AreEqual(3f, pan.Offset.y, D);

            pan.Drag(new Vector2(-100f, -100f));
            Assert.AreEqual(-2.25f, pan.Offset.x, D, "반대쪽도 대칭");
            Assert.AreEqual(-3f, pan.Offset.y, D);
        }

        /// <summary>보드가 화면보다 작으면 스크롤할 이유가 없다 — 흘러가지 않게 0으로 묶는다.</summary>
        [Test]
        public void SmallerThanView_DoesNotScroll()
        {
            var pan = new BoardPan(new Vector2(5f, 5f), new Vector2(10f, 10f));

            pan.Drag(new Vector2(3f, 3f));

            Assert.AreEqual(Vector2.zero, pan.Offset);
            Assert.AreEqual(Vector2.zero, pan.Range);
        }

        [Test]
        public void Resize_ReclampsExistingOffset()
        {
            BoardPan pan = Standard();
            pan.Drag(new Vector2(100f, 100f));
            Assert.AreEqual(2.25f, pan.Offset.x, D);

            // 가시 범위가 넓어지면 여유가 줄고, 기존 오프셋도 따라 줄어야 한다.
            pan.Resize(new Vector2(12f, 13f), new Vector2(11f, 12f));
            Assert.AreEqual(0.5f, pan.Offset.x, D, "여유 1의 절반");
        }

        // ---- 미니맵 표시(UI 문서 2장 「현재 뷰포트 위치 표시」) ----

        [Test]
        public void ViewportCenter_IsHalfWhenCentered()
        {
            Assert.AreEqual(new Vector2(0.5f, 0.5f), Standard().ViewportCenter01);
        }

        [Test]
        public void ViewportCenter_MovesOppositeToOffset()
        {
            BoardPan pan = Standard();

            pan.Drag(new Vector2(100f, 0f)); // 보드를 오른쪽으로 밀면 = 왼쪽을 보는 것
            Assert.AreEqual(0f, pan.ViewportCenter01.x, D);

            pan.Drag(new Vector2(-100f, 0f));
            Assert.AreEqual(1f, pan.ViewportCenter01.x, D);
        }

        /// <summary>스크롤 여유가 없는 축은 0이 아니라 가운데다 — 볼 것이 없는데 끝에 붙어 보이면 안 된다.</summary>
        [Test]
        public void ViewportCenter_IsCentered_WhenNoScrollRoom()
        {
            var pan = new BoardPan(new Vector2(5f, 5f), new Vector2(10f, 10f));
            Assert.AreEqual(new Vector2(0.5f, 0.5f), pan.ViewportCenter01);
        }

        [Test]
        public void Reset_ReturnsToCenter()
        {
            BoardPan pan = Standard();
            pan.Drag(new Vector2(2f, 2f));
            pan.Reset();
            Assert.AreEqual(Vector2.zero, pan.Offset);
        }

        // ---- 모드 ----

        /// <summary>
        /// 기본은 이동 모드다(UI 문서 9-2). 처음 보는 사람이 화면을 옮기려다 벨트를 까는 일이
        /// 없어야 한다 — 9-1의 제스처 충돌을 모드로 없앤 것이 이 시스템의 목적이다.
        /// </summary>
        [Test]
        public void DefaultMode_IsPan()
        {
            Assert.AreEqual(BoardMode.Pan, default(BoardMode));
        }
    }
}
