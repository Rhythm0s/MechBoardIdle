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
        public void 라인_스펙은_안_건드렸다()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            if (bal == null) Assert.Ignore("BalanceConfig 가 없다");

            // ⚠️⚠️ **한 번 올렸다가 되돌린 값이다**(2026-09-15 · 같은 날 안에서).
            //
            // 「발사율 ×2」를 이 값으로 알고 두 배(10·12·4)로 올렸는데, **이 필드는
            // 발사율이 아니다** — `LineSpecOf` 를 거쳐 `WorkloadRate` 가 읽고,
            // **군수 노드 몇 대까지 일하는가**(생산 상한)를 정한다.
            // 즉 **사용자가 정한 적 없는 축**을 두 배로 연 셈이었다.
            //
            // 발사율의 진짜 자리는 `RobotDefinition.weapons[].shotsPerSec` 이고 그쪽은
            // 제대로 두 배가 됐다(위 시험 둘이 그것을 지킨다).
            Assert.That(bal.lineSpecShots.x, Is.EqualTo(5f).Within(0.001f), "관통 라인 스펙");
            Assert.That(bal.lineSpecShots.y, Is.EqualTo(6f).Within(0.001f), "표준 라인 스펙");
            Assert.That(bal.lineSpecShots.z, Is.EqualTo(2f).Within(0.001f), "폭발 라인 스펙");
        }

        [Test]
        public void 발사수는_두_칸이다()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            RobotDefinition r = Robot();
            if (bal == null) Assert.Ignore("BalanceConfig 가 없다");

            // ✅ **설계 판정이다**(2026-09-16 · `260915_W01` 판정 1). 종전 이 시험은
            //    「지금 둘이 다르다」를 못 박아 **고칠 자리를 잊지 않으려는 표식**이었다.
            //    설계가 답했으므로 이제 단언이 바뀐다 — **합칠 것이 아니라 두 칸이다.**
            //
            // · `BalanceConfig.lineSpecShots` = **생산 상한** — 군수 노드 몇 대까지 일하는가.
            //   `LineSpecOf` 를 거쳐 `WorkloadRate` 가 읽는다.
            // · `RobotDefinition.weapons[].shotsPerSec` = **발사율** — 초당 몇 발 쏘는가.
            //   `ShotAllocator` 가 읽는다.
            //
            // ⚠️ 문서가 둘을 **한 이름**으로 불러서 「세 배 어긋남」처럼 보였다. 어긋난 것이
            //    아니라 **다른 축**이었다. 등가선과 대표 조합 140·180 은 발사율 × 피해로
            //    재산출한다(설계 몫 · 09-17).
            Assert.That(bal.lineSpecShots.x, Is.EqualTo(5f).Within(0.001f), "관통 생산 상한");
            Assert.That(bal.lineSpecShots.y, Is.EqualTo(6f).Within(0.001f), "표준 생산 상한");
            Assert.That(bal.lineSpecShots.z, Is.EqualTo(2f).Within(0.001f), "폭발 생산 상한");

            Assert.That(r.weapons[0].shotsPerSec, Is.EqualTo(2f).Within(0.001f), "관통 발사율");
            Assert.That(r.weapons[1].shotsPerSec, Is.EqualTo(2f).Within(0.001f), "표준 발사율");
            Assert.That(r.weapons[2].shotsPerSec, Is.EqualTo(4f).Within(0.001f), "폭발 발사율");

            // 📌 **둘을 같은 값으로 맞추지 않는다.** 같게 만들면 두 축이 다시 한 칸으로
            //    접히고, 생산을 건드릴 때마다 발사가 같이 움직인다.
        }
    }
}
