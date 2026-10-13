namespace MBI.Core
{
    /// <summary>
    /// **파밍 층에서 로봇이 부서지면 어디로 가는가** (2026-10-13 사용자 확정 #113).
    ///
    /// 규칙 — **이전 스테이지의 파밍 층으로 간다.** S(k) 에서 부서지면 S(k−1) 파밍이고,
    /// 몬스터는 그 판 **정원부터 새로**, 로봇 HP 는 **만탱**, 파밍 기록 한 바퀴는 **끊긴다.**
    ///
    /// 🗑️ **「제자리 부활」 가정 폐기**(같은 날 · 내가 잡아 두려던 것). 제자리 부활이면
    /// 파괴에 **값이 없다** — 죽어도 같은 자리에서 같은 적을 계속 잡으니 파밍이
    /// 「죽지 않는 자동 사냥」이 된다. 한 판 뒤로 밀리면 **더 센 판에 오래 서 있는 것**이
    /// 이득이 되고, 그것이 보드를 고칠 까닭이 된다.
    ///
    /// ⚠️ **S1 에서 부서지면 갈 데가 없다** — 앞 판이 없으므로 **S1 파밍으로 다시 선다.**
    /// 그 한 줄은 **플랜 가정**이며 사용자 확인 자리다(확정 문구가 S1 을 안 다뤘다).
    ///
    /// ⚠️ **튜토리얼 판은 이 규칙 밖이다**(여섯에 안 든다). 그 판에는 파밍 층이 없다.
    /// </summary>
    public static class FarmDefeatRule
    {
        /// <summary>
        /// 부서진 판의 번호를 받아 **다시 설 판의 번호**를 낸다.
        ///
        /// 0 을 받으면 0 을 돌려준다 — 여섯에 안 드는 판(튜토리얼)은 이 규칙을 안 탄다.
        /// </summary>
        public static int NextStageNumber(int brokenStageNumber)
        {
            if (brokenStageNumber <= 0) return 0;          // 여섯 밖 — 규칙 밖이다
            if (brokenStageNumber == 1) return 1;          // ⚠️ 가정 — 앞 판이 없다
            return brokenStageNumber - 1;
        }

        /// <summary>한 판 뒤로 밀렸는가 — 화면이 「S3 → S2」를 말할지 정할 때 쓴다.</summary>
        public static bool MovesBack(int brokenStageNumber) =>
            NextStageNumber(brokenStageNumber) != brokenStageNumber && brokenStageNumber > 0;

        /// <summary>
        /// 부서진 뒤 다시 서기까지 기다리는 초. ⚠️ **구현 가정**(설계 역기입 자리).
        ///
        /// 0 이면 눈 깜짝할 새에 바뀌어 **무엇이 일어났는지 안 읽힌다.** 너무 길면
        /// 방치형에서 손이 묶인다. 「한 번 읽을 만큼」으로 잡았다.
        /// </summary>
        public const float RespawnDelaySeconds = 2f;
    }
}
