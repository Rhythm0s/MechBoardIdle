using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 그리기 순서 7층(260826_V01 §B 확정).
    /// 순서가 뒤집히면 에러가 아니라 **그림이 이상해질 뿐**이라 눈으로 잡기 어렵다 — 그래서 테스트가 본다.
    /// </summary>
    public sealed class SortingLayersTests
    {
        /// <summary>배경 → 타일 → 하단 이펙트 → 액터 → 상단 이펙트 → HUD → 컷인.</summary>
        [Test]
        public void SevenLayers_AreInConfirmedOrder()
        {
            int[] order =
            {
                SortingLayers.Background,
                SortingLayers.Tile,
                SortingLayers.EffectUnder,
                SortingLayers.Actor,
                SortingLayers.EffectOver,
                SortingLayers.Hud,
                SortingLayers.Cutin,
            };

            for (int i = 1; i < order.Length; i++)
                Assert.Less(order[i - 1], order[i], $"{i}번째 층이 앞 층보다 아래에 있다");
        }

        /// <summary>
        /// **하단 이펙트가 액터보다 아래**여야 한다. 이펙트를 한 층으로 두면 바닥 그림자가
        /// 로봇 위에 올라가 「크기와 그림자로 높이를 위조한다」는 원칙이 뒤집힌다 —
        /// 이 층이 신설된 이유가 그것이다.
        /// </summary>
        [Test]
        public void GroundShadowLayer_IsBelowActors()
        {
            Assert.Less(SortingLayers.EffectUnder, SortingLayers.Actor);
            Assert.Greater(SortingLayers.EffectOver, SortingLayers.Actor, "상단 이펙트는 반대로 위다");
        }

        /// <summary>
        /// 층 간격이 있어야 같은 층 안에서 미세 조정(HP 배경 위에 채움 등)을 할 수 있다.
        /// 간격이 1이면 조정 한 번에 다음 층을 침범한다.
        /// </summary>
        [Test]
        public void LayerGap_LeavesRoomForWithinLayerOrdering()
        {
            Assert.AreEqual(SortingLayers.Step, SortingLayers.Actor - SortingLayers.EffectUnder);
            Assert.GreaterOrEqual(SortingLayers.Step, 10, "층 안에서 쓸 여유가 있어야 한다");
        }

        [Test]
        public void Actor_IsZero_SoRelativeOffsetsReadNaturally()
        {
            Assert.AreEqual(0, SortingLayers.Actor, "액터를 0으로 두면 위아래가 부호로 읽힌다");
        }
    }
}
