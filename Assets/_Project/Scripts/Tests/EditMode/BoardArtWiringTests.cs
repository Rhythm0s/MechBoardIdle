using System.Collections.Generic;
using System.IO;
using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 보드·품목 아트 배선(2026-09-09 · `260908_W09` 뒤 · 플랜 §58-3 ②).
    ///
    /// **이 시험이 잡는 것은 「그림이 있는데 안 붙은 자리」다.** 승인·설치까지 끝난 파일이
    /// 폴더에 있는데 생성기가 그 칸을 비워 두면 화면에는 색 사각이 남는다 —
    /// 에러가 나지 않아 **눈으로 보기 전에는 모른다.**
    ///
    /// ⚠️ 반대로 **아직 안 온 그림을 여기서 요구하지 않는다** — 파일이 없으면 그 항목은 건너뛴다.
    /// 없는 것을 실패로 적으면 시험이 **자산 도착 알림**이 되고, 그러면 「안 만들기로 한 것」과
    /// 「만들려다 못 한 것」이 같은 빨간불로 뜬다(지침 §7 ［09-07］). 도착 여부는 회신문이 나른다.
    ///
    /// ⚠️ **드론 둘은 폴더가 다르다** — `Art/Items/`에 없고 승인본(`Art/Units/`)을 그대로 쓴다.
    /// 처음에 품목 폴더만 훑고 「없다」로 적을 뻔한 자리다(자산 레지스트리 「신규 10 + 재사용 2」).
    ///
    /// 배향(회전)이 맞는지는 여기서 못 본다 — 배치모드는 화면을 안 그린다. 그것은 육안이다.
    /// </summary>
    public sealed class BoardArtWiringTests
    {
        private const string BoardDir = "Assets/_Project/Art/Board";
        private const string ItemDir = "Assets/_Project/Art/Items";

        private static BoardArtSet Build() => BoardConfigGenerator.LoadOrCreateArt();

        private static bool FileExists(string dir, string fileName)
            => File.Exists(Path.Combine(Directory.GetCurrentDirectory(), dir, fileName + ".png"));

        /// <summary>노드 종류 ↔ 파일 이름. 대응이 틀리면 **다른 노드 그림이 조용히 붙는다.**</summary>
        private static readonly (NodeType type, string file)[] NodeFiles =
        {
            (NodeType.Core, "node_core"),
            (NodeType.Processing, "node_processing"),
            (NodeType.MunitionsBasic, "node_muni_basic"),
            (NodeType.MunitionsComplex, "node_muni_complex"),
            (NodeType.Energy, "node_energy"),
            (NodeType.Storage, "node_storage"),
            (NodeType.Booster, "node_booster"),
        };

        private static readonly (FlowKind kind, string file)[] ItemFiles =
        {
            (FlowKind.CoreEnergy, "core_energy"),
            (FlowKind.BasicParts, "basic_parts"),
            (FlowKind.PowerMaterial, "power_material"),
            (FlowKind.Battery, "battery"),
            (FlowKind.StandardAmmo, "ammo_standard"),
            (FlowKind.DroneBodyParts, "drone_body_parts"),
            (FlowKind.DefenseMaterial, "defense_material"),
            (FlowKind.PierceAmmo, "ammo_pierce"),
            (FlowKind.ExplosiveAmmo, "ammo_explosive"),
            (FlowKind.Propellant, "propellant"),
        };

        /// <summary>승인본을 그대로 쓰는 품목 둘 — 폴더가 다르다(자산 레지스트리 「신규 10 + 재사용 2」).</summary>
        private static readonly (FlowKind kind, string file)[] UnitItemFiles =
        {
            (FlowKind.StackDrone, "drone_n"),
            (FlowKind.AoeDrone, "drone_w"),
        };

        private const string UnitDir = "Assets/_Project/Art/Units";

        [Test]
        public void EveryNodeArtOnDisk_IsWiredToItsType()
        {
            BoardArtSet art = Build();
            foreach ((NodeType type, string file) in NodeFiles)
            {
                if (!FileExists(BoardDir, file)) continue; // 아직 안 온 그림은 요구하지 않는다
                Assert.IsNotNull(art.NodeSprite(type),
                    $"{file}.png는 있는데 {type} 칸이 비었다 — 화면에는 색 사각이 남는다");
            }
        }

        [Test]
        public void EveryItemArtOnDisk_IsWiredToItsKind()
        {
            BoardArtSet art = Build();
            foreach ((FlowKind kind, string file) in ItemFiles)
            {
                if (!FileExists(ItemDir, file)) continue;
                Assert.IsNotNull(art.ItemSprite(kind),
                    $"{file}.png는 있는데 {kind} 칸이 비었다");
            }
        }

        /// <summary>
        /// 드론 둘은 <b>승인본 폴더에서</b> 온다. 품목 폴더만 훑으면 「자산이 없다」로 읽혀
        /// 안 만들기로 한 것과 못 만든 것이 섞인다(지침 §7 ［09-07］).
        /// </summary>
        [Test]
        public void DroneItems_ComeFromTheApprovedUnits()
        {
            BoardArtSet art = Build();
            foreach ((FlowKind kind, string file) in UnitItemFiles)
            {
                if (!FileExists(UnitDir, file)) continue;
                Assert.IsNotNull(art.ItemSprite(kind),
                    $"{file}.png는 있는데 {kind} 칸이 비었다 — 벨트에서 색 점으로 흐른다");
            }
        }

        [Test]
        public void EveryItemKindThatFlowsOnBelts_HasArt()
        {
            // 품목 열둘 = 신규 열 + 승인본 재사용 둘. 하나라도 비면 그 품목만 색 점이 되어
            // **한 벨트 위에서 그림과 점이 섞인다** — 무엇이 안 온 것인지가 화면에서 안 읽힌다.
            BoardArtSet art = Build();
            int filled = 0;
            foreach ((FlowKind kind, string _) in ItemFiles) if (art.ItemSprite(kind) != null) filled++;
            foreach ((FlowKind kind, string _) in UnitItemFiles) if (art.ItemSprite(kind) != null) filled++;
            Assert.AreEqual(ItemFiles.Length + UnitItemFiles.Length, filled,
                "벨트를 흐르는 품목 열둘이 다 붙어야 한다");
        }

        [Test]
        public void EveryBoardPartOnDisk_IsWired()
        {
            BoardArtSet art = Build();
            var parts = new Dictionary<string, Sprite>
            {
                { "belt_straight", art.beltStraight },
                { "belt_corner", art.beltCorner },
                { "belt_end", art.beltEnd },
                { "merger", art.merger },
                { "sorter", art.sorter },
                { "port_input", art.portInput },
                { "port_output", art.portOutput },
                { "port_power", art.portPower },
                { "mount_port", art.mountPort },
            };
            foreach (KeyValuePair<string, Sprite> kv in parts)
            {
                if (!FileExists(BoardDir, kv.Key)) continue;
                Assert.IsNotNull(kv.Value, $"{kv.Key}.png는 있는데 그 칸이 비었다");
            }
        }

        [Test]
        public void NodeArt_DoesNotPointAtTheWrongType()
        {
            // 같은 그림이 두 종류에 붙으면 화면에서 둘이 구별되지 않는다 —
            // 대응표를 손으로 옮기다 한 줄을 복사한 자리가 이렇게 생긴다.
            BoardArtSet art = Build();
            var seen = new Dictionary<Sprite, NodeType>();
            foreach ((NodeType type, string _) in NodeFiles)
            {
                Sprite s = art.NodeSprite(type);
                if (s == null) continue;
                Assert.IsFalse(seen.ContainsKey(s),
                    $"{type}와 {(seen.ContainsKey(s) ? seen[s].ToString() : "?")}가 같은 그림을 쓴다");
                seen[s] = type;
            }
        }

        [Test]
        public void BoardTiles_AreOneCellWide()
        {
            // 타일 한 장 = 한 칸이라야 격자와 월드가 1:1로 붙는다(ArtSpec 주석).
            // 여기서 어긋나면 보드가 조금씩 밀리는데, 밀림은 에러가 아니라 화면에서만 보인다.
            BoardArtSet art = Build();
            foreach ((NodeType type, string file) in NodeFiles)
            {
                Sprite s = art.NodeSprite(type);
                if (s == null) continue;
                Assert.AreEqual(1f, s.bounds.size.x, 0.01f, $"{file}: 가로가 한 칸이 아니다");
                Assert.AreEqual(1f, s.bounds.size.y, 0.01f, $"{file}: 세로가 한 칸이 아니다");
            }
        }

        // ── 배경 셋 (2026-09-09 배선) ─────────────────────────────────────────────

        private const string BackgroundDir = "Assets/_Project/Art/Backgrounds";

        /// <summary>
        /// 배경 셋이 SO 자리 셋에 걸렸는가. **셋이 SO 둘에 나뉘어 있어** 한쪽만 붙어도
        /// 화면의 절반은 멀쩡해 보인다 — 전투는 깔리는데 보드만 안 깔린 것을 눈으로 잡으려면
        /// 두 화면을 다 열어야 한다.
        /// </summary>
        [Test]
        public void CombatBackgrounds_AreWired()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            Assert.IsNotNull(tuning, "CombatTuning.asset이 없다 — 생성기를 먼저 돌린다");

            if (FileExists(BackgroundDir, "bg_combat"))
                Assert.IsNotNull(tuning.combatBackgroundSprite, "bg_combat.png는 있는데 자리가 비었다");
            if (FileExists(BackgroundDir, "bg_combat_boss"))
                Assert.IsNotNull(tuning.bossBackgroundSprite, "bg_combat_boss.png는 있는데 자리가 비었다");
        }

        [Test]
        public void BoardBackground_IsWired()
        {
            BoardArtSet art = Build();
            if (!FileExists(BackgroundDir, "bg_board")) return;
            Assert.IsNotNull(art.boardBackground, "bg_board.png는 있는데 자리가 비었다");
        }

        [Test]
        public void BoardBackground_IsExactlyOneCell()
        {
            // 캔버스 192 = 한 칸이라 **격자와 그림이 같은 눈금**이다. 어긋나면 칸마다
            // 조금씩 밀려 보드 전체가 흐트러지는데, 그 밀림은 에러가 아니라 화면에서만 보인다.
            BoardArtSet art = Build();
            if (art.boardBackground == null) return;
            Assert.AreEqual(1f, art.boardBackground.bounds.size.x, 0.01f);
            Assert.AreEqual(1f, art.boardBackground.bounds.size.y, 0.01f);
        }

        [Test]
        public void CombatBackground_IsBiggerThanOneCell_SoTilingIsCheap()
        {
            // 캔버스 256 = 1.333칸. 한 칸짜리로 착각해 깔면 장수가 배로 늘고,
            // 그것을 매 프레임 미는 것이라 값이 그대로 프레임 비용이 된다.
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            if (tuning == null || tuning.combatBackgroundSprite == null) return;
            Assert.Greater(tuning.combatBackgroundSprite.bounds.size.x, 1f);
        }

        [Test]
        public void BackgroundLayer_SitsBelowTheArenaDisc()
        {
            // 원반은 「여기까지 움직일 수 있다」는 경계 표시라 **바닥 위**여야 한다.
            // 같은 층에 두면 어느 쪽이 위인지가 정해지지 않는다.
            Assert.Less(SortingLayers.BackgroundFar, SortingLayers.Background);
            Assert.AreEqual(SortingLayers.Step, SortingLayers.Background - SortingLayers.BackgroundFar,
                "층 간격 10을 지킨다 — ±1~9는 같은 층 안의 미세 조정 몫이다");
        }

        [Test]
        public void ShieldNode_HasNoArt_AndThatIsCorrect()
        {
            // 쉴드는 스텁이다(NodeDefinition.implemented=false). 그림이 없는 것이 맞고,
            // 나중에 누가 채워 넣으면 **구현 안 된 노드가 보드에 그려진다.**
            BoardArtSet art = Build();
            Assert.IsNull(art.NodeSprite(NodeType.Shield));
        }
    }
}
