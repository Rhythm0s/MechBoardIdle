using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>한 표적의 피격 결과(엔티티 + 데미지 배율). AoE 스플래시는 배율 &lt;1 가능.</summary>
    public struct HitTarget
    {
        public CombatEntity entity;
        public float damageFactor; // 발당피해에 곱하는 배율(직격 1.0, 스플래시 aoeSplashFactor)

        public HitTarget(CombatEntity entity, float damageFactor)
        {
            this.entity = entity;
            this.damageFactor = damageFactor;
        }
    }

    /// <summary>
    /// 탄종 히트 패턴 해석(순수·결정론). 07 문서 탄종 성격 → 패턴:
    /// - 관통(Pierce) = 단일: 표적 1기 직격.
    /// - 분열(Split) = 멀티샷: 최근접 N기를 각각 직격(전부 풀 데미지). 군집 무관, N기 직접 타격.
    /// - 폭발(Explosive) = AoE 스플래시: 피격 몬스터(직격) + 그 위치 기준 반경 내 주변 몬스터(스플래시 배율).
    ///   → AoE는 "착탄점 중심 범위 피해"(사용자 정의). 멀티샷과 구분: 스플래시는 배율로 감쇠 가능.
    /// 각 표적은 시뮬에서 판정식으로 개별 데미지(발당피해×배율). Unity 없이 검증 가능.
    /// ⚠️ multiShotCount·aoeRadius·aoeSplashFactor는 TBD placeholder(소스 확정 대상).
    /// </summary>
    public static class HitResolver
    {
        public static List<HitTarget> Resolve(AmmoKind kind, CombatEntity primary,
            IReadOnlyList<CombatEntity> enemies, int multiShotCount, float aoeRadius, float aoeSplashFactor)
        {
            var result = new List<HitTarget>();
            if (primary == null || !primary.IsAlive || enemies == null) return result;

            switch (kind)
            {
                case AmmoKind.Split: // 멀티샷 — 최근접 N기 직격(풀)
                {
                    int k = Mathf.Max(1, multiShotCount);
                    var living = new List<CombatEntity>();
                    foreach (CombatEntity e in enemies)
                        if (e.IsAlive) living.Add(e);

                    living.Sort((a, b) =>
                    {
                        float da = (a.position - primary.position).sqrMagnitude;
                        float db = (b.position - primary.position).sqrMagnitude;
                        int c = da.CompareTo(db);
                        return c != 0 ? c : IndexOf(enemies, a).CompareTo(IndexOf(enemies, b));
                    });

                    for (int i = 0; i < living.Count && i < k; i++)
                        result.Add(new HitTarget(living[i], 1f));
                    break;
                }
                case AmmoKind.Explosive: // AoE 스플래시 — 직격 + 착탄점 반경 내 주변
                {
                    result.Add(new HitTarget(primary, 1f)); // 직격
                    float r2 = aoeRadius * aoeRadius;
                    foreach (CombatEntity e in enemies)
                    {
                        if (!e.IsAlive || ReferenceEquals(e, primary)) continue;
                        if ((e.position - primary.position).sqrMagnitude <= r2)
                            result.Add(new HitTarget(e, aoeSplashFactor)); // 스플래시(감쇠 배율)
                    }
                    break;
                }
                default: // Pierce 단일
                    result.Add(new HitTarget(primary, 1f));
                    break;
            }
            return result;
        }

        private static int IndexOf(IReadOnlyList<CombatEntity> list, CombatEntity e)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], e)) return i;
            return int.MaxValue;
        }
    }
}
