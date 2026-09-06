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

        /// <summary>
        /// 구역 라벨 여덟은 **붙여 쓴다** (2026-09-05 확정 · `260905_W01` · 용어 사전「구역 라벨」).
        ///
        /// 띄어 쓰거나 빗금으로 묶으면(「팔 L / R」) 문서 전수 검색에서 안 걸려 개정이 전파되지
        /// 않는다 — 2026-09-05에 실제로 세 문서를 놓칠 뻔했다. 화면에 그리는 문자열이
        /// 문서 표기와 갈라지면 그 검색이 또 헛돈다.
        /// </summary>
        [Test]
        public void ZoneLabels_AreWrittenWithoutSpaces()
        {
            Assert.AreEqual("팔R", PartLayout.LabelOf(RobotPart.ArmR));
            Assert.AreEqual("팔L", PartLayout.LabelOf(RobotPart.ArmL));
            Assert.AreEqual("어깨R", PartLayout.LabelOf(RobotPart.ShoulderR));
            Assert.AreEqual("다리L", PartLayout.LabelOf(RobotPart.LegL));
            Assert.AreEqual("머리", PartLayout.LabelOf(RobotPart.Head));
            Assert.AreEqual("몸통", PartLayout.LabelOf(RobotPart.Torso));

            foreach (PartRect p in PartLayout.Parts)
            {
                string label = PartLayout.LabelOf(p.part);
                Assert.IsNotEmpty(label, $"{p.part}에 이름표가 없다 — 화면에 빈 구역이 생긴다");
                StringAssert.DoesNotContain(" ", label, $"{p.part}: 라벨에 공백이 있으면 검색이 안 걸린다");
                StringAssert.DoesNotContain("/", label, $"{p.part}: 빗금도 같은 이유로 쓰지 않는다");
            }
        }

        // ---- 구역 경계선 ----

        /// <summary>
        /// 경계 도막이 **한 칸도 겹치지 않아야** 한다.
        ///
        /// 파츠마다 네 변을 그리면 맞닿은 변이 두 번 그려진다. 경계선은 불투명도 40%라
        /// 겹친 자리만 진해지고, 두 파츠의 변 길이가 달라 점선 위상까지 어긋나 그 자리가
        /// 실선처럼 보인다 — 2026-09-06에 실제로 그렇게 그리고 있었다.
        /// </summary>
        [Test]
        public void BoundaryRuns_DoNotOverlap()
        {
            var seen = new HashSet<(Vector2Int, bool)>();
            foreach (PartLayout.BoundaryRun r in PartLayout.BoundaryRuns())
                for (int i = 0; i < r.length; i++)
                {
                    var cell = r.horizontal
                        ? new Vector2Int(r.from.x + i, r.from.y)
                        : new Vector2Int(r.from.x, r.from.y + i);
                    Assert.IsTrue(seen.Add((cell, r.horizontal)),
                        $"{cell}(가로={r.horizontal})가 두 번 그려진다");
                }
        }

        /// <summary>
        /// 합친 뒤 남는 경계는 **89칸**이다. 파츠 여덟의 변을 그냥 더하면 120칸이고,
        /// 차이 31칸이 맞닿아 공유되는 변이다(몸통↔팔·다리↔다리·팔↔어깨 등).
        ///
        /// 이 두 숫자가 같아지면 파츠가 서로 떨어졌다는 뜻이고, 그러면 11-3 파츠 경계 통과가
        /// 성립하지 않는다 — <see cref="TorsoAndArms_AreAdjacent_SoBeltsCanCross"/>와 같은 계약이다.
        /// </summary>
        [Test]
        public void BoundaryRuns_Cover89Edges_Of120Drawn()
        {
            int merged = 0;
            foreach (PartLayout.BoundaryRun r in PartLayout.BoundaryRuns()) merged += r.length;

            int naive = 0;
            foreach (PartRect p in PartLayout.Parts) naive += 2 * (p.size.x + p.size.y);

            Assert.AreEqual(120, naive, "파츠별로 그리면 이만큼");
            Assert.AreEqual(89, merged, "합치면 이만큼 — 차이 31칸이 맞닿은 변이다");
        }

        /// <summary>
        /// 도막은 **이어진 구간**이어야 한다. 한 칸씩 쪼개져 나오면 점선 주기가 칸마다
        /// 새로 시작해 경계가 촘촘한 점선으로 보인다.
        ///
        /// 실루엣 바깥 왼변(팔R x=0 · y 4~9)과 어깨R 왼변(x=0 · y 9~12)은 한 줄로 이어지므로
        /// x=0에는 y 4에서 시작하는 길이 8짜리 도막 하나만 있어야 한다.
        /// </summary>
        [Test]
        public void BoundaryRuns_JoinAcrossParts_OnTheSameLine()
        {
            PartLayout.BoundaryRun found = default;
            int count = 0;
            foreach (PartLayout.BoundaryRun r in PartLayout.BoundaryRuns())
                if (!r.horizontal && r.from.x == 0) { found = r; count++; }

            Assert.AreEqual(1, count, "x=0 왼변은 도막 하나로 이어져야 한다");
            Assert.AreEqual(4, found.from.y, "팔R 아랫끝에서 시작");
            Assert.AreEqual(8, found.length, "팔R 5칸 + 어깨R 3칸");
        }

        /// <summary>
        /// 여덟 구역이 **저마다 다른 이름표**를 가져야 한다.
        ///
        /// 좌우가 같은 이름이면 화면에서 어느 쪽이 로봇의 오른팔인지가 사라지고,
        /// 마운트가 `팔R`에 붙는다는 조립 문서를 읽을 수 없게 된다.
        /// </summary>
        [Test]
        public void ZoneLabels_AreAllDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (PartRect p in PartLayout.Parts)
                Assert.IsTrue(seen.Add(PartLayout.LabelOf(p.part)),
                    $"{p.part}의 이름표가 다른 구역과 겹친다");
            Assert.AreEqual(8, seen.Count);
        }
    }
}
