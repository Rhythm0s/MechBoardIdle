using MBI.Core;
using MBI.Data;
using System.Collections.Generic;
using UnityEngine;

namespace MBI.Logistics
{
    /// <summary>
    /// 라이브 물류 네트워크 → 출력 반영(§5-6, L4-R).
    ///
    /// **한 프레임의 순서가 이 파일의 요점이다** (2026-09-05 개정 · `260904_W04` 2-1 4번):
    /// <code>
    ///   집계 → 배율(전력·발열) → 아이템을 민다(배율을 먹여) → 마운트 도착을 센다 → 조립 → 게시
    /// </code>
    /// 배율이 생산 앞에 와야 도착량이 그 배율을 반영하고, 도착을 센 뒤에 조립해야
    /// **같은 프레임의 관측치**가 결과에 들어간다. 순서를 바꾸면 한 프레임씩 어긋나
    /// 배치를 바꾼 직후가 틀리게 나온다.
    ///
    /// ⚠️ **actual이 계산값에서 관측치로 바뀌었다.** 종전에는
    /// 「노드 수 × 라인 스펙 × 전력 × 발열 × 벨트」였고, 그러면 벨트를 어떻게 깔든 노드 수만
    /// 같으면 같은 수가 나왔다 — 최적화의 결과가 숫자에 안 보였다. 이제 마운트를 통과한 것만
    /// 센다(<see cref="MountDelivery"/>).
    ///
    /// ⚠️ 노드 cause는 여전히 근사(R1)다. 병목 수치는 전부 TBD(LogisticsConfig).
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

        /// <summary>
        /// 마운트 도착 관측기 — 이 컴포넌트가 게시하는 `actual`의 원천이다.
        ///
        /// 롤링 창과 같은 주기(0.1초)로 비율을 낸다. 더 짧게 내면 한 개 닿을 때마다 비율이
        /// 튀고, 더 길게 내면 롤링에 들어가는 샘플이 성겨진다.
        /// </summary>
        private readonly MountDelivery _delivery = new MountDelivery();
        private const float DeliverySampleSeconds = 0.1f;

