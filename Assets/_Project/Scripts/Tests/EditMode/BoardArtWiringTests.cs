using System.Collections.Generic;
using System.IO;
using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
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
    /// ⚠️ 반대로 **아직 안 온 그림을 여기서 요구하지 않는다.** 지금 없는 것은 누적형·광역형
    /// 드론 둘이며(2026-09-09 실측), 그것을 실패로 적으면 시험이 **자산 도착 알림**이 된다.
    /// 도착 여부는 회신문이 나른다.
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
