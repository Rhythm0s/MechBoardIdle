using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **S5 폭발탄 튜토리얼** (사용자 확정 #190 · 2026-10-19 · 기획서 수준 전체판 · T5급).
    ///
    /// 자리 = #169 프리셋 좌표 그대로(`PresetBoards`) — 변환기 (6,7) 조합표 발전재료 · (7,7) 벨트 · 분류기 (8,8) ·
    /// 복합 가공소 (8,7) 90° · 조합표 폭발탄. 산출은 옛 기둥 (8,6)(8,5) → 운반로 y5 → 마운트(시작 보드에 이미 있다).
    ///
    /// ⚠️ **차례는 판 상태에서 따라 나온다**(새 장부 0 · T4 와 같은 결) — 보드가 매 프레임 칸을 읽어 다음 걸음을 낸다.
    /// ⚠️ **끝 = 다섯 칸이 다 섰다**(구조 판정 · 구현 판단). 플랜 가정 「폭발탄이 마운트 A 에 도착」은 도착률이 오르내려
    ///    끝이 깜빡이므로(저장 장부 없이 「한 번」을 못 든다) 구조로 둔다 — 서면 곧 도착한다.
    /// ⚠️ **대상 칸은 탭 한 번에 바뀐다**(구현 판단) — 놓인 것을 끌어 지우는 손은 이웃 칸까지 지운다(드래그 경로 전체).
    ///    그래서 가리키는 칸만 덮어쓴다: (7,7) 탭 = 벨트 · 분류기 고르고 (8,8) 탭 = 분류기 · 복합 가공소 고르고 (8,7) 탭 = 90° 로 선다.
    /// </summary>
    public static class ExplosiveTutorialRule
    {
        /// <summary>걸음 — 차례대로.</summary>
        public enum Step
        {
            /// <summary>모름(보드가 아직 안 알렸다) 또는 해당 없음.</summary>
            None,
            /// <summary>① 변환기 (6,7) 조합표 「발전재료」.</summary>
            PowerMaterial,
            /// <summary>② (7,7) 를 벨트로(서 → 동).</summary>
            Belt,
            /// <summary>③ 분류기 (8,8).</summary>
            Sorter,
            /// <summary>④ 복합 가공소 (8,7) · 90°.</summary>
            Complex,
            /// <summary>⑤ 복합 가공소 조합표 「폭발탄」.</summary>
            ExplosiveRecipe,
            /// <summary>끝.</summary>
            Done,
        }

        /// <summary>안내가 서는 판 — S5(주제 「폭발탄」 · STAGE_DATA 5행).</summary>
        public const string StageId = "S5";

        public static readonly Vector2Int ConverterCell = PresetBoards.PowerMaterialCell;   // (6,7)
        public static readonly Vector2Int BeltCell = new Vector2Int(7, 7);
        public static readonly Vector2Int SorterCell = PresetBoards.SplitCell;              // (8,8)
        public static readonly Vector2Int ComplexCell = PresetBoards.ExplosiveNodeCell;     // (8,7)

        /// <summary>복합 가공소가 서야 할 돌림(시계 90° = 1 · 프리셋 그대로).</summary>
        public const int ComplexRotation = 1;

        /// <summary>S5 파밍 첫 진입 알림 — ⚠️ 가정(#190).</summary>
        public const string GrantNotice = "복합 가공소가 열렸습니다";

        /// <summary>#216 — 걸음 ⑤ 끝 뒤 자동 완성이 끝나면 한 번(⚠️ 문안 플랜 가정).</summary>
        public const string AutoCompleteNotice = "이렇게 이으면 표준탄도 마운트에 닿습니다";

        /// <summary>전투 화면 문구 — ⚠️ 가정(#190).</summary>
        public const string CombatPrompt = "조립에서 복합 가공소로 폭발탄을 만들어보세요";

        /// <summary>
        /// 다섯 칸을 읽어 다음 걸음을 낸다. 인자는 보드가 칸에서 읽은 사실이다.
        /// </summary>
        public static Step Next(bool converterPowerMaterial, bool beltAt77, bool sorterAt88,
                                bool complexAt87Rotated, bool complexExplosive)
        {
            if (!converterPowerMaterial) return Step.PowerMaterial;
            if (!beltAt77) return Step.Belt;
            if (!sorterAt88) return Step.Sorter;
            if (!complexAt87Rotated) return Step.Complex;
            if (!complexExplosive) return Step.ExplosiveRecipe;
            return Step.Done;
        }

        /// <summary>그 걸음이 가리키는 칸.</summary>
        public static Vector2Int CellOf(Step s)
        {
            switch (s)
            {
                case Step.PowerMaterial: return ConverterCell;
                case Step.Belt: return BeltCell;
                case Step.Sorter: return SorterCell;
                default: return ComplexCell;
            }
        }

        /// <summary>그 걸음에 고철(노드 설치비)이 드는가 — 복합 가공소를 놓는 걸음만.</summary>
        public static bool NeedsNodeScrap(Step s, bool complexAlreadyThere) => s == Step.Complex && !complexAlreadyThere;

        /// <summary>
        /// 안내가 서는가 — 튜토리얼 마침 · S5 파밍 · 보드가 알린 걸음이 끝 전 · (노드를 놓을 걸음이면) 고철 ≥ 설치비.
        /// 고철이 모자라면 막을 안 띄운다(갇힘 방지 · 해금 · 강화 막과 같은 결).
        /// </summary>
        public static bool IsActive(bool tutorialCleared, int stageNumber, bool isFarming, Step step,
                                    bool needsScrap, double scrap, double nodeScrap) =>
            tutorialCleared && isFarming && stageNumber == StageMapName.NumberOf(StageId)
            && step != Step.None && step != Step.Done
            && (!needsScrap || scrap >= nodeScrap - 1e-9);

        /// <summary>팔레트에서 이 노드 단추가 열리는가 — ④ 걸음의 복합 가공소 하나만.</summary>
        public static bool AllowsNode(Step s, NodeType type) => s == Step.Complex && type == NodeType.MunitionsComplex;

        /// <summary>팔레트에서 이 벨트 요소가 열리는가 — ③ 걸음의 분류기 하나만.</summary>
        public static bool AllowsElement(Step s, BeltElementKind e) => s == Step.Sorter && e == BeltElementKind.Sorter;

        /// <summary>
        /// 보드 위 안내 — ⚠️ 문안 가정(#190). <paramref name="occupiedByOther"/> = 그 칸에 다른 것이 서 있다.
        /// </summary>
        public static string Hint(Step s, bool occupiedByOther)
        {
            switch (s)
            {
                case Step.PowerMaterial:
                    return occupiedByOther ? "이 변환기를 탭해 조합표 「발전재료」를 고르세요"
                                           : "이 자리의 변환기를 탭해 조합표 「발전재료」를 고르세요";
                case Step.Belt: return "이 칸을 탭해 벨트로 바꾸세요";
                // ✅ #215(2026-10-20 사용자 육안) — 실제 탭 이름으로(🗑️ 「벨트 요소에서 분류기를 …」 · 🗑️ 「복합 가공소를 고른 뒤 …」).
                case Step.Sorter:
                    return $"{PaletteCategories.LabelOf(PaletteCategories.OfBeltElement())} 탭에서 분류기를 고른 뒤 이 칸을 탭하세요";
                case Step.Complex:
                    return $"{PaletteCategories.LabelOf(PaletteCategory.Complex)} 탭에서 복합 가공소를 고른 뒤 이 칸을 탭하세요";
                case Step.ExplosiveRecipe: return "복합 가공소를 탭해 조합표 「폭발탄」을 고르세요";
                default: return null;
            }
        }
    }
}
