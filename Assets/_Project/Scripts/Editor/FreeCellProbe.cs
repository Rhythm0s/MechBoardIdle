using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// **빈 칸이 어디인가** — 쉴드 줄을 놓을 자리를 짐작하지 않으려고 센다
    /// (2026-09-17 · `260917_W07` 4장 「칸이 모자라면 놓지 말고 보고한다」).
    /// </summary>
    public static class FreeCellProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                    PartLayout.BuildMask(), owner);
                if (owner == MountOwner.RobotB) StartingBoardB.Apply(g, Node);
                else { StartingBoard.Apply(g, Node); StartingBoard.Place(g, StartingBoard.FillsEmptySlot); }

                var free = new List<Vector2Int>();
                HashSet<Vector2Int> mask = PartLayout.BuildMask();
                for (int y = PartLayout.Rows - 1; y >= 0; y--)
                for (int x = 0; x < PartLayout.Columns; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (!mask.Contains(c)) continue;
                    if (g.GetAt(c) != null || g.GetBeltAt(c) != null) continue;
                    free.Add(c);
                }

                sb.AppendLine($"=== {owner} · 빈 칸 {free.Count} ===");
                for (int y = PartLayout.Rows - 1; y >= 0; y--)
                {
                    var row = new StringBuilder($"y={y,2} ");
                    for (int x = 0; x < PartLayout.Columns; x++)
                    {
                        var c = new Vector2Int(x, y);
                        if (!mask.Contains(c)) { row.Append(" ·"); continue; }
                        if (g.GetAt(c) != null) { row.Append(" N"); continue; }
                        if (g.GetBeltAt(c) != null) { row.Append(" b"); continue; }
                        row.Append(" _");
                    }
                    sb.AppendLine(row.ToString());
                }
            }
            return sb.ToString();
        }
    }
}
