using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 연출 규칙(UI 문서「연출 표현 규칙」· 260825_V01 §3).
    /// 판정에 영향이 없는 순수 표현이지만, 규칙 자체는 문서가 정한 것이라 계약으로 고정한다.
    /// </summary>
    public sealed class EffectTimingTests
    {
        private const float D = 0.0001f;

        // ---- 발사 반동 ----

        /// <summary>2픽셀 — PPU에서 파생되므로 규격이 바뀌면 함께 따라온다.</summary>
        [Test]
        public void RecoilDistance_IsTwoPixelsAtCurrentPpu()
        {
            Assert.AreEqual(2f / ArtSpec.PixelsPerUnit, EffectTiming.RecoilDistance, D);
            Assert.AreEqual(0.0104f, EffectTiming.RecoilDistance, 0.0005f, "192 PPU에서 2px ≈ 0.0104 유닛");
        }

        /// <summary>표적 **반대** 방향으로 밀린다. 쏜 쪽으로 밀리면 반동이 아니라 돌진이다.</summary>
        [Test]
        public void Recoil_PushesOppositeToFireDirection()
        {
            Vector2 off = EffectTiming.RecoilOffset(Vector2.right, EffectTiming.RecoilDuration * 0.5f);

            Assert.Less(off.x, 0f, "오른쪽으로 쏘면 왼쪽으로 밀린다");
            Assert.AreEqual(0f, off.y, D);
        }

        /// <summary>0 → 최대 → 0. 밀렸다 돌아오는 것이 한 동작으로 읽혀야 한다.</summary>
        [Test]
        public void Recoil_PeaksAtMiddle_AndReturnsToZero()
        {
            Vector2 start = EffectTiming.RecoilOffset(Vector2.right, 0f);
            Vector2 peak = EffectTiming.RecoilOffset(Vector2.right, EffectTiming.RecoilDuration * 0.5f);
            Vector2 end = EffectTiming.RecoilOffset(Vector2.right, EffectTiming.RecoilDuration);

            Assert.AreEqual(0f, start.magnitude, D, "시작은 제자리");
            Assert.AreEqual(EffectTiming.RecoilDistance, peak.magnitude, D, "중간이 최대");
            Assert.AreEqual(0f, end.magnitude, D, "끝나면 정확히 제자리 — 누적되면 로봇이 흘러간다");
        }

        [Test]
        public void Recoil_IsZero_AfterDurationOrWithNoDirection()
        {
            Assert.AreEqual(Vector2.zero, EffectTiming.RecoilOffset(Vector2.right, 999f));
            Assert.AreEqual(Vector2.zero, EffectTiming.RecoilOffset(Vector2.zero, 0.01f));
        }

        /// <summary>방향 크기와 무관하다 — 먼 표적을 쏜다고 더 밀리지 않는다.</summary>
        [Test]
        public void Recoil_IgnoresDirectionMagnitude()
        {
            float near = EffectTiming.RecoilOffset(Vector2.right * 0.5f, EffectTiming.RecoilDuration * 0.5f).magnitude;
            float far = EffectTiming.RecoilOffset(Vector2.right * 50f, EffectTiming.RecoilDuration * 0.5f).magnitude;

            Assert.AreEqual(near, far, D);
        }

        // ---- 피격 점멸 ----

        /// <summary>
        /// 빨강·하양만 오간다. **세기는 일정** — 로봇에 방어력이 없어 받는 피해가 몬스터 공격력
        /// 그대로이므로, 세기로 정도를 표현하면 없는 정보를 지어내는 것이 된다.
        /// </summary>
        [Test]
        public void HitFlash_AlternatesRedAndWhite_AtConstantIntensity()
        {
            var seen = new System.Collections.Generic.HashSet<Color>();
            for (int i = 0; i < 4; i++)
                seen.Add(EffectTiming.HitFlashColor(Color.blue, EffectTiming.HitFlashDuration * (0.125f + i * 0.25f)));

            Assert.AreEqual(2, seen.Count, "두 색만 나온다");
            Assert.IsTrue(seen.Contains(Color.red));
            Assert.IsTrue(seen.Contains(Color.white));
        }

        [Test]
        public void HitFlash_RestoresBaseColor_WhenDone()
        {
            var baseColor = new Color(0.3f, 0.6f, 1f);

            Assert.AreEqual(baseColor, EffectTiming.HitFlashColor(baseColor, EffectTiming.HitFlashDuration));
            Assert.AreEqual(baseColor, EffectTiming.HitFlashColor(baseColor, 999f));
            Assert.AreEqual(baseColor, EffectTiming.HitFlashColor(baseColor, -1f));
        }

        /// <summary>두 번 이상 교차해야 눈에 걸린다 — 한 번만 깜빡이면 프레임 사이로 사라진다.</summary>
        [Test]
        public void HitFlash_CrossesMoreThanOnce()
        {
            Color a = EffectTiming.HitFlashColor(Color.blue, EffectTiming.HitFlashDuration * 0.1f);
            Color b = EffectTiming.HitFlashColor(Color.blue, EffectTiming.HitFlashDuration * 0.4f);
            Color c = EffectTiming.HitFlashColor(Color.blue, EffectTiming.HitFlashDuration * 0.6f);

            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(b, c);
        }

        // ---- 바닥 그림자 ----

        /// <summary>탑뷰에는 높이가 없어 크기와 그림자로 위조한다 — 납작해야 바닥에 누운 것으로 읽힌다.</summary>
        [Test]
        public void Shadow_IsFlatterThanItIsWide()
        {
            Vector2 s = EffectTiming.ShadowSize(1f);

            Assert.Less(s.y, s.x, "세로가 가로보다 짧아야 눕는다");
            Assert.Less(s.x, 1f, "본체보다 좁다");
        }

        [Test]
        public void Shadow_ScalesWithBody_AndSitsBelowCenter()
        {
            Assert.AreEqual(EffectTiming.ShadowSize(1f) * 2f, EffectTiming.ShadowSize(2f), "크기에 비례");
            Assert.Less(EffectTiming.ShadowFootOffset(1f), 0f, "발밑 = 중심보다 아래");
        }

        /// <summary>보스(2.667칸)와 드론(0.333칸)의 그림자가 같은 규칙에서 나온다.</summary>
        [Test]
        public void Shadow_DerivesFromCanvasSizes()
        {
            Assert.Greater(EffectTiming.ShadowSize(ArtSpec.LargeSize).x,
                EffectTiming.ShadowSize(ArtSpec.DroneSize).x, "보스 그림자가 드론보다 크다");
        }
    }
}
