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
    /// 관통·분열이 통째로 사라져 HUD 72.5 / 실 DPS 50으로 갈린다. 실수로 들고 있으면
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
    /// - RoundRobin: 싱글샷(관통)→멀티샷(분열)→AoE(폭발) 한 발씩 로테이션. **현재 미사용**(테스트만 참조).
    /// </summary>
    public static class ShotAllocator
    {
        /// <summary>
        /// 한 발씩 로테이션 목록: 관통(단일)→분열(멀티샷)→폭발(AoE) 순, 무기당 1발.
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

        // 발사 순서: 관통(싱글) 0 → 분열(멀티샷) 1 → 폭발(AoE) 2.
        private static int PatternRank(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return 0;
                case AmmoKind.Split: return 1;
                case AmmoKind.Explosive: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// 탄종별 발사 라인 산출(§5-6). 물류 산출 배율을 무기 발사율에 곱한 뒤,
        /// 발당피해가 큰 탄부터 소비 상한(capA=6발/초)까지 채운다 — 고효율 우선(밸런스 07 5장).
        ///
        /// productionScale = 라이브 물류 출력 / 명목 출력. 1이면 만공급, 0.5면 절반 공급.
        /// 결과는 호출자 버퍼에 쓴다(매 프레임 경로 — 반환 리스트를 새로 만들지 않는다).
        /// </summary>
        public static void AllocateRates(IReadOnlyList<WeaponSpec> weapons, float cap,
            float productionScale, List<AmmoLine> into)
        {
            if (into == null) return;
            into.Clear();
            if (weapons == null || cap <= 0f || productionScale <= 0f) return;

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
                float take = w.shotsPerSec * productionScale;
                if (take > remaining) take = remaining;
                if (take <= 0f) continue;

                into.Add(new AmmoLine(w.kind, w.damagePerShot, take));
                remaining -= take;
            }
        }
    }
}
