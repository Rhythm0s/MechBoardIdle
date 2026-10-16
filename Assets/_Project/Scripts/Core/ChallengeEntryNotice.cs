namespace MBI.Core
{
    /// <summary>
    /// **도전 들어가기 전 안내** (2026-10-16 사용자 직접 지시 ③ 「도전 들어가기 전 해당 내용 안내할 것」).
    ///
    /// 해당 내용 = 지시 ② 「도전 직전 로봇 A·B 의 현재 물류량(마운트에 적재한 탄환·드론)은 유지한 채 들어간다」.
    /// 마운트는 러너가 들고 있어 판을 다시 세워도 그대로 넘어간다(StageRunner `MountA`·`MountB`) —
    /// 이 창은 그 사실과 지금 실린 양을 **들어가기 전에** 보여 주고, 「도전 시작」을 눌러야 들어간다.
    ///
    /// ⚠️ 글자는 구현 가정이다(사용자가 바꿀 수 있다). 숫자는 마운트에서 읽는다 — 여기서 짓지 않는다.
    /// </summary>
    public static class ChallengeEntryNotice
    {
        public const string Title = "도전 시작";
        public const string Body = "지금 마운트에 실린 물류를 그대로 가지고 도전합니다";
        public const string Confirm = "도전 시작";
        public const string Cancel = "취소";

        /// <summary>로봇 A 줄 — 마운트에 실린 탄환 / 마운트 전 슬롯 기준 상한.</summary>
        public static string LineA(float rounds, float capacity) =>
            $"로봇 A 마운트 탄환 {Floor(rounds)}/{Floor(capacity)}";

        /// <summary>로봇 B 줄 — 마운트에 실린 드론(누적형 + 광역형) / 상한. B 가 해금 전이면 안 띄운다.</summary>
        public static string LineB(float drones, float capacity) =>
            $"로봇 B 마운트 드론 {Floor(drones)}/{Floor(capacity)}";

        /// <summary>합체 게이지 줄 — 이어 가는 게이지(사용자 확정 #155). 글자는 가정. 0~1 비율을 받는다.</summary>
        public static string LineMerge(float ratio) =>
            $"합체 게이지 {Floor(System.Math.Max(0f, System.Math.Min(1f, ratio)) * 100f)}%";

        /// <summary>
        /// 화면 숫자는 **내림**이다 — 39.6 을 40/40 으로 올려 적으면 「가득」으로 읽힌다
        /// (마일스톤 목표 줄 `MountProgressText` 와 같은 결).
        /// </summary>
        private static int Floor(float v) => v <= 0f ? 0 : (int)System.Math.Floor(v + 1e-4f);
    }
}
