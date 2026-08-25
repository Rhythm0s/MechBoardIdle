using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>한 탄종의 생산 입력. 보드에 놓인 군수 노드 수가 유일한 변수다.</summary>
    public struct MunitionsLine
    {
        public AmmoKind kind;
        /// <summary>100% 가동 시 발사율(발/초). params specA0/1/2 = 5 / 4 / 2.</summary>
        public float specShotsPerSec;
        /// <summary>발당피해. params dA0/1/2 = 20 / 25 / 50.</summary>
        public float damagePerShot;
        /// <summary>이 탄종에 배정된 군수 노드 수.</summary>
        public int nodeCount;

        public MunitionsLine(AmmoKind kind, float specShotsPerSec, float damagePerShot, int nodeCount)
        {
            this.kind = kind;
            this.specShotsPerSec = specShotsPerSec;
            this.damagePerShot = damagePerShot;
            this.nodeCount = nodeCount;
        }
    }

    /// <summary>
    /// 탄종별 생산 산출(260824_V02 §1). **보드에 놓인 군수 노드 수가 발사율을 만든다.**
    ///
    /// 확정 공식:
    ///   라인 가동률 = min(1, 보유 노드 수 ÷ 필요 노드 수)
    ///   라인 생산량 = 탄종 발사수 스펙 × 라인 가동률
    /// 노드 1개 = 1발/초(params muniPerNode)이므로 필요 노드 수 = 그 탄종의 발사수 스펙이고,
    /// 두 줄은 결국 **라인 생산량 = min(스펙, 보유 노드 수 × 노드당 생산)** 으로 접힌다.
    ///
    /// | 탄종 | 스펙 | 100% 가동 필요 노드 | 비용 배수 |
    /// | 관통 | 5발/초 | 5 | 1.0  |
    /// | 분열 | 4발/초 | 4 | 1.25 |
    /// | 폭발 | 2발/초 | 2 | 2.5  |
    /// 비용 배수는 등가선(세 탄종 모두 스펙 × 발당피해 = 100)에서 자동 도출되므로 별도 확정 항목이 아니다.
    ///
    /// 왜 상한이 필요한가: 스펙을 넘겨 노드를 더 박아도 그 라인은 더 못 쏜다. 상한이 없으면
    /// 한 탄종에 노드를 몰아넣는 것이 언제나 최적이 되어 조합 축이 죽는다.
    ///
    /// ⚠️ 여기서 **소비 상한(capA 6발/초)은 적용하지 않는다.** 생산과 소비는 별개의 축이다 —
    /// capA를 생산 자리에 넣었다가 「노드 하나면 이미 만렙」이 됐던 이력이 있다(CLAUDE.md §7).
    /// 소비 배분은 ShotAllocator가 고효율 탄 우선 규칙으로 따로 한다.
    /// </summary>
    public static class AmmoLineProduction
    {
        /// <summary>탄종 하나의 생산량(발/초). 노드가 스펙을 넘어도 스펙에서 멈춘다.</summary>
        public static float LineOutput(float spec, int nodeCount, float perNodeRate)
        {
            if (spec <= 0f || nodeCount <= 0 || perNodeRate <= 0f) return 0f;
            return Mathf.Min(spec, nodeCount * perNodeRate);
        }

        /// <summary>가동률 0~1. HUD·진단 표시용 — 생산량 계산 자체는 LineOutput 하나로 끝난다.</summary>
        public static float Utilization(float spec, int nodeCount, float perNodeRate)
        {
            if (spec <= 0f || nodeCount <= 0 || perNodeRate <= 0f) return 0f;
            return Mathf.Clamp01(nodeCount * perNodeRate / spec);
        }

        /// <summary>100% 가동에 필요한 노드 수. 노드 1개 = 1발/초면 스펙과 같은 수가 된다.</summary>
        public static int NodesForFullLine(float spec, float perNodeRate)
        {
            if (spec <= 0f || perNodeRate <= 0f) return 0;
            return Mathf.CeilToInt(spec / perNodeRate);
        }

        /// <summary>
        /// 탄종별 노드 배정 → 발사 라인. 생산이 0인 탄종은 라인을 만들지 않는다
        /// (빈 라인이 발사 누산기 자리를 먹지 않게).
        /// </summary>
        public static void BuildLines(IReadOnlyList<MunitionsLine> inputs, float perNodeRate, List<AmmoLine> into)
        {
            if (into == null) return;
            into.Clear();
            if (inputs == null) return;

            for (int i = 0; i < inputs.Count; i++)
            {
                MunitionsLine m = inputs[i];
                float rate = LineOutput(m.specShotsPerSec, m.nodeCount, perNodeRate);
                if (rate <= 0f) continue;

                into.Add(new AmmoLine(m.kind, m.damagePerShot, rate));
            }
        }

        /// <summary>라인들의 명목 출력 합(Σ 발사율 × 발당피해). 보드 배치가 만드는 전투력.</summary>
        public static float TotalOutput(IReadOnlyList<MunitionsLine> inputs, float perNodeRate)
        {
            if (inputs == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < inputs.Count; i++)
            {
                MunitionsLine m = inputs[i];
                sum += LineOutput(m.specShotsPerSec, m.nodeCount, perNodeRate) * m.damagePerShot;
            }
            return sum;
        }
    }
}
