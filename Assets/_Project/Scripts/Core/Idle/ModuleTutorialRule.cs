using System;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **T4 — S2 「모듈 배치」 튜토리얼** (튜토리얼 기획서 4장 T4 · 2026-10-15 사용자 확정 #138).
    ///
    /// <see cref="ChallengeTutorialRule"/>(T3)와 **같은 꼴**이다 — 장부를 새로 만들지 않고,
    /// 매 프레임 다시 판단하고, 강제 버튼은 대신 눌러 주지 않는다.
    ///
    /// ⚠️⚠️ **새 장부 0**(지침 §7). 「T4 를 마쳤는가」는 저장에 따로 안 적는다 —
    ///    **대상 노드에 모듈이 붙어 있는가**(보드 저장이 이미 든다)와 **S2 통과**(<c>clearedStageIds</c>)가
    ///    그 답이다. 따로 적으면 둘이 어긋나는 날 「붙였는데 또 뜨는」 화면이 된다.
    ///
    /// ⚠️ **모듈은 지금처럼 무료·무제한이다**(사용자 확정 「지급 = 알림만」) — 재고·구매를 안 짓는다.
    /// </summary>
    public static class ModuleTutorialRule
    {
        /// <summary>T4 가 붙잡는 판 — S2 다(기획서 4장 「무대 = S2 모듈 배치」).</summary>
        public const int StageNumber = 2;

        /// <summary>지급 알림 — **사용자 확정 #138 · 구현 가정 아님.** 고치지 않는다.</summary>
        public const string GrantNotice = "모듈 M·R을 받았습니다";

        /// <summary>전투 화면 안내 — **사용자 확정 #138 · 구현 가정 아님.** 고치지 않는다.</summary>
        public const string CombatPrompt = "조립 버튼을 눌러 모듈을 붙여보세요";

        /// <summary>고스트 위 안내 — **사용자 확정 #138 · 구현 가정 아님.** 고치지 않는다.</summary>
        public const string GhostHint = "모듈 탭에서 M 또는 R을 고른 뒤 이 노드를 탭하세요";

        /// <summary>
        /// 지급 알림이 서 있는 시간(초). ⚠️ **가정 4초** — 사용자 확정이 「표시 시간은 구현 가정」이다
        /// (설계 역기입 자리). 막은 알림이 걷힌 뒤에 깔린다 — 둘이 겹치면 알림이 어둠에 묻힌다.
        /// </summary>
        public const float NoticeSeconds = 4f;

        /// <summary>
        /// **대상 노드 자리 — 앞에서부터 고른다** (⚠️ 가정 · 설계 역기입 자리).
        ///
        /// 📌 사용자 확정 「시작 보드에서 모듈이 출력에 가장 크게 드는 노드 하나 · 하네스로 재서」.
        ///    `ModuleTargetProbe`(2026-10-15) 실측: 시작 보드 노드 열넷에 M·R 을 하나씩 붙였더니
        ///    **탄약 생산이 느는 것은 표준탄 군수 둘 — (7,8)·(7,7) — 뿐**이었다(2.00 → 2.50 발/초 · 둘이 같다).
        ///    나머지 열둘은 0 이다. 같으면 **동 줄(코어 동면 · 벨트 0칸)** 인 (7,8) 을 먼저 둔다.
        /// ⚠️ 「출력」 숫자(40)는 군수 노드 **수**에서 나오는 셈이라 모듈로 안 변한다 — 변하는 것은 탄약 생산이다.
        ///
        /// ⚠️ 플레이어가 그 노드를 지웠으면 다음 자리로 간다. 둘 다 없으면 <see cref="PickTarget"/> 이
        ///    null 을 주고, 그때는 **아무 노드에나 붙이면 끝난다**(막다른 국면을 안 만든다).
        /// </summary>
        public static readonly Vector2Int[] PreferredTargets =
        {
            new Vector2Int(7, 8),
            new Vector2Int(7, 7),
        };

        /// <summary>
        /// 지금 T4 가 돌고 있는가. 다섯이 다 참이어야 한다 —
        ///   ① 튜토리얼 전용 스테이지를 마쳤다 ② **S1 을 깼다**(T3 가 끝났다)
        ///   ③ **S2 를 아직 못 깼다** ④ 지금 판이 **S2 · 파밍 층**이다
        ///   ⑤ **대상 노드에 아직 모듈이 없다**(끝 조건)
        /// </summary>
        public static bool IsActive(bool tutorialCleared, bool stage1Cleared, bool stage2Cleared,
                                    int stageNumber, bool isFarming, bool targetHasModule)
        {
            if (!tutorialCleared || !stage1Cleared) return false;
            if (stage2Cleared) return false;
            if (stageNumber != StageNumber || !isFarming) return false;
            return !targetHasModule;
        }

        /// <summary>
        /// 대상 노드를 고른다 — <see cref="PreferredTargets"/> 에서 **노드가 놓인 첫 자리**.
        /// 없으면 null(그때 끝 조건은 「판의 아무 노드에나 모듈이 붙었다」).
        /// </summary>
        public static Vector2Int? PickTarget(Func<Vector2Int, bool> hasNode)
        {
            if (hasNode == null) return null;
            foreach (Vector2Int c in PreferredTargets)
                if (hasNode(c)) return c;
            return null;
        }
    }

    /// <summary>
    /// **지급 알림을 언제 한 장 띄우는가** (2026-10-15 사용자 확정 #138 · 기획서 4-7 준용).
    ///
    /// 「S2 파밍 **첫 진입** 때 한 번만 · 저장 복원으로 다시 들어오면 안 뜸」 —
    /// 4-7 의 「**상태가 바뀌는 순간에만**」이다. T4 가 서 있는 **상태**만 보면 저장에서 돌아온 판도
    /// 처음부터 참이라 들어오자마자 뜬다(09-16 고스트 안내가 같은 병이었다 · `DrawGhostHint`).
    ///
    /// 📌 그래서 **이 세션에서 다른 판(1 이상 · 2 아님)을 본 뒤** T4 가 서는 것을 볼 때만 띄운다.
    ///    켜자마자 S2 면 다른 판을 본 적이 없으므로 안 뜬다. 판 번호 0(아직 안 실림·튜토리얼 판)은
    ///    「다른 판」으로 안 센다 — 켜는 중 한두 프레임이 0 이라 그것을 세면 복원 판에서도 뜬다.
    /// </summary>
    public sealed class ModuleGrantNotice
    {
        private bool _sawOtherStage;
        private bool _shown;
        private float _shownAt = -1f;

        /// <summary>한 프레임을 본다. 이번 프레임에 알림을 **띄웠으면** 참.</summary>
        public bool Observe(int stageNumber, bool t4Active, float now)
        {
            if (stageNumber >= 1 && stageNumber != ModuleTutorialRule.StageNumber) _sawOtherStage = true;
            if (_shown || !t4Active || !_sawOtherStage) return false;
            _shown = true;
            _shownAt = now;
            return true;
        }

        /// <summary>알림이 지금 서 있는가.</summary>
        public bool IsVisible(float now) =>
            _shownAt >= 0f && now - _shownAt <= ModuleTutorialRule.NoticeSeconds;
    }
}
