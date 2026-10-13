using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 🗑️🗑️ **자동 전투 진행 — 거동 폐기**(2026-10-13 · #114 · 빌드 실측).
    ///
    /// ⚠️⚠️ **이 요약은 2026-10-13 까지 「전투가 끝나면 잠시 뒤 스스로 다음 판을 건다」였다.**
    /// 지금은 **아무 판도 안 건다.** 이층 구조가 서면서 끝나는 자리마다 임자가 생겼다:
    ///   · 도전이 끝나면 → **결과 화면의 단추 셋**(#114 · <c>StageRunner</c>)
    ///   · 파밍에서 부서지면 → **한 판 뒤로**(#113 · <c>StageRunner.FallBackOneStage</c>)
    /// 여기서 또 판을 걸면 **두 손이 같은 판을 민다** — 실제로 그렇게 밀어서
    /// S5 파밍에서 부서진 로봇이 S4 가 아니라 **S1 로** 갔다(실측).
    ///
    /// 📌 **「사람이 안 눌러도 돈다」(§5-7)는 이제 파밍 층이 맡는다** — 그쪽은 끝이 없다.
    ///
    /// ⚠️ **지우지 않고 멈춰 둔다.** <see cref="CurrentIndex"/>·<see cref="MaxClearedIndex"/>
    /// 를 보는 곳이 남아 있고, 자동 진행을 되살릴지는 설계 몫이다.
    /// <see cref="StageProgression"/> 도 같은 처지다 — 산 호출자가 여기 하나뿐이고
    /// 그 하나가 안 돈다.
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
        [Tooltip("🗑️ 거동 폐기(2026-10-13 · #114) — 켜 두어도 판을 안 건다. 클래스 주석 참조.")]
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

            // ⚠️⚠️ **도전 층의 끝은 사람이 받는다**(2026-10-13 **사용자 확정 #114**).
            //
            // 📌 **확정 문안** — 「방치 = 파밍 층 · 도전 끝 = 단추 셋」.
            //    아래 판단은 구현이 먼저 내고 **사용자가 그대로 확정했다**(같은 날 저녁).
            //
            // 🗑️ **자동 진행 폐기 — 도전에 한해서.** 종전에는 전투가 끝나면 1.5 초 뒤
            //    스스로 다음 판을 걸었고, 그래서 #111 이 정한 결과 화면
            //    (「다시 도전」·「스테이지로」·「다음 스테이지로」)이 **아무도 못 보는
            //    화면**이었다 — 빌드로 S1 도전을 끝까지 돌려 보고 잡았다(2026-10-13).
            //    통과하자마자 S2 파밍으로 넘어가 버렸다.
            //
            // 📌 **「사람이 안 눌러도 돈다」는 이제 파밍 층이 맡는다**(§5-7 의 본뜻 · #114).
            //    파밍은 끝나지 않으므로 여기 올 일이 없고, 도전은 사람이 「도전」을 눌러
            //    들어가는 곳이다 — 그 결과도 사람이 받는 것이 짝이 맞는다.
            //    자동으로 넘기면 **「도전」 단추 자체가 뜻을 잃는다**(안 눌러도 넘어가니까).
            //
            // ⚠️ 통과 보고(해금·보상)는 **러너로 옮겼다**(`ReportChallengeClearOnce`) —
            //    이 길이 안 돌아도 판이 열려야 하기 때문이다.
            //
            // 🗑️🗑️ **자동 진행은 이제 어느 층에서도 안 돈다**(2026-10-13 빌드 실측).
            //
            // ⚠️⚠️ 종전 가드 `if (!StageLayerSignals.IsFarming) return;` 는 **거꾸로였다.**
            //    「파밍은 끝나지 않으니 여기 올 일이 없다」고 적어 두었는데 **틀렸다** —
            //    파밍에서도 **로봇이 부서지면** 시뮬이 `LoseDead` 를 낸다
            //    (`Endless` 는 전멸·제한시간만 멈춘다). 그래서 파밍 패배가 이 길로 새어
            //    `LoadStage(stages[CurrentIndex])` 가 돌았고, `CurrentIndex` 는 설정
            //    바로가기로 건너뛴 판을 모르므로 **S5 에서 부서졌는데 S1 으로** 갔다
            //    (실측). 러너의 `FallBackOneStage`(S4 로 가야 한다)와 정면으로 싸운 것이다.
            //
            // 📌 **끝나는 자리는 전부 임자가 있다.**
            //    · 도전이 끝나면 → **결과 화면의 단추 셋**(#114)
            //    · 파밍에서 부서지면 → **한 판 뒤로**(#113 · `FallBackOneStage`)
            //    둘 다 러너가 쥔다. 여기서 또 판을 걸면 **두 손이 같은 판을 민다.**
            //
            // ⚠️ 컴포넌트를 지우지 않는 까닭 — `CurrentIndex`·`MaxClearedIndex` 를 보는
            //    곳이 아직 있고, 자동 진행을 되살릴 판단은 설계 몫이다. 거동만 멈춘다.
            if (Owns(stages, runner.CurrentStage)) { _restartAt = -1f; return; }

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
