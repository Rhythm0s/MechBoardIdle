using MBI.Core;
using MBI.Data;
using MBI.UI;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 스테이지 0 — **전투가 없는 첫 스테이지**(260901_V05 §3층 확정).
    ///
    /// 목표는 「벨트를 이으면 물건이 만들어진다」 하나이고, **빈 칸을 벨트로 채우고**
    /// 마운트가 가득 차면 스테이지 1로 넘어간다.
    ///
    /// ⚠️ **「노드를 놓았고」는 낡았다**(2026-09-11 설계 확정 (가)) — 빈 칸이 합류 뒤
    /// 운반로의 **벨트 한 칸**으로 옮겨 갔다. 그 앞에는 병합기였고 그 앞에는 기초 군수였다.
    /// 세 판 모두 **「놓기 전 0」이 서는 자리**를 찾아온 것이고, 수업은 내내 연결성 하나였다.
    /// ⚠️ **「약 8초」도 낡았다** — 하네스 실측 **16초**(첫 도착 5.5 + 40÷4). 튜토리얼
    /// 기획서 4-1 「소요」는 설계가 다시 적는다(`260911_V01` 1-3).
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
        [Tooltip("전투 러너. 튜토리얼 동안 적 스폰만 막고 시뮬은 그대로 돌린다.")]
        public StageRunner runner;

        [Tooltip("완료 시 넘어갈 스테이지. 비우면 넘어가지 않고 목표만 판정한다.")]
        public StageDefinition nextStage;

        private readonly Stage0Goal _goal = new Stage0Goal();
        private bool _finished;

        /// <summary>진행 상황(진단·테스트용).</summary>
        public Stage0Goal Goal => _goal;

        private void Awake()
        {
            // **이미 끝냈으면 열지 않는다**(260902_W08 §2-2). 클리어 여부는 저장에 남으므로
            // 껐다 켜도 두 번 하지 않는다 — 반대로 미클리어면 켤 때마다 여기서 시작한다.
            if (IdleSignals.TutorialCleared)
            {
                SkipToNextStage();
                return;
            }

            Arm();
        }

        /// <summary>
        /// 고스트와 강조를 건다. **Awake와 복귀가 함께 쓴다** —
        /// 복귀가 Awake를 다시 부르면 클리어 검사에 걸려 그 자리에서 되튕긴다(끝낸 뒤라 참이므로).
        /// </summary>
        private void Arm()
        {
            TutorialSignals.Reset();
            TutorialSignals.GhostCell = StartingBoard.EmptySlot;
            TutorialSignals.HighlightBoardButton = true;

            // ⚠️ **병합기 강조를 걷었다**(2026-09-11 설계 확정 (가) · 실측으로 잡은 자리).
            //
            // 「채우는 것이 병합기라 팔레트에서 고른다」는 **두 판 전** 이야기다. 지금 채우는
            // 것은 **벨트**이고 직선 벨트는 팔레트에 버튼이 없다 — 드래그가 만든다.
            // 게다가 놓기 국면은 **팔레트를 전부 잠그므로**, 이 신호를 켜 두면
            // **잠긴 채 노랗게 깜빡이는 버튼**이 된다(「눌러라」와 「못 누른다」를 동시에 말한다).
            //
            // 그리고 **조립 모드로 바꿔야 깔 수 있다**(T-7). 기본 모드가 이동이라
            // 이 강조가 없으면 보드를 끌어도 벨트가 안 깔린다.
            TutorialSignals.HighlightBuildMode = true;
        }

        private void OnDisable() => TutorialSignals.Reset();

        /// <summary>
        /// 튜토리얼을 건너뛴다. **러너가 시작하기 전에** 스테이지를 갈아 끼워야 한다 —
        /// 이 컴포넌트가 러너보다 먼저 도는(-50) 이유가 여기에도 걸린다.
        /// 나중에 끄면 러너는 이미 스테이지 0을 열어 버린 뒤다.
        /// </summary>
        private void SkipToNextStage()
        {
            _finished = true;
            if (runner != null && nextStage != null) runner.stage = nextStage;
            enabled = false;
        }

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

            // **끝냈다는 사실을 저장에 남긴다**(260902_W08 §2-2). 보상은 없다 —
            // 스테이지가 아니므로 요구치·보상·파밍 규칙의 대상이 아니다(§2-1).
            IdleSignals.ReportClear(IdleSignals.TutorialId, 0f);
            IdleSignals.TutorialCleared = true;

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
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;
            if (_finished) return;
            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };

            GUILayout.BeginArea(new Rect(12, Screen.height - 96f, 420f, 84f));
            // ⚠️ **이 문구는 벨트로 바뀐 뒤에도 맞는다** — 「끊긴 자리를 잇는다」는 노드보다
            // 벨트일 때 오히려 더 정확하다. 안내 문구를 따로 두지 않는 것도 그대로다.
            GUILayout.Label(Mark(_goal.SlotFilled) + " 끊긴 자리를 잇는다", style);
            GUILayout.Label(Mark(_goal.MountFilled) + " 마운트가 가득 찬다", style);
            GUILayout.EndArea();
        }

        private static string Mark(bool done) => done ? "[v]" : "[  ]";

        /// <summary>
        /// 튜토리얼로 다시 들어간다 — **개발 빌드 전용**(260902_W08 §2-2).
        /// 촬영과 리허설이 A구간을 여러 번 찍어야 하는데, 복귀 경로가 없으면
        /// 첫 테이크가 곧 최종본이 된다.
        ///
        /// ⚠️ **보드는 되돌리지 않는다.** 이 메서드가 되돌리는 것은 목표와 신호뿐이라,
        /// **이미 깐 벨트**는 그대로 남는다 — 그러면 「끊긴 자리」가 처음부터 이어져 있다.
        /// A구간을 온전히 다시 찍으려면 보드에서 그 칸을 직접 비워야 한다.
        /// </summary>
        public void Reenter()
        {
            _goal.Reset();
            _finished = false;
            enabled = true;
            Arm(); // 고스트·강조를 다시 건다
        }
    }
}
