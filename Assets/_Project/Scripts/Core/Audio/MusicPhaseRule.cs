using MBI.Data;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 어느 곡을 트는가 (사운드 문서 6장 · 2026-09-09 배선).
    ///
    /// **판정은 이미 있는 것을 그대로 쓴다** — `StageReqType.Budget`이 문서에서
    /// 「예산식(보스 HP) — S6」이며, 배경 배선의 보스 판정과 **같은 자리**다.
    /// 스테이지 id 문자열을 여기서 다시 비교하면 「S6이 무엇인가」에 답이 둘이 된다.
    /// </summary>
    public static class MusicPhaseRule
    {
        /// <summary>이 스테이지의 국면. 보스전은 S6 하나뿐이다.</summary>
        public static MusicPhase Of(StageReqType reqType)
            => reqType == StageReqType.Budget ? MusicPhase.Boss : MusicPhase.Battle;

        /// <summary>
        /// 곡을 갈아야 하는가. **국면이 바뀔 때만 참이다.**
        ///
        /// ⚠️ 화면이 바뀌었다고 갈지 않는다 — 조립 화면으로 들어가도 **이어서 재생한다**
        /// (사운드 문서 6장). UI 문서 1장의 연속성 원칙이 「음악이 끊기면 두 화면이
        /// 서로 다른 장소처럼 느껴진다」로 그 근거를 든다.
        /// </summary>
        public static bool NeedsSwap(MusicPhase playing, MusicPhase wanted) => playing != wanted;
    }
}
