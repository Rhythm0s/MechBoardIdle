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
        /// **줄곧 붉되 갈수록 옅어진다** (2026-09-15 사용자 확정 · 육안 ④ 「길게·진하게」).
        ///
        /// ⚠️ **구 시험은 「빨강·하양 교차 · 세기 일정」을 지켰다.** 근거는 「로봇에 방어력이
        /// 없어 세기로 정도를 표현하면 없는 정보를 지어낸다」였고 그 자체로는 옳다.
        /// 그런데 화면에서 **하양 국면이 원래 색과 같아 보여 절반이 헛돌았고**, 0.12 초는
        /// 배선이 다 있는데도 **「이펙트가 없다」로 보였다.**
        ///
        /// 이제 세기는 **정도가 아니라 시간**을 말한다 — 맞은 순간이 가장 진하고 잦아든다.
        /// 여전히 **피해량과 무관**하므로 없는 정보를 지어내지 않는다.
        /// </summary>
        [Test]
        public void HitFlash_StaysRed_AndFadesOverTime()
        {
            Color early = EffectTiming.HitFlashColor(Color.blue, EffectTiming.HitFlashDuration * 0.05f);
            Color late = EffectTiming.HitFlashColor(Color.blue, EffectTiming.HitFlashDuration * 0.90f);

            Assert.Greater(early.r, late.r, "맞은 순간이 가장 붉다");
            Assert.Greater(early.r, Color.blue.r, "바탕색보다 붉어야 맞은 것이 보인다");
            // ⚠️ 바탕이 파랑이므로 **잦아들수록 파랑이 늘어난다**(빨강 쪽에서 바탕 쪽으로).
            Assert.Greater(late.b, early.b, "잦아들수록 바탕색으로 돌아온다");
        }

        /// <summary>
        /// 흔들림은 **그림만** 떤다 — 넉백이 아니다 (2026-09-15 사용자 확정).
        /// 결정론적이어야 한다: 같은 시각이면 같은 값이다(난수 금지 · §5-6).
        /// </summary>
        [Test]
        public void HitShake_IsDeterministic_AndStopsWhenDone()
        {
            float t = EffectTiming.HitFlashDuration * 0.3f;
            Assert.AreEqual(EffectTiming.HitShakeOffset(t).x, EffectTiming.HitShakeOffset(t).x, D,
                "같은 시각이면 같은 값 — 난수를 쓰면 재현이 깨진다");

            Assert.AreEqual(Vector2.zero, EffectTiming.HitShakeOffset(EffectTiming.HitFlashDuration),
                "끝나면 제자리 — 안 되돌리면 몸이 밀린 채로 남는다");
            Assert.AreEqual(Vector2.zero, EffectTiming.HitShakeOffset(-1f));
        }

        /// <summary>피격 표시가 **눈에 걸릴 만큼 길다** — 0.12 초는 「없다」로 보였다.</summary>
        [Test]
        public void HitFlash_IsLongEnoughToSee()
        {
            Assert.Greater(EffectTiming.HitFlashDuration, 0.2f,
                "0.12 초는 배선이 다 있는데도 「이펙트가 없다」로 보였다(09-15 육안)");
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

        // ---- 탄약 소진 아이콘 (2026-09-10 사용자 확정 · 촬영 전 임시) ----

        /// <summary>
        /// **아이콘이 로봇 실루엣의 절반을 안 넘고, 몸통 밖 발 아래에 놓인다.**
        ///
        /// 실측 — `vfx_ammoout` 은 256 캔버스에 실루엣 **212px**, 로봇은 같은 캔버스에 **220px** 이다.
        /// 그대로 두면 아이콘이 로봇만 해져 「무엇이 멈췄는지」보다 「무언가 가려졌다」가 먼저 읽힌다.
        ///
        /// ⚠️ **화면에서 커 보이는지는 사람이 본다.** 여기서 재는 것은 폭과 자리뿐이다.
        /// </summary>
        [Test]
        public void AmmoOutIcon_StaysUnderHalfTheRobot_AndSitsBelowTheBody()
        {
            const float robotSilhouette = 220f;  // px · robot_a.png 실측
            const float iconSilhouette = 212f;   // px · vfx_ammoout.png 실측

            float scale = EffectTiming.AmmoOutScale(0.5f);
            Assert.AreEqual(0.5f, scale, 0.0001f, "가정치가 상한 아래라 그대로 쓰인다");

            Assert.LessOrEqual(iconSilhouette * scale, robotSilhouette * 0.5f,
                "아이콘 실루엣이 로봇 실루엣의 절반을 넘는다");

            // 상한은 넘겨도 잘린다 — SO 값을 잘못 넣어도 화면이 덮이지 않는다.
            Assert.AreEqual(EffectTiming.AmmoOutScaleMax, EffectTiming.AmmoOutScale(3f), 0.0001f);
            Assert.Greater(EffectTiming.AmmoOutScale(0f), 0f, "0을 넣어도 사라지지 않는다");

            // 자리 — 아이콘 위쪽 끝이 본체 아래쪽 끝보다 낮아야 몸통을 안 덮는다.
            float body = ArtSpec.RobotSize;
            float icon = body * scale;
            float y = EffectTiming.AmmoOutFootOffset(body, icon);

            Assert.LessOrEqual(y + icon * 0.5f, -body * 0.5f + 0.0001f,
                "아이콘 윗변이 몸통 아랫변 위로 올라왔다 — 다리를 덮는다");
            Assert.Less(y, EffectTiming.ShadowFootOffset(body),
                "그림자 발밑보다 더 내려간다 — 그 자리는 아직 실루엣 안이다");
        }
    }
}
