namespace MBI.Core
{
    /// <summary>
    /// 보드 좌표 덤프 요청 (2026-09-11 신설 · 플랜 §71-14 ② · **개발 빌드 전용 진단 도구**).
    ///
    /// **왜 필요한가.** 리허설 결함 ①(물류가 마운트에 안 닿는다)은 **사용자 화면에서만** 나고
    /// 하네스에서는 안 난다. 그래서 「사용자가 놓은 배치」를 하네스에 그대로 먹일 수단이 있어야
    /// 재현이 되는데, 지금은 **스크린샷을 보고 사람이 좌표를 옮겨 적는** 수밖에 없다 —
    /// 그 과정에서 한 칸만 틀려도 **다른 보드를 재게 된다**(오늘 하네스가 실제로 그랬다).
    ///
    /// 이 신호가 켜지면 보드가 자기 상태를 **하네스가 읽는 꼴로** 찍어 준다.
    ///
    /// ⚠️ **그리지 않고 판정하지 않는다.** 요청 하나와 결과 하나만 든다 —
    /// `MBI.Combat` 이 `MBI.Logistics` 를 참조하지 않으므로 이 자리가 둘 사이의 유일한 다리다.
    /// </summary>
    public static class BoardDumpSignals
    {
        /// <summary>덤프를 떠 달라. 보드가 처리하고 스스로 내린다.</summary>
        public static bool Requested;

        /// <summary>마지막 덤프 글. 비어 있으면 아직 뜬 적이 없다.</summary>
        public static string Latest = string.Empty;

        /// <summary>덤프가 새로 떠졌는가 — 버튼이 「복사됨」을 보여 줄 때만 쓴다.</summary>
        public static int Version;

        /// <summary>새 씬·새 판에서 되돌린다.</summary>
        public static void Reset()
        {
            Requested = false;
            Latest = string.Empty;
            Version = 0;
        }
    }
}
