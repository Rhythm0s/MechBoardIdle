namespace MBI.Core
{
    /// <summary>
    /// **스테이지 여섯의 맵 이름** (2026-09-30 사용자 확정 ⑦ — 상단에 늘 보이는 진행 표시).
    ///
    /// ⚠️⚠️ **문안 여섯이 전부 가정이다.** 설계가 준 것은 「몬스터·지형 테마에 맞춰
    /// 구현이 임의로 적고, 문안은 설계가 나중에 고친다」는 지시뿐이다. 그래서 여기 이름은
    /// **자리를 세우기 위한 것**이고 확정된 말이 아니다.
    ///
    /// 📌 **무엇을 보고 지었는가** — 스테이지 기획서 3장의 학습 주제와 몬스터 구성 방향이다.
    /// 지어낸 세계관을 덧붙이지 않고, 문서가 이미 적고 있는 성격만 이름으로 옮겼다:
    ///   · S1 전투를 처음 본다 · 체력 낮은 다수 · 느린 등장
    ///   · S2 모듈 배치 · 체력 조금 상승
    ///   · S3 물류 다시 짜기 · 점점 빨리 나타남(지속 화력 요구)
    ///   · S4 마운트 강화 벽 · 체력 급증
    ///   · S5 태그 학습 · 강력한 소수
    ///   · S6 합체·버스트 · 일반 무리 + 강적 1기
    ///
    /// ⚠️ **번호는 이름이 아니다** — 화면에는 「Stage k/6」이 따로 붙으므로 여기에는
    /// 숫자를 안 넣는다. 넣으면 같은 수가 두 곳에 산다(지침 §7).
    ///
    /// ⚠️ 튜토리얼 전용 스테이지는 **여섯에 안 든다**(스테이지 기획서 3장 — 요구치·보상·
    /// 파밍·오프라인 넷을 다 안 갖는다). 그래서 진행 띠도 안 뜬다.
    /// </summary>
    public static class StageMapName
    {
        /// <summary>일반 스테이지의 수. 「k / 6」의 분모다.</summary>
        public const int TotalStages = 6;

        /// <summary>⚠️ 가정 문안 여섯 — 설계가 고칠 자리다.</summary>
        private static readonly string[] Names =
        {
            "폐자재 야적장",   // S1 — 체력 낮은 다수가 느리게 걸어 나온다
            "조립 구역",       // S2 — 모듈을 배우는 판
            "수송 회랑",       // S3 — 점점 빨리 밀려든다 · 물류를 다시 짜는 무대
            "격벽 앞",         // S4 — 체력 급증 · 강화 없이는 못 넘는 벽
            "폐기 처리장",     // S5 — 강력한 소수
            "중앙 제어동",     // S6 — 강적 1기 · MVP 종점
        };

        /// <summary>
        /// 스테이지 번호(1부터)로 맵 이름을 낸다. 범위 밖이면 **빈 글자**다.
        ///
        /// ⚠️ **빈 글자를 지어내 채우지 않는다** — 모르는 판은 이름 없이 번호만 뜬다.
        /// 여기서 「?」 같은 것을 넣으면 화면이 아는 척을 하게 된다.
        /// </summary>
        public static string Of(int stageNumber)
            => stageNumber >= 1 && stageNumber <= Names.Length
                ? Names[stageNumber - 1]
                : string.Empty;

        /// <summary>
        /// 스테이지 자산 이름(<c>S1</c>~<c>S6</c>)에서 번호를 뽑는다. 못 읽으면 **0**이다.
        ///
        /// ⚠️ **튜토리얼 전용 스테이지도 0 이다** — 그 판은 여섯에 안 들기 때문이고,
        /// 0 을 받은 쪽은 진행 띠를 안 그린다.
        /// </summary>
        public static int NumberOf(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return 0;
            if (stageId.Length < 2) return 0;
            if (stageId[0] != 'S' && stageId[0] != 's') return 0;

            int n = 0;
            for (int i = 1; i < stageId.Length; i++)
            {
                char c = stageId[i];
                if (c < '0' || c > '9') return 0;   // 「S1a」 같은 것은 안 읽는다
                n = n * 10 + (c - '0');
            }
            return n >= 1 && n <= TotalStages ? n : 0;
        }
    }
}
