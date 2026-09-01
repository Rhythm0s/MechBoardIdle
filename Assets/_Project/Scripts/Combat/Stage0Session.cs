using MBI.Core;
using MBI.Data;
using MBI.UI;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 스테이지 0 — **전투가 없는 첫 스테이지**(260901_V05 §3층 확정).
    ///
    /// 목표는 「벨트를 이으면 물건이 만들어진다」 하나이고, 빈 칸에 노드를 놓았고
    /// 마운트가 가득 차면(약 8초) 스테이지 1로 넘어간다.
    ///
    /// ⚠️ **얹는 방식으로 지었다**(9월 4일 되돌림 지점). 기존 코드를 고치지 않고
    /// 이 컴포넌트가 위에 얹혀서 전투를 억제하고 목표만 본다. 되돌릴 때는 씬 생성기에서
    /// 이 컴포넌트를 붙이는 줄만 지우면 되고, <see cref="StageRunner"/>는 손대지 않는다.
    ///
    /// **전투를 어떻게 없애는가**: 적을 안 만드는 것이 아니라 <c>Endless</c>로 두고
    /// 스폰을 막는다. 시뮬 자체는 돌아야 마운트가 채워지기 때문이다 —
    /// 「이어지면 쌓인다」를 보여 주는 것이 이 스테이지의 내용물이다.
    /// </summary>
    // ⚠️ **StageRunner보다 먼저 돈다.** 뒤에 두었더니 첫 프레임에 이미 승리 판정이 났다 —
    // 적이 0기라 StageRunner.Update의 Evaluate가 곧바로 Win을 내고, 자동 전투가 다음 판을 걸어
    // 화면에 스테이지 2가 떴다(2026-09-01 브라우저 실측). 억제는 판정보다 앞서야 한다.
    [DefaultExecutionOrder(-50)]
    public sealed class Stage0Session : MonoBehaviour
    {
        [Tooltip("전투 러너. 스테이지 0 동안 적 스폰만 막고 시뮬은 그대로 돌린다.")]
        public StageRunner runner;

        [Tooltip("완료 시 넘어갈 스테이지. 비우면 넘어가지 않고 목표만 판정한다.")]
        public StageDefinition nextStage;

        private readonly Stage0Goal _goal = new Stage0Goal();
        private bool _finished;

        /// <summary>진행 상황(진단·테스트용).</summary>
        public Stage0Goal Goal => _goal;

        private void Awake()
        {
            TutorialSignals.Reset();
            TutorialSignals.GhostCell = StartingBoard.EmptySlot;
            TutorialSignals.HighlightBoardButton = true;
        }

        private void OnDisable() => TutorialSignals.Reset();

        private void Update()
        {
            if (_finished || runner == null) return;

            CombatSimulation sim = runner.Sim;
            if (sim == null) return;

            // 시간이 다 되어 지는 일이 없게 한다.
            // ⚠️ **적이 안 나오는 것은 Endless가 아니라 스테이지 데이터가 한다** — Endless는
            // 승패 판정만 막고 스폰은 그대로 돈다(2026-09-01 브라우저 실측에서 적 40기가 나왔다).
            // 스테이지 0 자산의 몬스터 구성이 비어 있는 것이 전투가 없는 진짜 이유다.
            sim.Endless = true;

            // 관찰은 **직전 프레임의 결과**를 본다. 억제보다 한 틱 늦지만, 8초짜리 목표라 무해하다.
            _goal.Observe(TutorialSignals.GhostCellFilled,
                sim.ActiveMount != null && sim.ActiveMount.IsFull);

            if (_goal.IsComplete) Finish();
        }

        private void Finish()
        {
            _finished = true;
            TutorialSignals.Reset(); // 고스트와 강조를 끈다

            // ⚠️ **억제를 반드시 푼다.** 이 컴포넌트는 곧 꺼지므로 여기서 안 풀면 Endless가
            // 그대로 남아 다음 스테이지가 영영 안 끝난다.
            if (nextStage != null) runner.LoadStage(nextStage); // 새 시뮬이라 Endless는 기본값
            if (runner.Sim != null) runner.Sim.Endless = false;

            enabled = false;
        }

        // ---- 최소 표시 ----

        /// <summary>
        /// 진행 표시. **안내 문구는 넣지 않는다**(구현 범위 확정) — 무엇을 했고 무엇이 남았는지만
        /// 두 줄로 보여 준다. 어디에 놓을지는 보드의 고스트가 말한다.
        /// </summary>
        private void OnGUI()
        {
            if (_finished) return;
            KoreanFont.Apply();

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };

            GUILayout.BeginArea(new Rect(12, Screen.height - 96f, 420f, 84f));
            GUILayout.Label(Mark(_goal.NodePlaced) + " 빈 칸에 노드를 놓는다", style);
            GUILayout.Label(Mark(_goal.MountFilled) + " 마운트가 가득 찬다", style);
            GUILayout.EndArea();
        }

        private static string Mark(bool done) => done ? "[v]" : "[  ]";
    }
}
