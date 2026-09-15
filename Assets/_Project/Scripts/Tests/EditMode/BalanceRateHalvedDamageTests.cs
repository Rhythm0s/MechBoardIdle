using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 발사율 ×2 · 발당 피해 ÷2 (2026-09-15 사용자 확정 (가) · 육안 6차 ②).
    ///
    /// 사용자가 고른 것은 「마운트에서 **탄환 소비량을 늘리고** 대미지를 그만큼 **줄인다**」였다.
    /// 그래서 **초당 피해(DPS)는 그대로이고 탄약 회전만 빨라진다** — 그것이 이 판정의 뜻이다.
    ///
    /// ⚠️ **DPS 가 안 변한다는 것을 시험이 지킨다.** 한쪽만 고치면(발사율만 올리거나
    /// 피해만 내리면) 밸런스가 조용히 두 배 또는 절반이 된다. 값을 손으로 옮기다
    /// 한 줄을 빠뜨리는 것은 **에러 없이** 일어난다.
    ///
    /// ⚠️ **등가선·요구치 재산출은 설계 사후다** — 여기서 지키는 것은 「두 축이 맞물려
    /// 움직였는가」 하나다.
    /// </summary>
    public sealed class BalanceRateHalvedDamageTests
    {
        private const string RobotA = "Assets/_Project/ScriptableObjects/Robots/Robot_A.asset";
        private const string BalancePath = "Assets/_Project/ScriptableObjects/BalanceConfig.asset";

        /// <summary>09-15 판정 전의 값 — 이것과 견주어 「맞물려 움직였는가」를 본다.</summary>
        private static readonly (AmmoKind kind, float dmg, float rate)[] Before =
        {
            (AmmoKind.Pierce, 20f, 1f),
            (AmmoKind.Standard, 10f, 1f),
            (AmmoKind.Explosive, 50f, 2f),
        };

        private static RobotDefinition Robot()
        {
            var r = AssetDatabase.LoadAssetAtPath<RobotDefinition>(RobotA);
            if (r == null) Assert.Ignore("Robot_A 가 없다 — 자산 생성 전 클론");
            return r;
        }

        [Test]
        public void 발사율은_두_배_피해는_절반이다()
        {
            RobotDefinition r = Robot();
            Assert.That(r.weapons.Count, Is.EqualTo(Before.Length), "무기 수가 달라졌다");

            for (int i = 0; i < Before.Length; i++)
            {
                WeaponSpec w = r.weapons[i];
                Assert.That(w.kind, Is.EqualTo(Before[i].kind), $"{i}번째 탄종이 바뀌었다");
                Assert.That(w.shotsPerSec, Is.EqualTo(Before[i].rate * 2f).Within(0.001f),
                    $"{w.kind} 발사율 — 구 {Before[i].rate} 의 두 배여야 한다");
                Assert.That(w.damagePerShot, Is.EqualTo(Before[i].dmg / 2f).Within(0.001f),
                    $"{w.kind} 발당 피해 — 구 {Before[i].dmg} 의 절반이어야 한다");
            }
        }

        [Test]
        public void 초당_피해는_그대로다()
        {
            RobotDefinition r = Robot();

            float before = 0f, now = 0f;
            for (int i = 0; i < Before.Length; i++)
            {
                before += Before[i].dmg * Before[i].rate;
                now += r.weapons[i].damagePerShot * r.weapons[i].shotsPerSec;
            }

            Assert.That(now, Is.EqualTo(before).Within(0.001f),
                $"초당 피해가 {before} → {now} 로 달라졌다. "
                + "이 판정은 **회전만 빨라지고 세기는 그대로**여야 한다 — "
                + "한 축만 고쳤는지 본다");
        }

        [Test]
        public void 소비_상한이_발사율_합을_안_깎는다()
        {
            RobotDefinition r = Robot();

            float sum = 0f;
            foreach (WeaponSpec w in r.weapons) sum += w.shotsPerSec;

            // ⚠️ **상한을 같이 안 올리면 두 배가 안 된다.** `ShotAllocator` 가 라인 합을
            // `consumptionCap` 으로 깎으므로, 상한이 6 인 채로 발사율만 올리면
            // **화면에서는 아무것도 안 바뀐다** — 값을 고치고도 안 바뀌는 자리다.
            Assert.That(r.consumptionCap, Is.GreaterThanOrEqualTo(sum),
                $"발사율 합 {sum} 인데 소비 상한이 {r.consumptionCap} 이다 — 상한이 깎는다");
        }

        [Test]
        public void 라인_스펙도_두_배로_따라갔다()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            if (bal == null) Assert.Ignore("BalanceConfig 가 없다");

            // 구 (관통 5 · 표준 6 · 폭발 2) → 두 배.
            Assert.That(bal.lineSpecShots.x, Is.EqualTo(10f).Within(0.001f), "관통 라인 스펙");
            Assert.That(bal.lineSpecShots.y, Is.EqualTo(12f).Within(0.001f), "표준 라인 스펙");
            Assert.That(bal.lineSpecShots.z, Is.EqualTo(4f).Within(0.001f), "폭발 라인 스펙");
        }

        [Test]
        public void 라인_스펙과_무기_발사율이_다른_값이라는_것을_남긴다()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            RobotDefinition r = Robot();
            if (bal == null) Assert.Ignore("BalanceConfig 가 없다");

            // ⚠️⚠️ **이 시험은 「같다」가 아니라 「다르다」를 못 박는다** — 고칠 자리를
            // 잊지 않으려는 표식이다(2026-09-15 · 육안 6차 ② 이행 중 발견).
            //
            // `BalanceConfig.lineSpecShots`(문서 쪽 · 표준 12)와
            // `RobotDefinition.weapons[].shotsPerSec`(코드가 실제로 쓰는 값 · 표준 2)가
            // **여섯 배 어긋나 있다.** 둘 다 「표준탄 발사수(발/초)」라고 불리는데
            // **`ShotAllocator` 가 읽는 것은 뒤쪽 하나뿐**이고 `SpecShotsOf` 는
            // **부르는 곳이 0건**이다.
            //
            // 📌 **어느 쪽이 맞는지는 구현이 정할 것이 아니다**(등가선이 걸린다).
            // 설계가 정하면 이 시험을 「같다」로 뒤집고 죽은 쪽을 지운다.
            Assert.That(bal.lineSpecShots.y, Is.Not.EqualTo(r.weapons[1].shotsPerSec),
                "둘이 같아졌다면 설계가 하나로 합친 것이다 — 이 시험을 「같다」로 바꾸고 "
                + "`SpecShotsOf` 의 죽은 갈래를 지운다");
        }
    }
}
