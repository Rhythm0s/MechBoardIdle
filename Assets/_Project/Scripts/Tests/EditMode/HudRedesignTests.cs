using MBI.Core;
using MBI.Core.Combat;
using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 2026-09-18 **시안 3 HUD 재작성**이 들여온 규칙들 (사용자 확정 · 플랜 §85-8).
    ///
    /// 여기서 재는 것은 **순수 규칙**뿐이다 — 골드 적립 · 웨이브 스폰 · 전투력 · 마일스톤 ·
    /// 문제 목록 · 오른쪽 기둥의 자리. 그림은 안 잰다(잴 수 있는 것과 없는 것을 섞지 않는다).
    /// </summary>
    public sealed class HudRedesignTests
    {
        private const float D = 0.001f;

        // ── 골드 — 「몬스터 20처치마다 5골드」(사용자 확정) ──────────────────

        [Test]
        public void 골드는_스무_마리마다_다섯이다()
        {
            int carry = 0;
            Assert.AreEqual(0, GoldRewardRule.Award(ref carry, 19, 20, 5), "열아홉에는 안 나온다");
            Assert.AreEqual(19, carry, "남은 처치는 그대로 든다");

            Assert.AreEqual(5, GoldRewardRule.Award(ref carry, 1, 20, 5), "스물째에 5");
            Assert.AreEqual(0, carry);
        }

        [Test]
        public void 한_번에_많이_잡아도_나머지를_안_버린다()
        {
            // ⚠️ 버리면 **많이 잡을수록 손해**가 되어 규칙이 뒤집힌다.
            int carry = 0;
            Assert.AreEqual(10, GoldRewardRule.Award(ref carry, 45, 20, 5), "마흔다섯 = 두 번 = 10");
            Assert.AreEqual(5, carry, "남은 다섯이 다음으로 넘어간다");
        }

        [Test]
        public void 값이_없으면_아무것도_안_준다()
        {
            // 자산이 0 이면 지급이 0 이다 — 미확정을 기본값으로 덮어 감추지 않는다.
            int carry = 0;
            Assert.AreEqual(0, GoldRewardRule.Award(ref carry, 100, 0, 5));
            Assert.AreEqual(0, GoldRewardRule.Award(ref carry, 100, 20, 0));
        }

        [Test]
        public void 지갑은_골드를_따로_든다()
        {
            // 고철·강화재료와 **서로 안 바뀐다** — 변환 메서드가 없는 것이 그 표현이다.
            var w = new CurrencyWallet(scrap: 10d, enhMaterial: 2d, gold: 7d);
            w.AddGold(5d);
            Assert.AreEqual(12d, w.Gold, D);
            Assert.AreEqual(10d, w.Scrap, D, "골드를 넣어도 고철은 그대로다");

            Assert.IsFalse(w.TrySpendGold(100d), "모자라면 아무것도 안 깎는다");
            Assert.AreEqual(12d, w.Gold, D);
        }

        // ── 웨이브 스폰 ─────────────────────────────────────────────────────

        [Test]
        public void 묶음이_하나면_종전_모델과_같은_수다()
        {
            // 📌 **되돌릴 길이 값 하나로 남아 있다** — 묶음 1 이면 index × 간격이다.
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(i * 0.8f, WaveSpawnRule.SpawnTime(i, 1, 0.8f), D);
        }

        [Test]
        public void 같은_묶음은_같은_시각에_나온다()
        {
            // 넷씩 묶으면 0~3 은 0초, 4~7 은 3.2초다.
            Assert.AreEqual(0f, WaveSpawnRule.SpawnTime(3, 4, 3.2f), D);
            Assert.AreEqual(3.2f, WaveSpawnRule.SpawnTime(4, 4, 3.2f), D);
            Assert.AreEqual(3.2f, WaveSpawnRule.SpawnTime(7, 4, 3.2f), D);
            Assert.AreEqual(6.4f, WaveSpawnRule.SpawnTime(8, 4, 3.2f), D);
        }

        [Test]
        public void 간격을_안_주면_평균_속도가_종전과_같다()
        {
            // ⚠️ 값을 지어내지 않는다 — 이미 있는 cadence 에서 끈다.
            Assert.AreEqual(3.2f, WaveSpawnRule.Interval(0f, 4, 0.8f), D, "묶음 × cadence");

            // 평균 마리/초가 같다: 종전 1/0.8 = 1.25, 웨이브 4/3.2 = 1.25.
            Assert.AreEqual(1f / 0.8f, 4f / WaveSpawnRule.Interval(0f, 4, 0.8f), D);
        }

        [Test]
        public void 더_나올_적이_없으면_음수다()
        {
            // 「남은 시간 0」과 「더 없음」은 다른 뜻이라 다른 수여야 한다.
            Assert.Less(WaveSpawnRule.SecondsToNextWave(10f, 8, 8, 4, 3.2f), 0f, "다 나왔다");
            Assert.AreEqual(0f, WaveSpawnRule.SecondsToNextWave(5f, 4, 8, 4, 3.2f), D, "지금 나온다");
            Assert.AreEqual(1.2f, WaveSpawnRule.SecondsToNextWave(2f, 4, 8, 4, 3.2f), D);
        }

        [Test]
        public void 리젠_시계는_분초로_적는다()
        {
            Assert.AreEqual("--:--", WaveSpawnRule.Clock(-1f), "더 없음은 시계가 아니다");
            Assert.AreEqual("00:00", WaveSpawnRule.Clock(0f));
            Assert.AreEqual("00:04", WaveSpawnRule.Clock(3.2f), "남은 시간은 올려 센다");
            Assert.AreEqual("01:05", WaveSpawnRule.Clock(64.2f));
        }

        // ── 전투력 ──────────────────────────────────────────────────────────

        [Test]
        public void 전투력은_기본으로_물류_출력_그대로다()
        {
            // 📌 지시는 「기존 물류 출력 × 마운트계수 · **이름만**」이다.
            // 🗑️ 구 기본값(계수 1 · 적재율만큼 부풀림) 폐기 — 같은 화면의 물류 출력과 갈렸다.
            Assert.AreEqual(100, CombatPower.Of(100f, 0f, 40f), "마운트가 비어도");
            Assert.AreEqual(100, CombatPower.Of(100f, 40f, 40f), "가득 차도 — 안 곱한다");
        }

        [Test]
        public void 마운트계수가_서면_그때_곱한다()
        {
            // 값이 확정되면 여기 한 줄이 살아난다 — 지금은 자산이 0 이라 안 곱한다.
            Assert.AreEqual(200, CombatPower.Of(100f, 40f, 40f, 1f), "계수 1 · 가득");
            Assert.AreEqual(150, CombatPower.Of(100f, 20f, 40f, 1f), "계수 1 · 절반");
        }

        [Test]
        public void 상한이_없으면_배수를_지어내지_않는다()
        {
            // ⚠️ 만충 판정 자체가 없는 자리다 — 거기서 배수를 만들면 화면이 거짓말을 한다.
            Assert.AreEqual(100, CombatPower.Of(100f, 999f, 0f));
        }

        // ── 마일스톤 ────────────────────────────────────────────────────────

        [Test]
        public void 마일스톤_보상은_한_번만_받는다()
        {
            var card = new MilestoneCard();
            Assert.IsFalse(card.CanClaim(false), "달성 전에는 못 받는다");
            Assert.IsFalse(card.TryClaim(false));

            Assert.IsTrue(card.TryClaim(true));
            Assert.IsTrue(card.Claimed);
            Assert.IsFalse(card.TryClaim(true), "두 번째는 아무 일도 안 일어난다");
        }

        // ── 조립 화면 상단 문제 목록 ────────────────────────────────────────

        [Test]
        public void 문제가_없으면_목록이_빈다()
        {
            // ⚠️ 없는 문제를 지어내 채우지 않는다.
            Assert.AreEqual(0, SupplyStopRules.Problems(false, false, false, false).Count);
        }

        [Test]
        public void 문제마다_한_줄씩_난다()
        {
            var all = SupplyStopRules.Problems(true, true, true, true);
            Assert.AreEqual(4, all.Count);
            StringAssert.Contains("미연결", all[0]);
            StringAssert.Contains("생산 정지", all[1]);
            StringAssert.Contains("전력 부족", all[2]);
        }

        // ── 오른쪽 기둥의 차례 ──────────────────────────────────────────────

        [Test]
        public void 오른쪽_기둥_넷이_서로_안_겹친다()
        {
            // 사용자 확정 차례 — 태그 → 자동 → 합체 → 마일스톤 → (조립 막대).
            const float W = 1440f, H = 2560f;
            Rect tag = UiLayout.RoundButtonRect(0, W, H);
            Rect auto = UiLayout.TagAutoToggleRect(W, H);
            Rect merge = UiLayout.RoundButtonRect(1, W, H);
            Rect card = UiLayout.MilestoneCardRect(W, H);
            Rect enter = UiLayout.EnterBoardRect(W, H);

            Assert.Less(tag.yMax, auto.y, "태그 아래 자동");
            Assert.Less(auto.yMax, merge.y, "자동 아래 합체");
            Assert.Less(merge.yMax, card.y, "합체 아래 마일스톤");
            Assert.LessOrEqual(card.yMax, enter.y, "마일스톤 아래가 조립 막대다");
            Assert.LessOrEqual(enter.yMax, H, "화면 밖으로 안 나간다");
        }

        [Test]
        public void 칩_줄의_셋이_서로_안_겹친다()
        {
            const float W = 1440f, H = 2560f;
            Rect left = UiLayout.ChipLeftRect(W, H);
            Rect gold = UiLayout.ChipGoldRect(W, H);
            Rect sound = UiLayout.ChipSoundRect(W, H);
            Rect settings = UiLayout.ChipSettingsRect(W, H);

            Assert.IsFalse(left.Overlaps(gold), "왼쪽 무리와 골드 칩");
            Assert.IsFalse(gold.Overlaps(sound), "골드 칩과 소리");
            Assert.IsFalse(sound.Overlaps(settings), "소리와 설정");

            // 📌 **소리 버튼은 이 자리 하나에만 산다** — 칩 줄이 따로 그리면 자리가 둘이 된다.
            Assert.IsTrue(UiLayout.MeetsMinButton(UiLayout.ChipIconSide),
                "아이콘 한 변은 눌리는 최소를 넘는다");
        }

        [Test]
        public void 칩_줄은_상단_정보줄_안에_든다()
        {
            const float W = 1440f, H = 2560f;
            Rect bar = UiLayout.ChipBarRect(W, H);
            Assert.LessOrEqual(bar.yMax, UiLayout.InfoBarHeight, "칩 줄은 상단 정보줄 120 안이다");
            Assert.Less(bar.yMax, UiLayout.StageBadgeRect(W, H).y, "배지는 칩 줄 아래");
        }
    }
}
