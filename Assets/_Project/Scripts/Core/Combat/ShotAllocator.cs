using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>한 발의 배정 결과(탄종·발당피해). 시뮬이 순차 발사한다.</summary>
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

        /// <summary>1초분 사격 목록(물류 생산율 pA 기반 고효율 배분). 현재 발사엔 미사용 — 물류 시뮬 연동용.</summary>
        public static List<AllocatedShot> AllocatePerSecond(IReadOnlyList<WeaponSpec> weapons, float cap)
        {
            var shots = new List<AllocatedShot>();
            if (weapons == null || cap <= 0f) return shots;

            // 발당피해 내림차순 정렬(원본 불변 — 복사 후 정렬).
            var sorted = new List<WeaponSpec>(weapons);
            sorted.Sort((a, b) => b.damagePerShot.CompareTo(a.damagePerShot));

            int remaining = Mathf.RoundToInt(cap);
            foreach (WeaponSpec w in sorted)
            {
                if (remaining <= 0) break;
                int rate = Mathf.RoundToInt(w.shotsPerSec);
                int take = rate < remaining ? rate : remaining;
                for (int i = 0; i < take; i++)
                    shots.Add(new AllocatedShot(w.kind, w.damagePerShot));
                remaining -= take;
            }
            return shots;
        }
    }
}
