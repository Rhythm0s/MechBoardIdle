using System;

namespace MBI.Data
{
    /// <summary>
    /// 노드 공통 변수 — 전력 / 탄약 / 발열 IO (§3, §5-2).
    ///
    /// 값은 코드 리터럴이 아니라 .asset에 저장한다(§3 수치 하드코딩 금지).
    /// 현재 balance.json의 노드별 수치는 전부 미확정(confirmed:false) — 합계 병목치
    /// (pw/pwc/heat/heatc)만 존재 — 이므로 생성기가 confirm=Tbd 로 마킹하고
    /// 병목 밴드값을 placeholder로 주입한다. 확정 시 SO만 손 입력(§9 커밋 게이트).
    /// </summary>
    [Serializable]
    public struct NodeResourceProfile
    {
        // --- 전력 (§3 전력 긴장 영구화: 효율 강화 불가, 가동 고정비 전용) ---
        public float powerDraw;     // 가동 고정비 — 모든 노드가 소비. balance.json 합 pw.
        public float powerSupply;   // 발전량 — 에너지 노드만 > 0. balance.json 합 pwc.

        // --- 탄약 (군수 노드가 생산, 마운트가 소비) ---
        public float ammoProduce;   // 생산/초 — 군수 노드. balance.json capA 소비상한과 대응.
        public float ammoConsume;   // 소비/초.

        // --- 발열 (임계 초과 시 감쇠 — §5-5 시뮬 담당) ---
        public float heatGenerate;  // 발열/초. 노드 대당 값(조립 문서「노드 종류」 부하 열).

        // ⚠️ 냉각(heatDissipate)은 **노드의 값이 아니다**(260829_V03 §판정①).
        // 구 냉각 노드는 2026-07-02에 모듈 F로 전환되어 노드 목록에서 빠졌다 —
        // 노드에 냉각량을 두면 폐기된 노드가 이름만 바꿔 되살아난다.
        // 냉각은 LogisticsConfig.moduleCoolingTbd(모듈 F 소유)가 든다.

        /// <summary>확정/미확정 표시. balance.json confirmed 플래그 대응(§7 오표기 방지).</summary>
        public ConfirmState confirm;
    }
}
