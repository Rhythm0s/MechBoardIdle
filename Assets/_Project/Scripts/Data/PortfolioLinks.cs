using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 메인 메뉴가 여는 바깥 주소와 그 위에 적는 말
    /// (2026-09-10 사용자 확정 · 플랜 §66-10).
    ///
    /// **주소를 코드에 박지 않는다**(지침 §3). 주소는 밸런스가 아니라 **사람이 주는 값**이고,
    /// 바뀔 때 코드를 고쳐 다시 빌드하게 만들 이유가 없다.
    ///
    /// ⚠️ **지금은 값이 없다.** 사용자가 아직 주소를 주지 않았으므로 **빈 채로 만들어 둔다** —
    /// 빈 주소의 버튼은 <b>비활성</b>이라 눌리지 않는다. 자리표시 주소를 지어 넣으면
    /// 심사자가 그것을 눌러 엉뚱한 곳으로 간다. **없는 값을 지어내지 않는다.**
    ///
    /// ⚠️ **생성기가 덮어쓰지 않는다** — <c>LoadOrCreate</c> 로 만들고, 이미 있으면 그대로 둔다.
    /// 사용자가 인스펙터에 넣은 주소가 다음 생성에서 날아가면 안 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "PortfolioLinks", menuName = "MBI/Portfolio Links")]
    public sealed class PortfolioLinks : ScriptableObject
    {
        [Header("문구")]
        [Tooltip("메뉴 한가운데 적는 한 줄. 이것이 포트폴리오용 데모라는 것을 먼저 말한다.")]
        [TextArea(2, 4)]
        public string notice = "포트폴리오용 데모";

        [Tooltip("아래쪽에 작게 적는 판 표기. 비면 안 적는다.")]
        public string version = "";

        [Header("주소 — 비면 그 버튼은 비활성")]
        [Tooltip("포트폴리오 문서 주소. ⚠️ 미기재 — 사용자가 준다.")]
        public string documentUrl = "";

        [Tooltip("관련 노션 주소. ⚠️ 미기재 — 사용자가 준다. **공개 링크여야 심사자가 연다.**")]
        public string notionUrl = "";

        /// <summary>
        /// 이 주소를 눌러도 되는가. **빈 것과 공백만 든 것을 같게 본다** —
        /// 인스펙터에서 지우다 남은 공백 하나가 버튼을 살려 두면 빈 탭이 열린다.
        /// </summary>
        public static bool IsUsable(string url) => !string.IsNullOrWhiteSpace(url);

        public bool HasDocument => IsUsable(documentUrl);
        public bool HasNotion => IsUsable(notionUrl);
    }
}
