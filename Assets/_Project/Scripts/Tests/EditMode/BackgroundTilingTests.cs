using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 전투 바닥 격자 — **시야가 격자 밖으로 안 나간다** (2026-09-16 신설 · 육안 10차 ②).
    ///
    /// **무엇을 지키는가.** 사용자 보고는 「아래로 이동할 때 전투 화면 상단 배경이 짧게
    /// 사라진다」였다. 원인은 카메라가 아니라 **격자 여유가 한쪽으로 쏠린 것**이었다 —
    /// 원점을 내림으로 스냅하면 위쪽 여유가 0 이 되고, 카메라가 로봇을 lerp 로 따라가며
    /// 뒤처진 그 만큼 윗변 밖이 빈다.
    ///
    /// 📌 **눈으로만 잡히던 결함을 시험이 잡게 하는 것이 이 파일의 목적이다.**
    /// </summary>
    public sealed class BackgroundTilingTests
    {
        /// <summary>
        /// 지금 판의 실제 값 — 한 장 256px ÷ PPU 192 = 1.333 유닛 · 전투 반높이 8.
        /// 시험이 이 둘을 붙들고 있을 이유는 없지만, **한 번은 실제 수로 재 봐야 한다.**
        /// </summary>
        private const float Tile = 256f / 192f;
        private const float HalfH = 8f;

        /// <summary>
        /// 카메라가 로봇에 뒤처지는 거리 — 로봇 4.5 유닛/초 ÷ 따라붙는 계수 6/초 = 0.75.
        /// ⚠️ **시험 안에서만 쓰는 수다.** 본체는 이 수를 안 들고 「여유 두 장」만 든다.
        /// </summary>
        private const float CameraLag = 4.5f / 6f;

        [Test]
        public void 시야와_스냅_오차와_카메라_뒤처짐을_모두_덮는다()
        {
            int rows = BackgroundTiling.Count(HalfH, Tile);
            float reach = BackgroundTiling.Reach(rows, Tile);

            // 격자 원점은 기준점에서 최대 ½장 어긋난다(반올림 스냅).
            float need = HalfH + Tile * 0.5f + CameraLag;
            Assert.Greater(reach, need,
                $"장수 {rows} 로는 시야 {HalfH} + 스냅 {Tile * 0.5f:F2} + 뒤처짐 {CameraLag:F2} 를 못 덮는다");
        }

        [Test]
        public void 구_식은_위쪽_여유가_0_이었다()
        {
            // 🗑️ 폐기된 식을 그대로 재현한다 — **무엇을 고쳤는지가 시험에 남아야 한다.**
            int oldRows = Mathf.CeilToInt(HalfH * 2f / Tile) + 2;
            float oldReach = oldRows * Tile * 0.5f;

            // 구 스냅은 내림이라 원점이 기준점보다 **최대 한 장 아래**에 앉았다.
            float worstTopReach = oldReach - Tile;
            Assert.LessOrEqual(worstTopReach, HalfH + 0.0001f,
                "구 식이 위쪽 여유를 갖고 있었다면 이 결함의 설명이 틀린 것이다");
        }

        [Test]
        public void 반올림_스냅은_기준점에서_반_장_안에_있다()
        {
            var tile = new Vector2(Tile, Tile);
            foreach (float y in new[] { 0f, 0.1f, 0.66f, 0.67f, 1.2f, -0.3f, -3.7f, 12.34f })
            {
                Vector2 o = BackgroundTiling.SnapOrigin(new Vector2(y, y), tile);
                Assert.LessOrEqual(Mathf.Abs(o.y - y), Tile * 0.5f + 0.0001f,
                    $"기준 {y} 에서 원점이 반 장보다 멀리 갔다");
            }
        }

        [Test]
        public void 원점은_언제나_격자_눈금_위다()
        {
            // 눈금을 벗어나면 격자가 자기 자신과 안 이어져 **이음매가 보인다.**
            var tile = new Vector2(Tile, Tile);
            foreach (float v in new[] { 0.4f, -5.9f, 21.1f })
            {
                Vector2 o = BackgroundTiling.SnapOrigin(new Vector2(v, v), tile);
                Assert.AreEqual(0f, Mathf.Repeat(o.x, Tile), 0.0005f);
                Assert.AreEqual(0f, Mathf.Repeat(o.y, Tile), 0.0005f);
            }
        }

        [Test]
        public void 한_장_크기가_0_이면_안_깐다()
        {
            // 그림이 없거나 크기를 못 재는 자리 — 0 으로 나누면 장수가 무한이 된다.
            Assert.AreEqual(0, BackgroundTiling.Count(HalfH, 0f));
            var p = new Vector2(3f, 4f);
            Assert.AreEqual(p, BackgroundTiling.SnapOrigin(p, Vector2.zero));
        }
    }
}
