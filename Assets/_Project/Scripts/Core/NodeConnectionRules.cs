using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// 노드 연결 규칙 — 격자에서 인접한 두 노드가 맞닿은 면끼리 연결 가능한지 판정하는 순수 함수.
    /// 실제 스냅/벨트 설치/그리드 배치는 §5-3·4 담당. 여기서는 호환성 판정만 한다.
    ///
    /// 성립 조건:
    ///   A.output(face = f, kind = K)  AND  B.input(face = opposite(f), kind = K)
    ///   즉 맞닿은 두 면에 방향이 반대(출력↔입력)이고 자원 종류가 같은 포트가 있어야 한다.
    /// 스텁 노드(implemented=false, 예: 쉴드 발생)는 연결 대상에서 제외한다(§4 스코프).
    /// </summary>
    public static class NodeConnectionRules
    {
        /// <summary>맞닿은 면의 반대 면.</summary>
        public static PortFace Opposite(PortFace face)
        {
            switch (face)
            {
                case PortFace.North: return PortFace.South;
                case PortFace.South: return PortFace.North;
                case PortFace.East:  return PortFace.West;
                default:             return PortFace.East; // West
            }
        }

        /// <summary>
        /// a의 aFace 면에서 b(반대 면)로 흐름이 연결되는지 판정.
        /// 연결되면 true 와 흐르는 자원 종류(matchedKind)를 반환.
        /// </summary>
        public static bool TryConnect(NodeDefinition a, PortFace aFace, NodeDefinition b, out FlowKind matchedKind)
        {
            matchedKind = default;
            if (a == null || b == null) return false;
            if (!a.implemented || !b.implemented) return false; // 스텁 노드 제외

            PortFace bFace = Opposite(aFace);

            foreach (NodePort outPort in a.ports)
            {
                if (outPort.io != PortIO.Output || outPort.face != aFace) continue;

                foreach (NodePort inPort in b.ports)
                {
                    if (inPort.io != PortIO.Input || inPort.face != bFace) continue;
                    if (inPort.kind != outPort.kind) continue;

                    matchedKind = outPort.kind;
                    return true;
                }
            }
            return false;
        }

        /// <summary>흐름 종류를 몰라도 되는 편의 오버로드.</summary>
        public static bool TryConnect(NodeDefinition a, PortFace aFace, NodeDefinition b)
        {
            return TryConnect(a, aFace, b, out _);
        }
    }
}
