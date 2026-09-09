using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 모듈 기호가 노드 타일 안에 앉는 자리 (`260909_W01` 5장).
    ///
    /// W01이 준 셈을 그대로 붙든다 — 포트 마커가 네 변 24를 쓰므로 **안쪽 144**만 비어 있고
    /// **64 둘은 128**이라 들어간다. 이 부등식이 깨지면 기호가 포트 막대를 밟는다.
    /// </summary>
    public sealed class ModuleSymbolLayoutTests
    {
        private const float D = 0.0001f;

        /// <summary>W01 5장의 「64 둘은 128이라 들어간다」를 값으로 붙든다.</summary>
        [Test]
        public void TwoSymbols_FitInsideThePortBars()
        {
            Assert.AreEqual(144, ModuleSymbolLayout.InnerPixels, "192 − 24 × 2");
            Assert.AreEqual(128, ModuleSymbolLayout.RowPixels, "64 × 2");
            Assert.LessOrEqual(ModuleSymbolLayout.RowPixels, ModuleSymbolLayout.InnerPixels,
                "둘이 안쪽에 들어간다 — 넘치면 포트 막대를 밟는다");
        }

        /// <summary>기호 한 장은 칸의 1/3이다 — 64 ÷ 192.</summary>
        [Test]
        public void SymbolSize_IsOneThirdOfACell()
        {
            Assert.AreEqual(1f / 3f, ModuleSymbolLayout.SymbolSize, D);
        }

        /// <summary>
        /// **왼쪽부터 채운다**(W01 5장). 0번이 왼쪽이고 둘은 정확히 기호 하나만큼 떨어진다.
        /// </summary>
        [Test]
        public void Slots_FillFromTheLeft_AndDoNotOverlap()
        {
            Vector2 left = ModuleSymbolLayout.SlotOffset(0);
            Vector2 right = ModuleSymbolLayout.SlotOffset(1);

            Assert.Less(left.x, right.x, "0번이 왼쪽이다");
            Assert.AreEqual(ModuleSymbolLayout.SymbolSize, right.x - left.x, D,
                "간격이 기호 폭과 같다 — 겹치지도 벌어지지도 않는다");
            Assert.AreEqual(left.y, right.y, D, "둘이 같은 높이에 나란히 선다");
        }

        /// <summary>둘이 이루는 블록은 칸 가운데를 기준으로 좌우 대칭이다.</summary>
        [Test]
        public void Row_IsCenteredHorizontally()
        {
            Vector2 left = ModuleSymbolLayout.SlotOffset(0);
            Vector2 right = ModuleSymbolLayout.SlotOffset(1);

            Assert.AreEqual(0f, left.x + right.x, D, "가운데를 사이에 두고 대칭");
        }

        /// <summary>
        /// **안쪽 아래**에 앉는다(W01 5장). 아래로는 포트 막대를 안 밟고,
        /// 위로는 칸 한가운데를 안 넘는다 — 넘으면 「아래」가 아니다.
        /// </summary>
        [Test]
        public void Symbols_SitAtTheInnerBottom_WithoutTouchingThePortBar()
        {
            Vector2 slot = ModuleSymbolLayout.SlotOffset(0);
            float half = ModuleSymbolLayout.SymbolSize * 0.5f;

            float innerBottom = -ModuleSymbolLayout.InnerPixels * 0.5f / ArtSpec.PixelsPerUnit;
            Assert.GreaterOrEqual(slot.y - half, innerBottom - D, "포트 막대를 안 밟는다");
            Assert.Less(slot.y, 0f, "칸 아래쪽이다");

            float cellBottom = -0.5f;
            Assert.Greater(slot.y - half, cellBottom, "칸 밖으로 안 나간다");
        }

        /// <summary>**빈 칸은 그리지 않는다** — 자리표시를 남기면 모듈이 둘로 읽힌다.</summary>
        [Test]
        public void EmptySlot_IsNotDrawn()
        {
            Assert.IsFalse(ModuleSymbolLayout.SlotIsVisible(null));
            Assert.IsTrue(ModuleSymbolLayout.SlotIsVisible(
                ScriptableObject.CreateInstance<ModuleDefinition>()));
        }

        /// <summary>
        /// **강조색을 쓰지 않는다**(W01 5장) — 모듈은 계열 셋 어느 쪽에도 안 속한다.
        /// 코드가 흰색을 주므로 색은 전부 아트가 갖는다.
        /// </summary>
        [Test]
        public void Tint_IsWhite_SoTheArtKeepsItsOwnColor()
        {
            Assert.AreEqual(Color.white, ModuleSymbolLayout.Tint);
        }

        // ---- 배선 ----

        /// <summary>기호 둘이 실제로 자산에 걸렸는가. 규격 64도 함께 잰다.</summary>
        [Test]
        public void BothSymbols_AreWiredIntoTheArtSet_At64()
        {
            var art = AssetDatabase.LoadAssetAtPath<BoardArtSet>(
                "Assets/_Project/ScriptableObjects/BoardArtSet.asset");
            Assert.NotNull(art, "BoardArtSet 자산");

            AssertSymbol(art.ModuleSprite(ModuleKind.Output), "mod_m");
            AssertSymbol(art.ModuleSprite(ModuleKind.Rate), "mod_r");

            Assert.AreNotSame(art.moduleOutput, art.moduleRate, "둘이 다른 그림이다");
        }

        private static void AssertSymbol(Sprite sprite, string name)
        {
            Assert.NotNull(sprite, $"{name} 기호가 걸렸다");
            Assert.AreEqual(ModuleSymbolLayout.SymbolCanvas, (int)sprite.rect.width, $"{name} 가로 64");
            Assert.AreEqual(ModuleSymbolLayout.SymbolCanvas, (int)sprite.rect.height, $"{name} 세로 64");
        }
    }
}
