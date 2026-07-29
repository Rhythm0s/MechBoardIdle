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
        public FlowKind kind;   // 이 포트를 흐르는 자원 종류

        public NodePort(PortFace face, PortIO io, FlowKind kind)
        {
            this.face = face;
            this.io = io;
            this.kind = kind;
        }
    }
}
