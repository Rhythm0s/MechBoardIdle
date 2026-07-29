using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 물류 보드 격자 — 순수 C# 좌표계·점유 모델(§5-3). MonoBehaviour/씬 비의존 → EditMode 테스트 가능.
    ///
    /// 좌표계:
    ///   - 셀 = Vector2Int(x=열, y=행), 유효 범위 0 ≤ x &lt; Columns, 0 ≤ y &lt; Rows.
    ///   - Origin = 격자 좌하단 코너의 월드 좌표(호출자가 주입 — 중앙 정렬 등 파생값).
    ///   - 셀 중심 월드 = Origin + (cell + 0.5) * CellSize.
    ///   - 월드→셀 = floor((world - Origin) / CellSize).
    ///   round-trip: WorldToCell(CellToWorld(c)) == c (셀 중심은 +0.5 오프셋 → floor(x+0.5)=x, 정확).
    ///
    /// 배치/벨트/면 연결은 하지 않는다(§5-4). 여기서는 좌표 변환·점유(겹침 방지)만.
    /// </summary>
    public sealed class BoardGrid
    {
        public int Columns { get; }
        public int Rows { get; }
        public float CellSize { get; }
        public Vector2 Origin { get; }

        private readonly Dictionary<Vector2Int, NodeInstance> _occupancy = new Dictionary<Vector2Int, NodeInstance>();

        public BoardGrid(int columns, int rows, float cellSize, Vector2 origin)
        {
            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
            CellSize = Mathf.Max(0.0001f, cellSize);
            Origin = origin;
        }

        // ---- 좌표 변환 ----

        /// <summary>셀 중심의 월드 좌표.</summary>
        public Vector2 CellToWorld(Vector2Int cell)
        {
            return new Vector2(
                Origin.x + (cell.x + 0.5f) * CellSize,
                Origin.y + (cell.y + 0.5f) * CellSize);
        }

        /// <summary>월드 좌표가 속한 셀(경계 밖도 계산은 됨 — 유효성은 IsInside로 확인).</summary>
        public Vector2Int WorldToCell(Vector2 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x - Origin.x) / CellSize),
                Mathf.FloorToInt((world.y - Origin.y) / CellSize));
        }

        /// <summary>월드 좌표를 가장 가까운 셀 중심으로 스냅.</summary>
        public Vector2 SnapToCell(Vector2 world)
        {
            return CellToWorld(WorldToCell(world));
        }

        /// <summary>셀이 격자 유효 범위 안인가.</summary>
        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;
        }

        // ---- 점유 ----

        public bool IsOccupied(Vector2Int cell)
        {
            return _occupancy.ContainsKey(cell);
        }

        public NodeInstance GetAt(Vector2Int cell)
        {
            return _occupancy.TryGetValue(cell, out NodeInstance instance) ? instance : null;
        }

        /// <summary>
        /// 셀에 노드를 배치. 경계 밖·이미 점유·def null 이면 실패(겹침 방지).
        /// </summary>
        public bool TryPlace(Vector2Int cell, NodeDefinition def, out NodeInstance placed)
        {
            placed = null;
            if (def == null) return false;
            if (!IsInside(cell)) return false;
            if (IsOccupied(cell)) return false;
            // TODO(§8): def.implemented==false(쉴드 스텁) 배치 차단 — 배치 가능 노드 필터링은 팔레트/검증 단계 소관.

            placed = new NodeInstance(def, cell);
            _occupancy[cell] = placed;
            return true;
        }

        /// <summary>셀 점유 해제. 비어 있었으면 false.</summary>
        public bool TryRemove(Vector2Int cell)
        {
            return _occupancy.Remove(cell);
        }
    }
}