        /// <summary>탄종별 발당피해를 무기 스펙에서 찾는다. 없으면 0 — 세지 않는다는 뜻이다.</summary>
        private float DamageOf(AmmoKind kind)
        {
            if (robot == null || robot.weapons == null) return 0f;
            for (int i = 0; i < robot.weapons.Count; i++)
                if (robot.weapons[i].kind == kind) return robot.weapons[i].damagePerShot;
            return 0f;
        }

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
            _delivery.Reset();
        }

        private void Update()
        {
            if (board == null || robot == null) return;
            BoardGrid grid = board.Grid;
            if (grid == null) return;

            // 100은 인스펙터 리터럴이 아니라 원천에서 온다(§3 수치 하드코딩 금지).
            // 브릿지는 물류 단위 = 마운트계수 미적용(마운트계수는 판정식 내부 항 = 전투 측).
            float origin = robot.balanceRef != null ? robot.balanceRef.origin : 100f;

            // **이어진 노드만 센다**(260829_V03 §판정① A안). 배선이 곧 출력이다 —
            // 종전에는 격자에 놓인 노드를 전부 세서 벨트를 전부 지워도 출력이 그대로였다.
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(grid);

            // 일감률(260831_V07 승인분) — 전력 수요가 이 값을 타고 변동비가 된다.
            // ⚠️ 부스터의 「추진제 스택이 차면 0」은 여기서 못 읽는다(스택은 전투가 쥔다) → 잠정 1.
            WorkloadRate.Result work = WorkloadRate.Compute(grid, connected, robot.balanceRef);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid, connected, work);
            LogisticsOutputBridge.Workload = work; // 보드가 「노는 중」을 그리는 근거
            LogisticsOutputBridge.AmmoProduce = agg.ammoProduce; // 전투 HUD 저장고/탄약 표시(§C-2)

            // 군수 노드가 탄약 말고 다른 조합표를 돌리면 산출이 이쪽으로 나간다.
            // ⚠️ 코어가 없어도 게시한다 — 이 셋은 전투 출력이 아니라 **보드가 만든 물건**이라,
            // 코어 유무로 막으면 「부스터를 놓았는데 회피 칸이 안 늘어난다」가 된다.
            LogisticsOutputBridge.DroneProduce = agg.droneProduce;
            LogisticsOutputBridge.PropellantProduce = agg.propellantProduce;
            LogisticsOutputBridge.BoosterCount = agg.boosterCount;

            if (!agg.hasCore)
            {
                LogisticsOutputBridge.Result = default; // 물류 허브(코어) 없음 → 전투로 나가는 출력 없음
                LogisticsOutputBridge.GlobalCause = ConstraintCause.None;
                board.ClearDiagnostics();
                _roll.Reset();
                _delivery.Reset(); // 모으던 구간을 버린다 — 코어가 돌아왔을 때 옛 도착이 섞이면 안 된다
                return;
            }

            // 출력은 **보드에 놓인 군수 노드 수**에서 나온다(260824_V02 §1).
            // 종전에는 `무기 스펙 합(145) × clamp01(총 탄약생산 ÷ 소비상한)`이었는데,
            // 그 모델은 탄종 구분이 없어 "관통 노드를 늘렸는지 폭발 노드를 늘렸는지"가 출력에
            // 반영되지 않는다. 소비 상한(capA)은 생산이 아니라 소비 축이므로 여기서 쓰지 않는다.
            BuildMunitionsLines(agg);
            float baseEff = AmmoLineProduction.TotalOutput(_muniLines, PerNodeRate);

            float heatThreshold = config != null ? config.heatThreshold : 12f;

            // ① 배율 먼저. 생산에 걸리는 것이라 아이템을 밀기 전에 나와야 한다.
            //    냉각은 노드 합이 아니라 모듈 F가 든다(260829_V03) — 모듈이 없으면 0이다.
            ProductionThrottle throttle = LogisticsSimulation.Throttles(
                agg.powerSupply, agg.powerDraw,
                agg.heatGenerate, config != null ? config.moduleCoolingTbd : 0f, heatThreshold);

            // ② 개별 아이템을 실제로 민다 (2026-09-04 배선 · `260904_W01` 6장).
            //
            // ⚠️ **9월 3일까지 이 호출이 없었다.** `BoardItemTick`도 `BeltItemFlow`도 만들어만
            // 두고 부르는 곳이 0건이라 테스트 안에서만 돌았다 — 불일치 3번과 같은 형태가
            // 한 층 위에서 반복된 것이다(`260904_V02`).
            //
            // **전력·발열을 여기서 곱한다.** 둘이 모자라면 노드가 덜 만들고, 덜 만들면 덜
            // 도착한다(`260903_W02` 2-2). 도착량을 출력으로 쓰는 이상 그 인과는 생산 단계에만
            // 있어야 하며, 조립에서 또 곱하면 제곱이 된다.
            BoardItemTick.Step(grid, board.ItemFlow, Time.deltaTime, throttle.Scale);

            // ③ 마운트에 닿은 것을 센다. 읽고 나면 비운다 — 안 비우면 같은 도착이 계속 세어진다.
            _delivery.Observe(board.ItemFlow.PendingMountArrivals, DamageOf, Time.deltaTime);
            board.ItemFlow.ClearPendingMountArrivals();
            _delivery.TryDrain(DeliverySampleSeconds, out float deliveredRate);

            // ④ 조립. actual은 계산이 아니라 위에서 잰 값이다.
            LogisticsResult r = LogisticsSimulation.Compute(baseEff, throttle, deliveredRate, origin);

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
            // Max(0,…)로 덮지 않는다. actual이 관측치가 된 뒤로는 음수가 **버그가 아니라
            // 버퍼가 비워지는 중**이라는 뜻일 수 있다 — 쌓여 있던 것이 한꺼번에 빠지면
            // 그 구간의 도착이 생산 능력보다 많다. 덮으면 분해 합이 총갭과 안 맞는다.
            pub.gap = pub.expected - pub.actual;

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
