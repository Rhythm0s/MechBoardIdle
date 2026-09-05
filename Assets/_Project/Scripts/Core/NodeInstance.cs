using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 격자에 배치된 노드 1기의 순수 논리 표현(§5-3).
    /// NodeDefinition(스키마)와 놓인 셀 좌표만 담는다.
    ///
    /// GameObject/비주얼 참조는 두지 않는다 — 씬 표현(마커 등)은 BoardController가
    /// 셀→GameObject 매핑으로 따로 관리한다(Core 순수성 유지, EditMode 테스트 가능).
    /// </summary>
    public sealed class NodeInstance
    {
        public readonly NodeDefinition Definition;
        public readonly Vector2Int Cell;

        /// <summary>
        /// 이 노드가 만드는 탄종(군수 노드에만 의미 — 다른 타입에서는 읽지 않는다).
        ///
        /// 노드 1개 = 1발/초이고 탄종은 **노드별로 지정**된다(260824_V02 §1).
        /// 노드 카탈로그를 3종으로 쪼개는 대신 인스턴스 속성으로 둔 이유는 **노드 6종 확정**을 지키기 위해서다.
        ///
        /// 기본값이 관통인 근거: `origin`(원점 100)의 basis가 「관통탄 20×5발 **기본 라인**」이다.
        /// 관통이 원천상 기본 라인이므로 임의 선택이 아니다.
        /// </summary>
        public AmmoKind AmmoKind { get; set; } = AmmoKind.Pierce;

        /// <summary>
        /// 이 노드가 **지금 돌리는 조합표 하나**(260827_V01 §3). 후보는 Definition.recipes가 갖는다.
        /// 배치 후에도 언제든 바꿀 수 있다 — 바꾸는 순간의 처리는 <see cref="SelectRecipe"/> 참조.
        /// </summary>
        public RecipeKind SelectedRecipe { get; private set; } = RecipeKind.None;

        /// <summary>
        /// 출력 버퍼(개). 가득 차면 이 노드는 생산을 멈춘다.
        /// ⚠️ **만충 판정에 세지 않는다** — 태그가 보는 창고는 저장 노드다(§3-3).
        /// </summary>
        public float OutputBuffer { get; set; }

        /// <summary>
        /// 버퍼에 든 것의 종류. 조합표를 바꿔도 이전 산출물은 남으므로, 지금 조합표의 산출과
        /// 다를 수 있다 — 그 차이가 「정지 사유」를 가른다(§2-1).
        /// </summary>
        public FlowKind BufferKind { get; set; }

        /// <summary>
        /// 입력 버퍼 — 벨트로 도착한 재료가 품목별로 쌓이는 자리 (2026-09-04 신설 · `260904_W01` 3장).
        ///
        /// **어느 면으로 들어왔는지는 기억하지 않는다.** 입력면은 재료 종류를 가리지 않으며
        /// 노드가 안에서 맞추므로(W01 3-2), 면이 아니라 품목으로만 센다.
        /// </summary>
        public Dictionary<FlowKind, float> InputBuffer { get; } = new Dictionary<FlowKind, float>();

        /// <summary>도착한 재료를 입력 버퍼에 넣는다.</summary>
        public void TakeInput(FlowKind kind, float amount)
        {
            if (amount <= 0f) return;
            InputBuffer.TryGetValue(kind, out float have);
            InputBuffer[kind] = have + amount;
        }

        /// <summary>재료가 모자라 멈춰 있는가. 출력 버퍼 만충과 사유가 다르다.</summary>
        public bool IsStarved => NodeProduction.IsStarved(CurrentRecipe, InputBuffer);

        /// <summary>지금 왜 멈춰 있는가. 멈추지 않았으면 None.</summary>
        public NodeStallReason StallReason => NodeProduction.StallReason(CurrentRecipe, OutputBuffer, BufferKind);

        /// <summary>
        /// 현재 조합표. 고르지 않았으면 **노드 종류가 정한 기본값**을 쓴다.
        ///
        /// 기본값이 있는 이유는 노드를 놓았는데 아무것도 안 하면 플레이어가 무엇을 더 해야
        /// 하는지 모르기 때문이다. 「시스템은 개입하지 않는다」에 어긋나지 않는다 —
        /// **답을 정해 주는 것이 아니라 바꾸기 전의 자리를 정하는 것**이다
        /// (조립 시스템 문서「노드 종류」· `260903_W03` 3장).
        ///
        /// ⚠️ **「첫 후보」로 두지 않는다.** 그러면 목록에 항목을 하나 더했을 때 기본값이
        /// 조용히 바뀐다. 종전 구현이 그랬고, 같은 함정이 `RefillMount`의 탄종 열거 순서에도
        /// 있었다(`260903_V01` 5-1).
        ///
        /// **재고를 보지 않는다.** 판정 기준은 자산의 정적 속성뿐이라 매번 같은 답이 나온다 —
        /// 부품이 떨어졌다고 노드가 다른 조합표로 갈아타지 않는다. 재고가 없으면 그 조합표를
        /// 잡은 채 멈추고, 멈춘 것이 병목으로 화면에 나온다.
        /// </summary>
        public NodeRecipe CurrentRecipe
        {
            get
            {
                List<NodeRecipe> candidates = Definition != null ? Definition.recipes : null;
                if (candidates == null) return default;

                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].kind == SelectedRecipe) return candidates[i];

                RecipeKind fallback = DefaultRecipeFor(Definition.type);
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].kind == fallback && candidates[i].IsRunnable) return candidates[i];

                return default;
            }
        }

        /// <summary>
        /// 노드 종류별 기본 조합표 (2026-09-03 확정 · `260903_W03` 3-2).
        ///
        /// 군수 노드가 관통탄인 근거는 튜토리얼 전용 스테이지의 시작 보드가 관통 4노드라는 것이다 —
        /// 처음 보는 탄종과 기본값이 같아야 학습이 어긋나지 않는다. 탄종 자체는
        /// <see cref="AmmoKind"/>가 들고 있고 그 기본값도 관통이다.
        ///
        /// **가공의 기본값이 2026-09-04에 들어왔다** — 그때까지 그 조합표가 `RecipeKind`에도
        /// 자산에도 없어 가공 단계가 통째로 비어 있었다(`260903_W04` 3장).
        ///
        /// 기초 군수의 기본값은 **표준탄**이다. 구 「탄약」 자리이며 특수탄의 재료이므로
        /// 로봇 A는 이 노드를 반드시 거친다. 복합 군수는 관통탄이 기본인데, 원점 100의
        /// basis가 「관통탄 20×5발 기본 라인」이기 때문이다.
        /// </summary>
        private static RecipeKind DefaultRecipeFor(NodeType type)
        {
            switch (type)
            {
                case NodeType.Core:             return RecipeKind.CoreEnergy;
                case NodeType.Processing:       return RecipeKind.BasicParts;
                case NodeType.MunitionsBasic:   return RecipeKind.StandardAmmo;
                case NodeType.MunitionsComplex: return RecipeKind.PierceAmmo;
                default:                        return RecipeKind.None;
            }
        }

        /// <summary>
        /// 조합표를 바꾼다. 후보에 없거나 돌릴 수 없는 것은 거절한다.
        ///
        /// 바꾸는 순간의 처리(2026-08-27 구현 판단, 기획 회신 대기):
        ///   - **진행 중이던 1회분은 취소된다.** 완주를 기다리면 「언제든 바꿀 수 있다」가 깨져
        ///     조작이 먹지 않은 것으로 읽힌다. 입력은 완료 시점에 소비하므로 취소해도 잃는 것이 없다.
        ///   - **출력 버퍼에 남은 이전 산출물은 그대로 둔다.** 밀어내면 창고에 원치 않는 품목이
        ///     섞이고, 지우면 플레이어 자원을 몰수하는 것이 된다. 남아 있는 동안 새 조합표는
        ///     자리가 없어 멈추는데, 그것이 「막히면 멈춘다」와 같은 규칙이라 따로 배울 것이 없다.
        ///   - **입력 버퍼의 이전 재료도 그대로 둔다.** 되돌리면 다시 쓴다.
        /// 한 문장으로: **코드가 플레이어의 물건을 없애지 않는다.**
        /// </summary>
        public bool SelectRecipe(RecipeKind kind)
        {
            List<NodeRecipe> candidates = Definition != null ? Definition.recipes : null;
            if (candidates == null) return false;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].kind != kind || !candidates[i].IsRunnable) continue;
                SelectedRecipe = kind;
                return true; // 진행률만 버린다 — 버퍼는 건드리지 않는다
            }
            return false;
        }

        public NodeInstance(NodeDefinition definition, Vector2Int cell)
        {
            Definition = definition;
            Cell = cell;
        }
    }
}
