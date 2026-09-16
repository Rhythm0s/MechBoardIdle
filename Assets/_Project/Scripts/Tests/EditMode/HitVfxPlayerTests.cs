using MBI.Combat;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 피격 VFX — 칸 고르기와 꺼짐 (2026-09-16 · 사용자 승인 §74-7 ②).
    ///
    /// ⚠️⚠️ **끝을 코드가 만든다.** `vfx_hit_explosive` 는 마지막 칸이 첫 칸보다 커서
    /// **스스로 안 꺼진다**(아트 실측). 마지막 칸에서 멈추면 불덩이가 화면에 남는다.
    /// 그래서 뒤 구간을 알파로 뺀다 — 셋을 다르게 다루지 않는다.
    ///
    /// 📌 **칸 번호가 배열 밖으로 나가면 안 된다.** t=1 은 마지막 프레임에서 정확히
    /// 한 번 일어나는데, 거기서 한 칸 넘으면 **마지막 순간에만** 터진다 — 가장 늦게 잡힌다.
    /// </summary>
    public sealed class HitVfxPlayerTests
    {
        [Test]
        public void 칸은_배열_밖으로_안_나간다()
        {
            Assert.That(HitVfxPlayer.FrameAt(4, 0f), Is.EqualTo(0), "처음");
            Assert.That(HitVfxPlayer.FrameAt(4, 1f), Is.EqualTo(3), "끝 — 4 가 나오면 배열 밖이다");
            Assert.That(HitVfxPlayer.FrameAt(4, 2f), Is.EqualTo(3), "1 을 넘겨도 마지막에 머문다");
            Assert.That(HitVfxPlayer.FrameAt(4, -1f), Is.EqualTo(0), "음수도 처음에 머문다");
        }

        [Test]
        public void 칸_넷을_고르게_나눈다()
        {
            Assert.That(HitVfxPlayer.FrameAt(4, 0.1f), Is.EqualTo(0));
            Assert.That(HitVfxPlayer.FrameAt(4, 0.3f), Is.EqualTo(1));
            Assert.That(HitVfxPlayer.FrameAt(4, 0.6f), Is.EqualTo(2));
            Assert.That(HitVfxPlayer.FrameAt(4, 0.9f), Is.EqualTo(3));
        }

        [Test]
        public void 칸이_없으면_없다고_답한다()
        {
            Assert.That(HitVfxPlayer.FrameAt(0, 0.5f), Is.EqualTo(-1), "빈 벌에 칸 번호를 주면 안 된다");
        }

        [Test]
        public void 앞은_그대로_두고_뒤만_뺀다()
        {
            Assert.That(HitVfxPlayer.AlphaAt(0f), Is.EqualTo(1f).Within(0.001f), "처음");
            Assert.That(HitVfxPlayer.AlphaAt(0.5f), Is.EqualTo(1f).Within(0.001f), "가운데는 아직 온전하다");
            Assert.That(HitVfxPlayer.AlphaAt(1f), Is.EqualTo(0f).Within(0.001f),
                "끝에서 0 이 아니면 **불덩이가 남는다**");
        }

        [Test]
        public void 알파는_줄기만_한다()
        {
            float last = 1.01f;
            for (float t = 0f; t <= 1.0001f; t += 0.05f)
            {
                float a = HitVfxPlayer.AlphaAt(t);
                Assert.That(a, Is.LessThanOrEqualTo(last + 0.001f), $"t={t:F2} 에서 다시 밝아졌다");
                Assert.That(a, Is.InRange(0f, 1f), $"t={t:F2} 알파가 범위 밖");
                last = a;
            }
        }

        [Test]
        public void 설치된_자산이_네_칸이다()
        {
            // ⚠️ **자산이 없으면 건너뛴다** — 아트가 아직 안 낸 판에서 빨갛게 만들지 않는다.
            //    있으면 칸 수를 못 박는다: 넷이 아니면 배선 쪽 가정이 깨진 것이다.
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            if (tuning == null) Assert.Ignore("CombatTuning 이 없다 — 자산 생성 전");

            Check(tuning.hitStandardFrames, "표준");
            Check(tuning.hitPierceFrames, "관통");
            Check(tuning.hitExplosiveFrames, "폭발");
        }

        private static void Check(UnityEngine.Sprite[] frames, string label)
        {
            if (frames == null || frames.Length == 0)
                Assert.Ignore($"{label} 피격 VFX 가 아직 없다 — 러너가 코드 플래시로 떨어진다");

            Assert.That(frames.Length, Is.EqualTo(4), $"{label} 은 네 칸이어야 한다");
            for (int i = 0; i < frames.Length; i++)
                Assert.That(frames[i], Is.Not.Null, $"{label} {i}번 칸이 비었다");
        }

        [Test]
        public void 길이는_피격_표시_길이와_같다()
        {
            // 코드 플래시와 이 그림은 **같은 사건**이다 — 길이가 다르면 둘이 어긋나 보인다.
            // 새 값을 만들지 않고 09-15 사용자 확정값(0.12 → 0.35)을 그대로 쓴다.
            Assert.That(EffectTiming.HitFlashDuration, Is.EqualTo(0.35f).Within(0.001f));
        }
    }
}
