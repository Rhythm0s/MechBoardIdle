using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 노드별 진단(§L4-R #6, 근사·R1) — 구조 원인(Blocked/NoInput)·전역 원인(Power)·색 산출률 검증.
    /// 순수 로직 + SO 인스턴스(씬 불필요).
    /// </summary>
    public sealed class LogisticsDiagnosticsTests
    {
        private const float Delta = 0.001f;

        // CreateInstance한 SO는 씬에 속하지 않아 자동 해제되지 않는다 → EditMode 누수 경고 방지.
        private readonly List<NodeDefinition> _created = new List<NodeDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (NodeDefinition def in _created)
                if (def != null) Object.DestroyImmediate(def);
            _created.Clear();
        }

        private NodeDefinition MakeNode(NodeType type, params NodePort[] ports)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = type;
            def.implemented = true;
            def.ports = new List<NodePort>(ports);
            _created.Add(def);
            return def;
        }

        private static ConstraintCause CauseAt(List<NodeDiagnostic> ds, Vector2Int cell)
        {
            foreach (NodeDiagnostic d in ds) if (d.cell == cell) return d.cause;
            return ConstraintCause.None;
        }

        private static float RateAt(List<NodeDiagnostic> ds, Vector2Int cell)
        {
            foreach (NodeDiagnostic d in ds) if (d.cell == cell) return d.actualRate;
            return -1f;
        }

        private static LogisticsResult NoBottleneck() =>
            LogisticsSimulation.Compute(145f, 80f, 66f, 8f, 0f, 12f, 14f, 10f, 100f);

        private static LogisticsResult PowerStarved() =>
            LogisticsSimulation.Compute(145f, 33f, 66f, 8f, 0f, 12f, 14f, 10f, 100f); // eff 0.5

        private static LogisticsResult HeatThrottled() =>
            LogisticsSimulation.Compute(145f, 80f, 66f, 24f, 0f, 12f, 14f, 10f, 100f); // 전력 정상 · 발열 0.5

        [Test]
        public void Diagnose_StructuralAndGlobalCauses()
        {
            var grid = new BoardGrid(4, 4, 1f, Vector2.zero);

            // Blocked: 출력 포트 East, 인접(1,0) 비어 있음 → 뒤로 안 빠짐.
            grid.TryPlace(new Vector2Int(0, 0),
                MakeNode(NodeType.Munitions, new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo)), out _);

            // NoInput: 입력 포트 West, 인접(2,3) 비어 있음 → 앞에서 안 옴.
            grid.TryPlace(new Vector2Int(3, 3),
                MakeNode(NodeType.Processing, new NodePort(PortFace.West, PortIO.Input, FlowKind.Material)), out _);

            // 연결쌍: (1,2) Output East ↔ (2,2) Input West → 서로 인접 존재 → 구조 정상.
            grid.TryPlace(new Vector2Int(1, 2),
                MakeNode(NodeType.Energy, new NodePort(PortFace.East, PortIO.Output, FlowKind.Power)), out _);
            grid.TryPlace(new Vector2Int(2, 2),
                MakeNode(NodeType.Core, new NodePort(PortFace.West, PortIO.Input, FlowKind.Power)), out _);

            // 병목 없음 → 연결쌍 None, 구조 문제만 표출.
            List<NodeDiagnostic> ok = LogisticsDiagnostics.Evaluate(grid, NoBottleneck());
            Assert.AreEqual(ConstraintCause.Blocked, CauseAt(ok, new Vector2Int(0, 0)));
            Assert.AreEqual(ConstraintCause.NoInput, CauseAt(ok, new Vector2Int(3, 3)));
            Assert.AreEqual(ConstraintCause.None, CauseAt(ok, new Vector2Int(1, 2)));
            Assert.AreEqual(ConstraintCause.None, CauseAt(ok, new Vector2Int(2, 2)));
            // 색: 구조 정지 = 0(빨강) / 정상 = 1(초록).
            Assert.AreEqual(0f, RateAt(ok, new Vector2Int(0, 0)), Delta);
            Assert.AreEqual(1f, RateAt(ok, new Vector2Int(1, 2)), Delta);

            // 전역 전력부족 → 연결된 노드는 Power, 산출률 0.5(노랑). 구조 정지 노드는 여전히 Blocked/NoInput.
            List<NodeDiagnostic> starved = LogisticsDiagnostics.Evaluate(grid, PowerStarved());
            Assert.AreEqual(ConstraintCause.Power, CauseAt(starved, new Vector2Int(1, 2)));
            Assert.AreEqual(ConstraintCause.Power, CauseAt(starved, new Vector2Int(2, 2)));
            Assert.AreEqual(0.5f, RateAt(starved, new Vector2Int(1, 2)), Delta);
            Assert.AreEqual(ConstraintCause.Blocked, CauseAt(starved, new Vector2Int(0, 0)));
        }

        /// <summary>
        /// **발열 축 폐기를 테스트로 고정한다** (2026-09-02 · 260902_W15).
        ///
        /// 구 테스트는 「전력이 멀쩡하면 발열이 원인으로 표출된다」를 검증했다. 발열이 폐기되어
        /// 그 기대를 **뒤집는다** — 감쇠가 걸려 있어도 원인은 나오지 않아야 한다.
        ///
        /// 테스트를 지우지 않고 뒤집는 이유: 지우면 발열 분기가 되살아나도 아무 데서도 안 걸린다.
        /// 산출률(0.5)은 그대로 두는데, 그것은 계산부의 값이고 폐기 대상은 **화면에 나오는 원인**이다.
        /// </summary>
        [Test]
        public void Diagnose_HeatNeverSurfaces_AfterAxisRetired()
        {
            var grid = new BoardGrid(4, 4, 1f, Vector2.zero);
            grid.TryPlace(new Vector2Int(1, 2),
                MakeNode(NodeType.Energy, new NodePort(PortFace.East, PortIO.Output, FlowKind.Power)), out _);
            grid.TryPlace(new Vector2Int(2, 2),
                MakeNode(NodeType.Core, new NodePort(PortFace.West, PortIO.Input, FlowKind.Power)), out _);

            LogisticsResult r = HeatThrottled();
            Assert.AreEqual(1f, r.powerEfficiency, Delta, "전력이 정상이어야 발열만 남은 상태가 된다");
            Assert.Less(r.heatThrottle, 1f, "감쇠 자체는 계산부에 남아 있다 — 표출만 폐기됐다");

            List<NodeDiagnostic> ds = LogisticsDiagnostics.Evaluate(grid, r);
            Assert.AreEqual(ConstraintCause.None, CauseAt(ds, new Vector2Int(1, 2)));
            Assert.AreEqual(ConstraintCause.None, CauseAt(ds, new Vector2Int(2, 2)));
            Assert.AreEqual(0.5f, RateAt(ds, new Vector2Int(2, 2)), Delta); // 산출률은 그대로
        }
    }
}
