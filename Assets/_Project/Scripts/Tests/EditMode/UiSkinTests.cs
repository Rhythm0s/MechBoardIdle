using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// IMGUI 껍데기 (2026-09-11 신설 · 플랜 §68-4 (A) · §68-5 ②).
    ///
    /// **무엇을 지키는가.** 색이 예쁜지가 아니라 **월요일에 갈아끼울 수 있는 모양인지**다 —
    /// 테두리가 9-슬라이스 여백과 같은 두께인지, 각진 채로 늘어나는지, 텍스처 소스가
    /// 한 곳인지. 이 셋이 서면 아트 자산이 와도 호출부를 안 건드린다.
    ///
    /// ⚠️ `Apply()` 는 `GUI.skin` 을 만지므로 `OnGUI` 밖에서 못 부른다 —
    /// 여기서는 **텍스처와 색만** 본다.
    /// </summary>
    public sealed class UiSkinTests
    {

        /// <summary>
        /// 색 비교 — **8비트로 반올림된 것을 감안한다.** `RGBA32` 는 0~255 로 저장하므로
        /// 0.86 을 넣으면 0.859 로 돌아온다. 정확히 같기를 요구하면 값이 맞는데도 빨강이 뜬다.
        /// </summary>
        private static void Same(Color want, Color got, string what)
        {
            const float Q = 1f / 255f + 0.0005f; // 8비트 한 칸
            Assert.AreEqual(want.r, got.r, Q, what + " R");
            Assert.AreEqual(want.g, got.g, Q, what + " G");
            Assert.AreEqual(want.b, got.b, Q, what + " B");
            Assert.AreEqual(want.a, got.a, Q, what + " A");
        }

        [Test]
        public void 텍스처가_지어진다()
        {
            Assert.IsTrue(UiSkin.TexturesReady, "코드 생성 텍스처 — 자산이 없어도 서야 한다");
        }

        [Test]
        public void 테두리는_각지고_고정_두께다()
        {
            Texture2D t = UiSkin.NormalTexture;
            Assert.IsNotNull(t);

            // 각진 것이 톤이다 — 보간하면 늘어날 때 테두리가 번져 둥글어 보인다.
            Assert.AreEqual(FilterMode.Point, t.filterMode, "점 보간");

            // ⚠️ **그림 자산은 화소를 못 읽는다**(2026-09-11 · c10 설치 뒤).
            // 임포트한 텍스처는 `Read/Write` 를 켜야 `GetPixel` 이 되는데, 그것은 **시험만을
            // 위해 메모리를 두 배로 쓰는 일**이다 — 런타임은 GPU 로만 쓴다.
            // 그림일 때는 **약속**(여백이 0보다 크다 · 여백 둘이 한 변보다 작다)만 본다.
            if (UiSkin.UsingArt)
            {
                Assert.Greater(UiSkin.ActiveBorder, 0, "9-슬라이스 여백이 있어야 늘어난다");
                Assert.Less(UiSkin.ActiveBorder * 2, t.width, "여백 둘이 한 변을 넘으면 가운데가 없다");
                return;
            }

            // 네 귀퉁이는 테두리색, 한가운데는 바탕색. 9-슬라이스가 이 배치를 전제한다.
            Same(UiSkin.Border, t.GetPixel(0, 0), "왼아래 귀퉁이");
            Same(UiSkin.Border, t.GetPixel(t.width - 1, t.height - 1), "오른위 귀퉁이");
            Same(UiSkin.Fill, t.GetPixel(t.width / 2, t.height / 2), "한가운데");

            // 테두리 두께가 `GUIStyle.border` 와 같아야 늘어나도 4px 로 남는다.
            Same(UiSkin.Border, t.GetPixel(UiSkin.BorderPx - 1, t.height / 2), "안쪽 끝");
            Same(UiSkin.Fill, t.GetPixel(UiSkin.BorderPx, t.height / 2), "그 한 칸 안");
        }

        /// <summary>
        /// **그림이 오면 그림이 이기고, 없으면 코드 생성본으로 떨어진다**
        /// (2026-09-11 · `260911_W02` 9장 자산 절).
        ///
        /// 「자산이 오면 갈아끼운다」가 말이 되려면 **안 온 상태에서도 화면이 서야** 한다.
        /// 어느 쪽이든 **호출부는 모른다** — 그것이 텍스처 소스를 한 곳에 둔 이유다.
        /// </summary>
        [Test]
        public void 그림이_없어도_껍데기는_선다()
        {
            Assert.IsTrue(UiSkin.TexturesReady, "어느 쪽이든 넷 다 채워진다");
            Assert.IsNotNull(UiSkin.PlateTexture);
            Assert.IsNotNull(UiSkin.DisabledTexture);

            // ⚠️ **눌림이 기본과 같으면 안 된다** — 같으면 눌러도 안 바뀌는 버튼이 되어
            // 상태가 화면에서 사라진다. 그림이 아직 하나뿐이라 나머지는 코드 생성본이 메운다.
            Assert.AreNotSame(UiSkin.NormalTexture, UiSkin.DisabledTexture);
        }

        [Test]
        public void 텍스처가_9슬라이스보다_크다()
        {
            // 테두리 둘 + 늘어날 가운데가 있어야 한다. 같거나 작으면 가운데가 없어
            // 늘렸을 때 테두리끼리 맞물려 **통짜 사각**이 된다.
            Texture2D t = UiSkin.NormalTexture;
            Assert.Greater(t.width, UiSkin.ActiveBorder * 2, "가운데가 남아야 늘어난다");
            Assert.AreEqual(t.width, t.height, "정사각이라 가로세로 어느 쪽으로도 늘어난다");
        }

        [Test]
        public void 판은_UiPlate_와_같은_계열이다()
        {
            // 판 색이 갈리면 같은 화면에 어두운 회색 두 가지가 생긴다.
            // ⚠️ 패널 그림이 오면 색은 그림이 정한다 — 그때는 볼 것이 없어진다.
            if (UiSkin.UsingArt && UiSkin.PlateTexture != null
                && !UiSkin.PlateTexture.isReadable) Assert.Pass("패널이 그림이다 — 색은 그림이 정한다");

            Same(UiPlate.Tint, UiSkin.PlateTexture.GetPixel(6, 6), "판 가운데");
        }

        [Test]
        public void 잠긴_바탕이_따로_있다()
        {
            // IMGUI 에 꺼진 상태 칸이 없어 **호출부가 직접 깐다** — 없으면 튜토리얼 잠금이
            // 색으로 안 말해진다(팔레트가 이것을 쓴다).
            Assert.IsNotNull(UiSkin.DisabledTexture);
            Assert.AreNotEqual(UiSkin.NormalTexture, UiSkin.DisabledTexture);
        }

        [Test]
        public void 강조는_주황이고_테두리는_미색이다()
        {
            // 문서 톤(아이언사가 · 무채색 금속 + 미색 테두리 + 주황 강조)의 최소 확인.
            Assert.Greater(UiSkin.Accent.r, UiSkin.Accent.b, "주황은 붉은 쪽이 파란 쪽보다 세다");
            Assert.Greater(UiSkin.Border.r, 0.7f, "미색은 밝다");
            Assert.Less(UiSkin.Fill.r, 0.2f, "바탕은 검정 계열");
            Assert.Less(UiSkin.Fill.a, 1f, "반투명 — 뒤 전투가 비친다");
        }

        /// <summary>
        /// **상태는 명도로 말한다** (2026-09-11 아트 요청 · 구 「주황 곱」 폐기).
        ///
        /// 그릇 그림이 이미 색을 갖고 있어 주황을 곱하면 **색조가 통째로 돌아간다** —
        /// 금속이 주황 금속이 된다. 명도는 색조를 안 건드린다.
        /// </summary>
        [Test]
        public void 눌림은_어두워지고_손올림은_밝아진다()
        {
            Assert.Less(UiSkin.PressedMul, 1f, "눌리면 어두워진다");
            Assert.Greater(UiSkin.HoverMul, 1f, "손이 올라가면 밝아진다");

            Color p = UiSkin.PressedTint, h = UiSkin.HoverTint;
            Assert.Less(p.r + p.g + p.b, UiSkin.Text.r + UiSkin.Text.g + UiSkin.Text.b);
            Assert.Greater(h.r + h.g + h.b, UiSkin.Text.r + UiSkin.Text.g + UiSkin.Text.b);
        }

        [Test]
        public void 명도_곱은_색조를_안_돌린다()
        {
            // ⚠️ **이것이 주황 곱과 갈리는 자리다.** 주황을 곱하면 파랑 채널이 죽어
            // 색조가 돈다 — 명도는 세 채널을 같은 비로 옮긴다.
            var blue = new Color(0.2f, 0.4f, 0.8f, 1f);
            Color b = UiSkin.Brighten(blue, 0.5f);

            Assert.AreEqual(blue.r / blue.b, b.r / b.b, 0.0001f, "채널 비가 그대로다");
            Assert.AreEqual(blue.a, b.a, 0.0001f, "알파는 안 건드린다");

            // 1 을 넘겨도 안 터진다 — 흰색에서 멈춘다.
            Color hot = UiSkin.Brighten(Color.white, 3f);
            Assert.AreEqual(1f, hot.r, 0.0001f);
        }
    }
}
