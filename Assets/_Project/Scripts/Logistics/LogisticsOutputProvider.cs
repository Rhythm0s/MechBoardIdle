using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Logistics
{
    /// <summary>
    /// 라이브 물류 네트워크 → 출력 반영(§5-6, L4-R). 배치 노드 집계(LogisticsNetwork) → 흐름시뮬
    /// (LogisticsSimulation: expected/actual/gap) → LogisticsOutputBridge(Output=actual·Expected·Gap·GlobalCause).
    /// 노드별 진단(LogisticsDiagnostics) → BoardController 상태색. actual은 60초 롤링(움직이는 거울).
    ///
    /// ⚠️ 연결성/체인 미강제(합계 기반) — 정밀 흐름/노드 cause는 근사(R1). 병목 수치 전부 TBD(LogisticsConfig).
    /// </summary>
    public sealed class LogisticsOutputProvider : MonoBehaviour
    {
        [Tooltip("물류 보드. 씬 생성기가 주입.")]
        public BoardController board;
        [Tooltip("병목 파라미터(TBD). 없으면 기본값.")]
        public LogisticsConfig config;
        [Tooltip("로봇. 명목 출력·원점·탄약 수요의 단일 원천. 씬 생성기가 주입.")]
        public RobotDefinition robot;
        [Tooltip("실측(actual) 롤링 창(초). 밸런스 계약(CLAUDE.md §9 실측 60초 롤링) — 임의 변경 금지.")]
        public float rollingWindow = 60f;

        // 롤링 채널: 0 expected · 1 actual · 2 gapPower · 3 gapHeat · 4 gapBelt.
        // 같은 샘플 집합을 쓰므로 롤링 후에도 분해합 == 총갭이 성립한다.
        private const int ChExpected = 0, ChActual = 1, ChGapPower = 2, ChGapHeat = 3, ChGapBelt = 4;
        private RollingWindow _roll;
        private readonly float[] _sample = new float[5];

        // 탄종별 생산 입력 버퍼(§1). 매 프레임 재사용 — GC 0.
        private readonly System.Collections.Generic.List<MunitionsLine> _muniLines =
            new System.Collections.Generic.List<MunitionsLine>(3);

        /// <summary>군수 노드 1개당 생산(발/초). 원천 = balance_v4 muniPerNode 확정치 1.</summary>
        private float PerNodeRate => robot != null && robot.balanceRef != null ? robot.balanceRef.muniPerNode : 1f;

        /// <summary>
        /// 보드의 탄종별 군수 노드 수 → 생산 입력. 발당피해는 무기 스펙에서,
        /// 라인 스펙(5/4/2)은 밸런스 앵커에서 온다.
        /// </summary>
        private void BuildMunitionsLines(NetworkAggregate agg)
        {
            _muniLines.Clear();
            if (robot == null || robot.weapons == null) return;

            BalanceConfig bal = robot.balanceRef;

            for (int i = 0; i < robot.weapons.Count; i++)
            {
                WeaponSpec w = robot.weapons[i];
                float spec = bal != null ? bal.LineSpecOf(w.kind) : 0f;
                if (spec <= 0f) continue;

                _muniLines.Add(new MunitionsLine(w.kind, spec, w.damagePerShot, agg.MuniCountOf(w.kind)));
            }
        }

        private void Awake()
        {
            LogisticsOutputBridge.Reset(); // 도메인 리로드 비활성 시 이전 Play 값이 남는 것 방지
            _roll = new RollingWindow(5, rollingWindow);
        }

        private void Update()
        {
            if (board == null || robot == null) return;
            BoardGrid grid = board.Grid;
            if (grid == null) return;

            // 100은 인스펙터 리터럴이 아니라 원천에서 온다(§3 수치 하드코딩 금지).
            // 브릿지는 물류 단위 = 마운트계수 미적용(마운트계수는 판정식 내부 항 = 전투 측).
            float origin = robot.balanceRef != null ? robot.balanceRef.origin : 100f;

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid);
            LogisticsOutputBridge.AmmoProduce = agg.ammoProduce; // 전투 HUD 저장고/탄약 표시(§C-2)

            if (!agg.hasCore)
            {
                LogisticsOutputBridge.Result = default; // 물류 허브(코어) 없음 → 전투로 나가는 출력 없음
                LogisticsOutputBridge.GlobalCause = ConstraintCause.None;
                board.ClearDiagnostics();
                _roll.Reset();
                return;
            }

            // 출력은 **보드에 놓인 군수 노드 수**에서 나온다(260824_V02 §1).
            // 종전에는 `무기 스펙 합(145) × clamp01(총 탄약생산 ÷ 소비상한)`이었는데,
            // 그 모델은 탄종 구분이 없어 "관통 노드를 늘렸는지 폭발 노드를 늘렸는지"가 출력에
            // 반영되지 않는다. 소비 상한(capA)은 생산이 아니라 소비 축이므로 여기서 쓰지 않는다.
            BuildMunitionsLines(agg);
            float baseEff = AmmoLineProduction.TotalOutput(_muniLines, PerNodeRate);

            float heatThreshold = config != null ? config.heatThreshold : 12f;
            float beltCapacity = config != null ? config.beltCapacity : 14f;

            LogisticsResult r = LogisticsSimulation.Compute(
                baseEff,
                agg.powerSupply, agg.powerDraw,
                agg.heatGenerate, agg.heatDissipate, heatThreshold,
                beltCapacity, agg.ammoProduce, // 운송 필요 proxy = 탄약 생산량
                origin);

            // 크기값(예상·실제·갭 분해)은 전부 같은 창으로 굴린다 → 분해합 == 총갭이 유지된다.
            // 배율·플래그(powerEfficiency/heatThrottle/beltThrottle/multiple)는 즉시값 그대로 —
            // 비율을 평균내면 의미가 흐려지고, 원인 배지·경고는 지금 상태를 가리켜야 한다.
            _sample[ChExpected] = r.expected;
            _sample[ChActual] = r.actual;
            _sample[ChGapPower] = r.gapPower;
            _sample[ChGapHeat] = r.gapHeat;
            _sample[ChGapBelt] = r.gapBelt;
            _roll.TrySample(Time.time, _sample);

            LogisticsResult pub = r;
            pub.expected = _roll.Average(ChExpected);
            pub.actual = _roll.Average(ChActual);
            pub.gapPower = _roll.Average(ChGapPower);
            pub.gapHeat = _roll.Average(ChGapHeat);
            pub.gapBelt = _roll.Average(ChGapBelt);
            pub.gap = pub.expected - pub.actual; // Max(0,…)로 덮지 않는다 — 음수가 나오면 그건 버그 신호다

            LogisticsOutputBridge.Result = pub;
            LogisticsOutputBridge.GlobalCause = GlobalCause(r);

            board.ApplyDiagnostics(LogisticsDiagnostics.Evaluate(grid, r)); // 노드 상태색
        }

        /// <summary>전역 원인(변수패널 아이콘·점멸): Power → Heat 우선(§3-4-1). 벨트는 아이콘 아님(gapBelt 담당).</summary>
        private static ConstraintCause GlobalCause(LogisticsResult r)
        {
            if (r.powerEfficiency < 1f) return ConstraintCause.Power;
            if (r.heatThrottle < 1f) return ConstraintCause.Heat;
            return ConstraintCause.None;
        }

    }
}
