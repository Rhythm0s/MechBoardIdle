using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 탄종별 발사 라인(탄종·발당피해·초당 발사수). 시뮬이 라인마다 제 주기로 쏜다.
    ///
    /// ⚠️ 왜 정수 발수 목록이 아니라 실수 발사율인가: 물류 산출이 절반으로 떨어지면 pA 1/1/2가
    /// 0.5/0.5/1이 되는데, 정수로 반올림하면 Unity의 RoundToInt가 half-to-even이라 0.5 → 0이다.
    /// 관통·표준이 통째로 사라져 HUD 72.5 / 실 DPS 50으로 갈린다. 실수로 들고 있으면
    /// 0.5발/초 = 2초에 한 발로 정확히 표현된다.
    /// </summary>
    public struct AmmoLine
    {
        public AmmoKind kind;
        public float damagePerShot;
        public float shotsPerSec;

        public AmmoLine(AmmoKind kind, float damagePerShot, float shotsPerSec)
        {
            this.kind = kind;
            this.damagePerShot = damagePerShot;
            this.shotsPerSec = shotsPerSec;
        }
    }

    /// <summary>한 발의 배정 결과(탄종·발당피해). RoundRobin 전용(현재 미사용).</summary>
    public struct AllocatedShot
    {
        public AmmoKind kind;
        public float damagePerShot;

        public AllocatedShot(AmmoKind kind, float damagePerShot)
        {
            this.kind = kind;
            this.damagePerShot = damagePerShot;
        }
    }

    /// <summary>
    /// 사격 목록 산출(순수 함수 — EditMode 검증 가능). 두 모델:
    /// - AllocatePerSecond: 물류 생산율(pA) 기반 고효율 우선 배분. **현재 발사 모델**(StageRunner가 호출).
    /// - RoundRobin: 싱글샷(관통)→멀티샷(표준)→AoE(폭발) 한 발씩 로테이션. **현재 미사용**(테스트만 참조).
    /// </summary>
    public static class ShotAllocator
    {
        /// <summary>
        /// 한 발씩 로테이션 목록: 관통(단일)→표준(멀티샷)→폭발(AoE) 순, 무기당 1발.
        /// 시뮬이 이 목록을 순환 발사 → 매 회전마다 세 패턴을 한 번씩 사용(사용자 지정 발사 모델).
        /// ⚠️ 현재 프로덕션 호출자 없음(ShotAllocatorTests만 참조). 삭제 여부는 사용자 승인 대기.
        /// </summary>
        public static List<AllocatedShot> RoundRobin(IReadOnlyList<WeaponSpec> weapons)
        {
            var shots = new List<AllocatedShot>();
            if (weapons == null) return shots;

            var sorted = new List<WeaponSpec>(weapons);
            sorted.Sort((a, b) => PatternRank(a.kind).CompareTo(PatternRank(b.kind)));

            foreach (WeaponSpec w in sorted)
                shots.Add(new AllocatedShot(w.kind, w.damagePerShot));
            return shots;
        }

        // 발사 순서: 관통(싱글) 0 → 표준(멀티샷) 1 → 폭발(AoE) 2.
        private static int PatternRank(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return 0;
                case AmmoKind.Standard: return 1;
                case AmmoKind.Explosive: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// 탄종별 발사 라인 산출(§5-6). 물류 산출 배율을 무기 발사율에 곱한 뒤,
        /// 발당피해가 큰 탄부터 소비 상한(capA=6발/초)까지 채운다 — 고효율 우선(플레이어블 로봇 기획서「무기 스펙트럼」).
        ///
        /// productionScale = 라이브 물류 출력 / 명목 출력. 1이면 만공급, 0.5면 절반 공급.
        /// 결과는 호출자 버퍼에 쓴다(매 프레임 경로 — 반환 리스트를 새로 만들지 않는다).
        /// </summary>
        public static void AllocateRates(IReadOnlyList<WeaponSpec> weapons, float cap,
            float productionScale, List<AmmoLine> into)
        {
            // ⚠️ **구 경로는 곱하기다.** 새 경로(`supplyOf`)는 **최소값**이라 뜻이 다르다 —
            // 처음에 `_ => productionScale` 로 이어 붙였더니 배율이 **절대 발수**로 읽혀
            // 시험 일곱이 빨개졌다(2026-09-15). 둘은 같은 고르기를 쓰되 **take 만 다르다.**
            if (productionScale <= 0f) { into?.Clear(); return; }
            Allocate(weapons, cap, w => w.shotsPerSec * productionScale, into);
        }

        /// <summary>
        /// **탄종별 공급율로 배분한다** (2026-09-15 사용자 확정 · 문서 이행).
        ///
        /// ⚠️ **종전은 「명목 출력 대비 전역 비율」 하나였다.** 그러면 표준탄만 오는 보드에서도
        /// 관통·폭발이 발사율을 배분받는다 — 화면에는 「관통 0.2 · 폭발 0.5 발/초」로 찍히는데
        /// 마운트에 그 탄이 없어 `ConsumeRound` 가 실패하고 **한 발도 안 나간다.**
        /// 배분과 실제가 갈려 **HUD 가 거짓말을 했다.**
        ///
        /// <paramref name="supplyOf"/> 는 그 탄종이 **마운트에 실제로 닿는 비율**(발/초)이다.
        /// 한 줄이 쏠 수 있는 것은 **스펙과 공급 중 작은 쪽**이다 — 스펙이 6발이어도
        /// 공급이 1발이면 1발이고, 공급이 넘쳐도 스펙을 넘지 않는다.
        /// </summary>
        public static void AllocateRates(IReadOnlyList<WeaponSpec> weapons, float cap,
            System.Func<AmmoKind, float> supplyOf, List<AmmoLine> into)
        {
            if (supplyOf == null) { into?.Clear(); return; }
            Allocate(weapons, cap, w => Mathf.Min(w.shotsPerSec, supplyOf(w.kind)), into);
        }

        /// <summary>
        /// **재고가 있으면 스펙대로, 비면 공급이 상한** (2026-09-15 사용자 확정 · 발사 규칙 (가)).
        ///
        /// ⚠️ **왜 공급율 하나로는 안 됐나.** 공급율(도착률)은 **흐름**이고 재고는 **고임**이다.
        /// 마운트에 표준탄이 40 발 실려 있어도 그 순간 벨트가 쉬고 있으면 도착률은 0 이고,
        /// 그러면 줄이 아예 안 서서 **실탄을 지고도 한 발을 못 쐈다** — 09-15 육안이 그것이다
        /// (창고 40/40 · 마운트 40 · 적 96 기 · 78 초 0 발).
        ///
        /// 이제 규칙은 **재고를 먼저 본다.**
        /// · 재고 &gt; 0  → <c>lineSpecShots</c>(무기 스펙) 상한까지 쏜다. 쌓인 것을 쓰는 구간이다.
        /// · 재고 == 0 → **도착률이 상한**이다. 버는 만큼만 쏘는 구간이다.
        ///
        /// ⚠️ **등가선 재산출은 설계 사후다** — 이 규칙은 재고가 있는 동안 발사율을 스펙까지
        /// 올리므로 DPS 곡선이 종전과 다르다. 밸런스 값은 여기서 안 만진다.
        ///
        /// ⚠️ <c>ConsumeRound</c> 실패 시 안 세는 것은 그대로다 — 이 배분은 **상한**이고,
        /// 실제로 나간 발수는 여전히 마운트에서 한 발을 빼는 데 성공한 것만 센다.
        /// </summary>
        public static void AllocateRates(IReadOnlyList<WeaponSpec> weapons, float cap,
            System.Func<AmmoKind, float> supplyOf, System.Func<AmmoKind, float> stockOf,
            List<AmmoLine> into)
        {
            if (supplyOf == null || stockOf == null) { into?.Clear(); return; }
            Allocate(weapons, cap,
                w => stockOf(w.kind) > 0f ? w.shotsPerSec : Mathf.Min(w.shotsPerSec, supplyOf(w.kind)),
                into);
        }

        /// <summary>고효율 우선으로 상한까지 채운다. **한 줄이 가져갈 양만 밖에서 정한다.**</summary>
        private static void Allocate(IReadOnlyList<WeaponSpec> weapons, float cap,
            System.Func<WeaponSpec, float> takeOf, List<AmmoLine> into)
        {
            if (into == null) return;
            into.Clear();
            if (weapons == null || cap <= 0f) return;

            // 발당피해 내림차순으로 훑되 원본을 건드리지 않고 복사도 하지 않는다.
            // 무기는 마운트 A/B 붙박이라 수가 아주 작다(MVP 가드레일 §4) → 선택 정렬로 충분하고 할당이 0.
            int n = weapons.Count;
            if (n > 31) n = 31; // 비트마스크 한도. MVP 무기 수(≤3)와 무관한 안전장치.
            int used = 0;
            float remaining = cap;

            for (int picked = 0; picked < n && remaining > 0f; picked++)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    if ((used & (1 << i)) != 0) continue;
                    if (best < 0 || weapons[i].damagePerShot > weapons[best].damagePerShot) best = i;
                }
                if (best < 0) break;
                used |= 1 << best;

                WeaponSpec w = weapons[best];

                // ⚠️ **0 이면 그 줄은 아예 안 선다** — 0 발짜리 줄을 넣으면
                // HUD 가 「쏘는 중」으로 읽는다.
                float take = takeOf(w);
                if (take > remaining) take = remaining;
                if (take <= 0f) continue;

                into.Add(new AmmoLine(w.kind, w.damagePerShot, take));
                remaining -= take;
            }
        }
    }
}
