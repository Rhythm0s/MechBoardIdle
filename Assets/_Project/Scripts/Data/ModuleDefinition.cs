using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 모듈 2종 (2026-09-09 신설 · 조립 시스템 문서「모듈 재정의」· MVP 문서 11장).
    ///
    /// **범위 효과는 폐기됐다**(2026-08-31) — 모듈은 **붙인 노드 하나에만** 작용한다.
    /// F(냉각)는 발열 축 폐기와 함께 사라졌다(2026-09-02) — 이 열거에 자리를 두지 않는다.
    /// 자리를 남겨 두면 폐기된 것이 이름만으로 되살아난다.
    /// </summary>
    public enum ModuleKind
    {
        /// <summary>M — 생산량. 산출을 올리고 재료는 그대로다. 대신 부하가 가장 무겁다.</summary>
        Output = 0,

        /// <summary>R — 생산속도. 산출과 재료가 함께 오른다. 부하는 M보다 가볍다.</summary>
        Rate = 1,
    }

    /// <summary>
    /// 모듈 한 종의 정의(SO). **값은 여기 하나에만 산다**(지침 §3 수치 하드코딩 금지).
    ///
    /// **이득 칸과 비용 칸을 함께 둔다.** 지침 §7 ［08-31］이 「이득만 적힌 항목은 두 달을
    /// 살아남는다」로 등재한 자리이며, 모듈 세 종에 ×1.5만 있고 부하 칸이 아예
    /// 만들어진 적이 없던 것이 그 사례 본문이다. 그래서 <see cref="powerLoadMultiplier"/>가
    /// 이 클래스에 처음부터 있다.
    ///
    /// ⚠️ **지침 §4의 「노드·모듈 강화: MVP 상수 1.0」은 이 값들이 아니다** — 그것은
    /// 모듈을 **더 강하게 만드는 성장 통로**를 막는 것이고, 여기 있는 것은 **장착 효과**다
    /// (2026-09-08 · `260908_W09` 2-2). 둘을 섞으면 모듈이 MVP에서 통째로 사라진다.
    /// </summary>
    [CreateAssetMenu(fileName = "Module", menuName = "MBI/Module Definition", order = 4)]
    public sealed class ModuleDefinition : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("안정 키. 예: mod_output / mod_rate.")]
        public string moduleId;
        [Tooltip("표시명. 예: 생산량 / 생산속도.")]
        public string displayName;
        public ModuleKind kind;

        [Tooltip("보드에 그릴 기호. ⚠️ 아트가 없어 글자를 자리표시로 쓴다 — 모듈 기호는 보드 아트 문서에 없다.")]
        public string symbol = "M";

        [Header("이득")]
        [Tooltip("붙인 노드의 산출 배수. 조립 시스템 문서「모듈 재정의」가 소스다.")]
        public float outputMultiplier = 1.5f;
        [Tooltip("붙인 노드가 먹는 재료 배수. M은 그대로(1.0) · R은 산출과 함께 오른다(1.5).")]
        public float inputMultiplier = 1f;

        [Header("비용")]
        [Tooltip("붙인 노드의 대당 전력 배수. **부하는 산출보다 가파르게 올라야 한다**(지침 §3).")]
        public float powerLoadMultiplier = 2f;

        [Header("참조")]
        [Tooltip("전역 밸런스 앵커 단일 원천.")]
        public BalanceConfig balanceRef;
    }
}
