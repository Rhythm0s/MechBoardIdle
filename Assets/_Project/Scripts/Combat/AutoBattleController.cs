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

        private void Start()
        {
            // 러너에 이미 꽂혀 있는 스테이지를 현재 위치로 삼는다(씬에서 S1로 시작).
            if (runner != null && runner.CurrentStage != null)
            {
                int idx = stages.IndexOf(runner.CurrentStage);
                if (idx >= 0) CurrentIndex = idx;
            }
        }

        private void Update()
        {
            if (!autoAdvance || runner == null || tuning == null || stages.Count == 0) return;

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
