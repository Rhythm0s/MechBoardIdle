using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 자동 전투 진행(§5-7). 전투가 끝나면 잠시 뒤 스스로 다음 판을 건다 —
    /// 사람이 "다시" 버튼을 눌러야 이어지면 방치형이 아니다.
    ///
    /// 판단은 전부 <see cref="StageProgression"/>(순수)이 하고 여기서는 시간과 씬만 붙인다.
    /// 진행 게이트는 **실제 클리어 성공/실패**이지 요구치 수치가 아니다 — 패배하면 그 자리에서
    /// 반복하므로 S4 강화-only 벽에서 무한 자살 루프 대신 파밍이 돈다.
    ///
    /// 이동(카이팅)은 StageRunner가 맡는다. 여기는 스테이지 진행만 본다 —
    /// 한 파일 = 한 책임(§3).
    /// </summary>
    public sealed class AutoBattleController : MonoBehaviour
    {
        [Tooltip("전투 러너. 씬 생성기가 주입.")]
        public StageRunner runner;
        [Tooltip("진행 순서대로의 스테이지 목록(S1~S6). 씬 생성기가 주입.")]
        public List<StageDefinition> stages = new List<StageDefinition>();
        [Tooltip("자동 재시작 대기 등 TBD 튜닝.")]
        public CombatTuning tuning;
        [Tooltip("자동 진행 사용. 끄면 수동으로 '다시'를 눌러야 한다.")]
        public bool autoAdvance = true;

        /// <summary>지금 몇 번째 스테이지인가(stages 인덱스).</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>지금까지 깬 최고 인덱스. 아직 없으면 -1.</summary>
        public int MaxClearedIndex { get; private set; } = -1;

        private float _restartAt = -1f;

        /// <summary>
        /// 이 판이 **내 목록의 것인가.** 아니면 남이 몰고 있는 판이므로 손을 뗀다.
        ///
        /// ⚠️ 튜토리얼(S0)은 이 목록에 없다 — 그래서 목록에 없다는 것은 곧
        /// **지금 러너를 튜토리얼이 몰고 있다**는 뜻이다.
        /// </summary>
        public static bool Owns(List<StageDefinition> stages, StageDefinition current) =>
            stages != null && current != null && stages.IndexOf(current) >= 0;

        private void Start()
        {
            // 러너에 이미 꽂혀 있는 스테이지를 현재 위치로 삼는다.
            //
            // ⚠️⚠️ **씬은 S1 이 아니라 튜토리얼(S0)로 시작한다** — 그리고 S0 은 이 목록에
            // **없다**(목록은 S1~Sn). 그래서 여기서 idx 가 -1 이 되는데, 종전에는 그것을
            // 그냥 무시했고 `CurrentIndex` 는 기본값 **0(=S1)** 으로 남았다.
            // 「못 찾았다」와 「첫 번째다」가 같은 값이 된 것이다.
            if (runner != null && runner.CurrentStage != null)
            {
                int idx = stages.IndexOf(runner.CurrentStage);
                if (idx >= 0) CurrentIndex = idx;
            }
        }

        private void Update()
        {
            if (!autoAdvance || runner == null || tuning == null || stages.Count == 0) return;

            // ⚠️⚠️ **내 것이 아닌 판은 안 건드린다**(2026-09-15 오후 · 육안 ③④).
            //
            // 증상 — S2 전투 화면에 **튜토리얼 체크리스트가 같이 떠 있고**, 73 초가 지나도
            // 창고 0/40 · 저장고 0.0 발/초였다.
            //
            // 뿌리는 **`runner.stage` 의 주인이 둘**이라는 것이다. 씬은 러너에 튜토리얼(S0)을
            // 꽂고 `Stage0Session` 이 그것을 몰고 간다. 그런데 이쪽은 S0 이 끝나는 것을 보고
            // (S0 은 전투이기도 하다) **제 목록의 다음 판을 러너에 덮어썼다** —
            // 튜토리얼 밑에서 판이 바뀐다. 그러면
            // · 체크리스트는 S0 을 기다린 채 S2 화면 위에 남고(③),
            // · 튜토리얼이 영영 안 끝나니 `TutorialCleared` 가 안 서고,
            // · 그래서 비워 둔 칸 (6,5) 가 안 채워져 **운반로가 끊긴 채**라 생산이 0 이다(④).
            //
            // 📌 고침은 **소유를 명시하는 것** 하나다 — 러너가 지금 들고 있는 판이 내 목록에
            // 없으면 남의 판이므로 손을 뗀다. 튜토리얼이 끝나면서 S1 을 걸어 주면 그때부터
            // 다시 내 것이 된다(`Stage0Session` 이 `nextStage` 로 그렇게 한다).
            if (!Owns(stages, runner.CurrentStage)) return;

            CombatResult result = runner.CurrentResult;
            if (result == CombatResult.InProgress)
            {
                _restartAt = -1f; // 진행 중이면 대기 타이머를 접는다
                return;
            }

            // 종료 직후 한 박자 쉬고 넘어간다(결과를 볼 시간).
            if (_restartAt < 0f)
            {
                _restartAt = Time.time + tuning.autoRestartDelayTbd;
                return;
            }
            if (Time.time < _restartAt) return;

            var input = new ProgressionInput(CurrentIndex, MaxClearedIndex, stages.Count, result);
            ProgressionDecision d = StageProgression.Decide(input);

            if (d.isFirstClear)
            {
                MaxClearedIndex = CurrentIndex;
                // 최초 클리어 보상(강화재료)은 방치 런타임이 지급한다 — 재화는 여기서 만지지 않는다.
                IdleSignals.ReportClear(CurrentStageId(), CurrentStage()?.enhMaterialReward ?? 0f);
            }

            CurrentIndex = d.nextIndex;
            _restartAt = -1f;
            runner.LoadStage(stages[CurrentIndex]);
        }

        private StageDefinition CurrentStage() =>
            CurrentIndex >= 0 && CurrentIndex < stages.Count ? stages[CurrentIndex] : null;

        private string CurrentStageId()
        {
            StageDefinition s = CurrentStage();
            return s != null ? s.stageId : string.Empty;
        }
    }
}
