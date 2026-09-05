using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 보드 격자 규격(조립 시스템 문서 11장, 2026-08-24 개정).
    /// 구 규격 64칸 → 117칸. 「정답 배치 하나만 성립하고 대안 경로가 나오지 않으면
    /// 최적화할 여지 자체가 사라진다」가 개정 근거이므로, 칸 수와 실루엣 형태가 계약이다.
    /// </summary>
    public sealed class PartLayoutTests
    {
        // ---- 11-2 파츠별 격자 (확정 표) ----

        [Test]
        public void Silhouette_Is12By13()
        {
            Assert.AreEqual(12, PartLayout.Columns);
            Assert.AreEqual(13, PartLayout.Rows);
        }

        [Test]
        public void PartSizes_MatchConfirmedTable()
        {
            var expected = new Dictionary<RobotPart, Vector2Int>
            {
                { RobotPart.Torso, new Vector2Int(6, 6) },
                { RobotPart.ArmR, new Vector2Int(3, 5) },
                { RobotPart.ArmL, new Vector2Int(3, 5) },
                { RobotPart.LegR, new Vector2Int(3, 4) },
                { RobotPart.LegL, new Vector2Int(3, 4) },
                { RobotPart.ShoulderR, new Vector2Int(3, 3) },
                { RobotPart.ShoulderL, new Vector2Int(3, 3) },
                { RobotPart.Head, new Vector2Int(3, 3) },
            };

            Assert.AreEqual(8, PartLayout.Parts.Count, "파츠 8개");
            foreach (PartRect p in PartLayout.Parts)
                Assert.AreEqual(expected[p.part], p.size, $"{p.part} 격자");
        }

        [Test]
        public void TotalValidCells_Is117()
        {
            int sum = 0;
            foreach (PartRect p in PartLayout.Parts) sum += p.Cells;

            Assert.AreEqual(117, sum, "36 + 15×2 + 12×2 + 9×2 + 9");
            Assert.AreEqual(117, PartLayout.ValidCells);
            Assert.AreEqual(117, PartLayout.BuildMask().Count, "마스크 셀 수도 같아야 한다");
        }

        /// <summary>
        /// 실루엣은 직사각형이 아니다. 12 × 13 = 156칸 중 39칸은 팔·다리 사이 빈 공간이다 —
        /// 이 차이가 0이 되면 격자가 다시 직사각형이 된 것이고, 개정 취지가 사라진 것이다.
        /// </summary>
        [Test]
        public void SilhouetteIsNotRectangular_39CellsAreVoid()
        {
            Assert.AreEqual(39, PartLayout.Columns * PartLayout.Rows - PartLayout.ValidCells);
        }

        /// <summary>최소 폭 3칸 원칙(11-2): 폭 2칸이면 병합기·분류기로 갈라질 자리가 없다.</summary>
        [Test]
        public void EveryPart_IsAtLeastThreeWideAndTall()
        {
            foreach (PartRect p in PartLayout.Parts)
            {
                Assert.GreaterOrEqual(p.size.x, 3, $"{p.part} 가로");
                Assert.GreaterOrEqual(p.size.y, 3, $"{p.part} 세로");
            }
        }

        [Test]
        public void Parts_DoNotOverlap_AndStayInsideSilhouette()
        {
            var seen = new HashSet<Vector2Int>();
            foreach (PartRect p in PartLayout.Parts)
            {
                for (int x = p.origin.x; x < p.origin.x + p.size.x; x++)
                for (int y = p.origin.y; y < p.origin.y + p.size.y; y++)
                {
                    var c = new Vector2Int(x, y);
                    Assert.IsTrue(seen.Add(c), $"{p.part}가 {c}에서 다른 파츠와 겹친다");
                    Assert.IsTrue(x >= 0 && x < PartLayout.Columns && y >= 0 && y < PartLayout.Rows,
                        $"{p.part}의 {c}가 실루엣 밖이다");
                }
            }
        }

        /// <summary>배치가 산술로 잠긴다는 확인 — 세 변이 모두 딱 떨어진다.</summary>
        [Test]
        public void Arrangement_IsForcedByDimensions()
        {
            Assert.AreEqual(12, 3 + 6 + 3, "팔R + 몸통 + 팔L = 가로 12 (L·R은 로봇 기준)");
            Assert.AreEqual(13, 3 + 6 + 4, "머리 + 몸통 + 다리 = 세로 13");
            Assert.AreEqual(6, 3 + 3, "다리R + 다리L = 몸통 폭 6");
        }

        // ---- 격자 연동 ----

        private static BoardGrid MaskedGrid() =>
            new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero, PartLayout.BuildMask());

        [Test]
        public void Grid_RejectsVoidCells_ButAcceptsPartCells()
        {
            BoardGrid grid = MaskedGrid();

            Assert.IsTrue(grid.IsInside(new Vector2Int(5, 6)), "몸통 한가운데는 유효");
            Assert.IsTrue(grid.IsInside(new Vector2Int(0, 5)), "팔R(화면 왼쪽)은 유효");

            // (0, 0)은 실루엣 사각 안이지만 다리(x 3~8)가 아니라 빈 공간이다.
            Assert.IsFalse(grid.IsInside(new Vector2Int(0, 0)), "팔 아래 빈칸은 무효");
            Assert.IsTrue(grid.IsInBounds(new Vector2Int(0, 0)), "다만 실루엣 사각 안이기는 하다");

            Assert.AreEqual(117, grid.ValidCellCount);
        }

        [Test]
        public void Grid_CannotPlaceOnVoidCell()
        {
            BoardGrid grid = MaskedGrid();
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.implemented = true;
            def.ports = new List<NodePort>();

            Assert.IsFalse(grid.TryPlace(new Vector2Int(0, 0), def, out _), "빈 공간에는 못 놓는다");
            Assert.IsTrue(grid.TryPlace(new Vector2Int(5, 6), def, out _), "몸통에는 놓인다");

            Object.DestroyImmediate(def);
        }

        /// <summary>마스크를 주지 않으면 종전대로 직사각 전체가 유효하다(기존 테스트 보호).</summary>
        [Test]
        public void Grid_WithoutMask_KeepsRectangularBehaviour()
        {
            var grid = new BoardGrid(8, 8, 1f, Vector2.zero);

            Assert.IsTrue(grid.IsInside(new Vector2Int(0, 0)));
            Assert.AreEqual(64, grid.ValidCellCount);
        }

        // ---- 파츠 소속 태그 ----

        [Test]
        public void PartAt_ReportsOwningPart_OrNoneForVoid()
        {
            Assert.AreEqual(RobotPart.Torso, PartLayout.PartAt(new Vector2Int(5, 6)));
            Assert.AreEqual(RobotPart.Head, PartLayout.PartAt(new Vector2Int(5, 11)));
            // L·R은 **로봇 기준**이다 — x가 작은 쪽이 화면 왼쪽이고 로봇의 오른쪽이다.
            Assert.AreEqual(RobotPart.LegR, PartLayout.PartAt(new Vector2Int(3, 0)));
            Assert.AreEqual(RobotPart.LegL, PartLayout.PartAt(new Vector2Int(6, 0)));
            Assert.AreEqual(RobotPart.None, PartLayout.PartAt(new Vector2Int(0, 0)));
        }

        /// <summary>
        /// 11-3 파츠 경계 통과: 몸통과 팔이 실제로 맞닿아 있어야 벨트가 넘어갈 수 있다.
        /// 사이에 무효 셀이 끼면 경계 통과가 성립하지 않는다.
        /// </summary>
        [Test]
        public void TorsoAndArms_AreAdjacent_SoBeltsCanCross()
        {
            // 몸통 좌단 x=3, 팔R 우단 x=2 — y가 겹치는 구간에서 맞닿는다.
            // (팔R이 화면 왼쪽이다 — L·R은 로봇 기준이다)
            Assert.AreEqual(RobotPart.ArmR, PartLayout.PartAt(new Vector2Int(2, 5)));
            Assert.AreEqual(RobotPart.Torso, PartLayout.PartAt(new Vector2Int(3, 5)));

            Assert.AreEqual(RobotPart.Torso, PartLayout.PartAt(new Vector2Int(8, 5)));
            Assert.AreEqual(RobotPart.ArmL, PartLayout.PartAt(new Vector2Int(9, 5)));
        }

        /// <summary>몸통과 다리도 맞닿아야 한다(생산 허브 → 보조 파츠 라인).</summary>
        [Test]
        public void TorsoAndLegs_AreAdjacent()
        {
            Assert.AreEqual(RobotPart.Torso, PartLayout.PartAt(new Vector2Int(5, 4)));
            Assert.AreEqual(RobotPart.LegR, PartLayout.PartAt(new Vector2Int(5, 3)));
        }
    }
}
