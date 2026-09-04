using System.Collections.Generic;

namespace MBI.Data
{
    /// <summary>
    /// 레시피 열둘의 표 (2026-09-04 신설 · `260904_W01` 3-2).
    ///
    /// **이 표가 코드 쪽 진실 원천이다.** 자산 생성기도 테스트도 여기를 읽어야 조합표가
    /// 두 곳에서 갈리지 않는다. W01 5-2가 「위 개정이 끝나기 전에도 구현은 3-2 표를 근거로
    /// 진행해도 된다 — 표가 진실 원천이고 문서가 그걸 따라온다」고 적었다.
    ///
    /// **입력면 수는 노드가 정하고 레시피가 정하지 않는다.** 레시피를 바꿔도 면이 늘거나
    /// 줄지 않으므로, 입력이 1종인 것과 2종인 것은 애초에 다른 노드다 — 그래서 군수가
    /// 기초·복합으로 갈렸다.
    ///
    /// ⚠️ **수량은 아직 없다.** W01 3-2 표는 입력 품목만 적고 개당 몇 개인지는 안 적었다.
    /// 여기서는 전부 <see cref="PerOutputTbd"/>(=1)로 두었다 — 값을 발명하지 않기 위한
    /// 자리이며, 밸런스가 정하면 이 상수 대신 행마다 값을 넣는다.
    /// </summary>
    public static class RecipeCatalog
    {
        /// <summary>산출 1개당 재료 개수. **미확정 센티넬**이며 밸런스 확정 시 행별 값으로 갈린다.</summary>
        public const float PerOutputTbd = 1f;

        /// <summary>레시피 한 줄 — 어느 노드가 무엇을 먹어 무엇을 내는가.</summary>
        public readonly struct Row
        {
            public readonly NodeType owner;
            public readonly RecipeKind kind;
            public readonly string displayName;
            public readonly FlowKind output;
            public readonly FlowKind[] inputs;

            public Row(NodeType owner, RecipeKind kind, string displayName,
                FlowKind output, params FlowKind[] inputs)
            {
                this.owner = owner;
                this.kind = kind;
                this.displayName = displayName;
                this.output = output;
                this.inputs = inputs ?? System.Array.Empty<FlowKind>();
            }
        }

        private static readonly Row[] Rows =
        {
            // 코어 — 입력면 0. 원천에서 난다.
            new Row(NodeType.Core, RecipeKind.CoreEnergy, "코어 에너지",
                FlowKind.CoreEnergy),

            // 가공 — 입력면 1.
            new Row(NodeType.Processing, RecipeKind.BasicParts, "기초재료·부품",
                FlowKind.BasicParts, FlowKind.CoreEnergy),
            new Row(NodeType.Processing, RecipeKind.PowerMaterial, "발전재료",
                FlowKind.PowerMaterial, FlowKind.CoreEnergy),
            // 배터리만 코어 에너지가 아니라 발전재료를 먹는다. 이 한 줄이 다툼을 만든다 —
            // 배터리를 늘리면 전력·추진제·폭발탄이 같이 줄어든다(W01 3-4).
            new Row(NodeType.Processing, RecipeKind.Battery, "배터리",
                FlowKind.Battery, FlowKind.PowerMaterial),

            // 기초 군수 — 입력면 1.
            new Row(NodeType.MunitionsBasic, RecipeKind.StandardAmmo, "표준탄",
                FlowKind.StandardAmmo, FlowKind.BasicParts),
            new Row(NodeType.MunitionsBasic, RecipeKind.DroneBody, "드론 몸체 부품",
                FlowKind.DroneBodyParts, FlowKind.BasicParts),
            new Row(NodeType.MunitionsBasic, RecipeKind.DefenseMaterial, "방어 재료",
                FlowKind.DefenseMaterial, FlowKind.BasicParts),
            new Row(NodeType.MunitionsBasic, RecipeKind.Propellant, "추진제",
                FlowKind.Propellant, FlowKind.PowerMaterial),

            // 복합 군수 — 입력면 2. **표준탄이 특수탄의 재료다.**
            new Row(NodeType.MunitionsComplex, RecipeKind.PierceAmmo, "관통탄",
                FlowKind.PierceAmmo, FlowKind.StandardAmmo, FlowKind.BasicParts),
            new Row(NodeType.MunitionsComplex, RecipeKind.ExplosiveAmmo, "폭발탄",
                FlowKind.ExplosiveAmmo, FlowKind.StandardAmmo, FlowKind.PowerMaterial),
            new Row(NodeType.MunitionsComplex, RecipeKind.StackDrone, "누적형 드론",
                FlowKind.StackDrone, FlowKind.Battery, FlowKind.DroneBodyParts),
            new Row(NodeType.MunitionsComplex, RecipeKind.AoeDrone, "광역형 드론",
                FlowKind.AoeDrone, FlowKind.Battery, FlowKind.DroneBodyParts),
        };

        public static IReadOnlyList<Row> All => Rows;

        /// <summary>이 노드가 돌릴 수 있는 조합표들.</summary>
        public static List<Row> For(NodeType type)
        {
            var list = new List<Row>();
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].owner == type) list.Add(Rows[i]);
            return list;
        }

        /// <summary>
        /// 그 노드가 가져야 할 입력면 수. **레시피가 아니라 노드의 속성이다** —
        /// 같은 노드의 조합표는 전부 같은 개수를 먹어야 하며, 다르면 자산이 잘못된 것이다.
        /// </summary>
        public static int InputFacesOf(NodeType type)
        {
            int max = 0;
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].owner == type && Rows[i].inputs.Length > max)
                    max = Rows[i].inputs.Length;
            return max;
        }
    }
}
