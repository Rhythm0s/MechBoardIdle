using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// 팔레트 상위 그룹 **일곱** (2026-09-15 사용자 확정 · 하단 개편 ② ·
    /// 2026-09-17 「군수」 신설 · 참고 = 명일방주: 엔드필드 배치 화면).
    ///
    /// ⚠️ **탭 차례가 곧 이 열거값 차례다** — 화면이 따로 정렬하지 않는다.
    /// ⚠️ **새 값은 맨 뒤에 붙인다** — 앞의 정수는 저장·자산에 박혀 있다.
    ///    그래서 「군수」는 값이 6 이고 **차례는 `Order` 가 따로 정한다**(전력 앞).
    /// </summary>
    public enum PaletteCategory
    {
        All = 0,        // 전체
        Logistics = 1,  // 물류
        Basic = 2,      // 기초 가공
        Complex = 3,    // 복합 가공
        Power = 4,      // 전력
        Module = 5,     // 모듈

        /// <summary>
        /// 군수 — **싸우는 데 쓰는 무형 자원을 내는 것**(2026-09-17 사용자 확정 · `260917_W08` 5-1).
        /// 지금은 **쉴드 발생 · 부스터** 둘이다.
        ///
        /// ⚠️ 탭 이름 「군수」는 **기초 가공 · 복합 가공 노드 이름과 같은 말**이었는데,
        ///    같은 날 그 둘이 **기초 가공 · 복합 가공**으로 개명되어 겹침이 풀렸다.
        /// </summary>
        Munitions = 6,
    }

    /// <summary>
    /// 무엇이 어느 그룹인가.
    ///
    /// ⚠️ **소속은 전부 구현 가정이다**(2026-09-15 · 설계 역기입 목록). 조립 문서에
    /// 팔레트 그룹 절이 없다. 사용자가 고칠 수 있게 **이 파일 한 곳**에 모았다 —
    /// 화면 코드에 흩어 두면 고칠 자리를 찾는 것부터 일이 된다.
    ///
    /// 명시 그룹 셋은 「무엇을 하는 물건인가」로 정한다 —
    /// · **물류** = 옮기고 **나누고** 쌓는 것 — 병합기 · 분류기 · 저장
    ///   (🗑️ 구 「고르고」 폐기 2026-09-17 — 품목을 고르는 것으로 읽힌다)
    /// · **전력** = **전력을 내고 관리하는 것만** — 에너지
    ///   (🗑️ 구 「전력으로 무형 자원을 내는 것 — 부스터」 폐기 2026-09-17 사용자 확정 ·
    ///    부스터는 아래 「군수」로 갔다)
    /// · **군수** = 싸우는 데 쓰는 **무형 자원**을 내는 것 — 쉴드 발생 · 부스터 (2026-09-17 신설)
    /// · **모듈** = 노드에 **붙는** 것 — M · R
    ///
    /// 남는 가공 계열 둘은 **입력면 수가 가른다**(2026-09-15 사용자 확정) —
    /// · **기초 가공** = 입력 하나 · 출력 하나
    /// · **복합 가공** = 입력 둘 이상
    /// 지금 자산이면 가공·기초 가공가 기초, 복합 가공가 복합이다. 이름이 아니라 수라서,
    /// 나중에 입력 둘짜리가 새로 생겨도 **이 파일을 안 고쳐도** 복합으로 간다.
    ///
    /// ⚠️ **모듈은 복합 가공이 아니다**(2026-09-15 사용자 정정). 모듈은 놓는 것이 아니라
    /// **붙이는 것**이라 다른 축이다 — 복합 가공에 섞어 두면 「놓을 것」 목록에 놓을 수
    /// 없는 것이 낀다.
    ///
    /// ⚠️ **코어는 어디에도 없다** — 시작 보드 고정이라 팔레트에 뜨지 않는다.
    /// 그래도 <see cref="Of"/> 는 답을 줘야 하므로 기초 가공으로 둔다(안 쓰이는 답이다).
    /// </summary>
    public static class PaletteCategories
    {
        /// <summary>
        /// 탭에 뜨는 차례. 「전체」가 맨 앞이다.
        /// ⚠️ **열거값 차례가 아니라 이 배열이 화면 차례다**(2026-09-17) —
        /// 「군수」는 값이 6 이지만 **전력 앞**에 선다(만드는 것 → 먹이는 것 순).
        /// </summary>
        public static readonly PaletteCategory[] Order =
        {
            PaletteCategory.All,
            PaletteCategory.Logistics,
            PaletteCategory.Basic,
            PaletteCategory.Complex,
            PaletteCategory.Munitions,
            PaletteCategory.Power,
            PaletteCategory.Module,
        };

        public static string LabelOf(PaletteCategory c)
        {
            switch (c)
            {
                case PaletteCategory.All: return "전체";
                case PaletteCategory.Logistics: return "물류";
                // ⚠️ **두 글자로 줄였다**(2026-09-15 사용자 확정 · 육안 6차 ⑥).
                //
                // 「기초 가공」·「복합 가공」은 다섯 글자라 여섯 칸으로 나눈 탭 줄에서
                // **잘렸다** — 화면에는 「초 기」·「합 기」로 찍혔다. 나머지 넷은 두 글자라
                // 멀쩡했고, **두 칸만 길어서 그 둘만 깨진 것**이다.
                //
                // 「가공」은 **여섯 중 둘에만 붙는 꼬리**라 구별에 보태는 것이 없다 —
                // 기초/복합이 이미 둘을 가른다. 문서 문안이 서면 그때 이 줄이 바뀐다.
                case PaletteCategory.Basic: return "기초";
                case PaletteCategory.Complex: return "복합";
                case PaletteCategory.Munitions: return "군수";
                case PaletteCategory.Power: return "전력";
                case PaletteCategory.Module: return "모듈";
                default: return "";
            }
        }

        /// <summary>
        /// 노드 하나가 속한 그룹 (2026-09-15 사용자 확정 — **입력 포트 수로 유도한다**).
        ///
        /// ⚠️ **가공 계열은 이름으로 안 가른다.** 「기초 가공」·「복합 가공」라는 이름이
        /// 아니라 **입력면이 몇 개인가**로 갈린다 —
        /// · 입력 하나 → **기초 가공**
        /// · 입력 둘 이상 → **복합 가공**
        ///
        /// 그래서 나중에 입력 둘짜리 노드가 새로 생기면 **이 표를 안 고쳐도** 복합으로 간다.
        /// 이름으로 갈랐다면 그때마다 여기 한 줄을 더해야 했고, 빠뜨리면 조용히 엉뚱한
        /// 탭에 선다.
        ///
        /// ⚠️ **명시 그룹이 먼저다** — 전력 · 물류 · 모듈은 「무엇을 받는가」가 아니라
        /// 「무엇을 하는 물건인가」로 정해진 것이라 입력 수 규칙 **앞**에 둔다.
        /// 저장 노드는 입력이 하나지만 물류다.
        /// </summary>
        public static PaletteCategory Of(NodeDefinition def)
        {
            if (def == null) return PaletteCategory.Basic;

            // ── 명시 그룹 (입력 수 규칙보다 먼저) ──────────────────────────────
            switch (def.type)
            {
                case NodeType.Storage: return PaletteCategory.Logistics;

                // ✅ **전력은 전력만**(2026-09-17 사용자 확정 · `260917_W08` 5-1).
                case NodeType.Energy: return PaletteCategory.Power;

                // ✅ **군수 — 싸우는 데 쓰는 무형 자원**(2026-09-17 사용자 확정).
                //    🗑️ 구 「부스터 = 전력」 폐기. 쉴드도 여기다 —
                //    ⚠️ 쉴드는 입력면이 하나라 **규칙대로 두면 기초 가공**으로 간다.
                //    생존 두 층(회피 · 쉴드)이 다른 탭에 서는 것을 사용자가 고쳤다.
                //    📌 **입력면 수 규칙은 이 두 종에서 멈춘다** — 명시 그룹이 먼저다.
                case NodeType.Booster:
                case NodeType.Shield: return PaletteCategory.Munitions;
            }

            // ── 남는 노드 = 가공 계열. 입력면 수가 가른다 ───────────────────────
            return InputFaceCount(def) >= 2 ? PaletteCategory.Complex : PaletteCategory.Basic;
        }

        /// <summary>
        /// 입력면이 몇 개인가 — **면 기준이지 품목 기준이 아니다.**
        ///
        /// ⚠️ 입력면 수는 **노드에 고정된다**(레시피를 바꿔도 면이 늘거나 줄지 않는다 ·
        /// <see cref="NodeType.MunitionsComplex"/> 주석). 그래서 이 수가 조합표보다
        /// 안정된 기준이다 — 조합표로 세면 같은 노드가 탭 사이를 오간다.
        /// </summary>
        public static int InputFaceCount(NodeDefinition def)
        {
            if (def == null || def.ports == null) return 0;
            int n = 0;
            for (int i = 0; i < def.ports.Count; i++)
                if (def.ports[i].io == PortIO.Input) n++;
            return n;
        }

        /// <summary>벨트 요소(병합기·분류기)는 노드가 아니지만 팔레트에 같이 선다.</summary>
        public static PaletteCategory OfBeltElement() => PaletteCategory.Logistics;

        /// <summary>이 탭에서 그 노드가 보이는가.</summary>
        public static bool Shows(PaletteCategory tab, NodeDefinition def) =>
            tab == PaletteCategory.All || Of(def) == tab;

        /// <summary>이 탭에서 벨트 요소가 보이는가.</summary>
        public static bool ShowsBeltElement(PaletteCategory tab) =>
            tab == PaletteCategory.All || tab == PaletteCategory.Logistics;

        /// <summary>이 탭에서 모듈이 보이는가.</summary>
        public static bool ShowsModule(PaletteCategory tab) =>
            tab == PaletteCategory.All || tab == PaletteCategory.Module;
    }
}
