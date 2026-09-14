using System;

namespace MBI.Data
{
    /// <summary>
    /// 노드 한 면의 입출력 포트. 연결 규칙(NodeConnectionRules)의 최소 단위.
    /// 한 면에 여러 종류가 흐를 수 있으므로 NodeDefinition은 List&lt;NodePort&gt;로 보관.
    /// </summary>
    [Serializable]
    public struct NodePort
    {
        public PortFace face;   // 어느 면인가 (N/E/S/W)
        public PortIO io;       // 입력 / 출력
        // ⚠️ **기본값 표시다 — 실제로 무엇이 드나드는지는 조합표가 정한다**
        //    (2026-09-14 · §72-19 · 사용자 판정 (2)).
        //
        //    노드마다 포트 품목이 하나로 박혀 있어, 종전에는 조합표를 바꿔도 면이 그것을
        //    안 받아 **폭발탄·드론·배터리 줄이 격자 위에 한 줄도 못 섰다.**
        //    이제 `BeltRouting.HasInputPort` 는 **면만** 여기서 읽고 품목은
        //    `NodeInstance.CurrentRecipe` 가 가른다. 이 값은 화면 표시와 기본값으로 남는다.
        public FlowKind kind;   // 이 포트를 흐르는 자원 종류

        public NodePort(PortFace face, PortIO io, FlowKind kind)
        {
            this.face = face;
            this.io = io;
            this.kind = kind;
        }
    }
}
