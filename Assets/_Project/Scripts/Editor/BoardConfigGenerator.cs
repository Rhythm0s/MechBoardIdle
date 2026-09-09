using System.IO;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 물류 보드 레이아웃 설정 자산 생성(§5-3). 메뉴: MBI/Create Board Config.
    ///
    /// 값은 BoardConfig의 직렬화 기본값(레이아웃 placeholder, §3) — 여기서 리터럴을 두지 않는다.
    /// 재실행/재사용 시 같은 경로 자산을 덮지 않고 그대로 반환해 GUID·인스펙터 조정을 보존한다.
    /// LogisticsSceneCreator가 LoadOrCreate를 재사용 → 메뉴 실행 순서와 무관하게 null 참조 없음.
    /// </summary>
    public static class BoardConfigGenerator
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        public const string ConfigPath = SoRoot + "/BoardConfig.asset";

        [MenuItem("MBI/Create Board Config")]
        public static void Create()
        {
            BoardConfig cfg = LoadOrCreate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = cfg;
            Debug.Log($"[MBI] BoardConfig 준비 완료: {ConfigPath} ({cfg.columns}×{cfg.rows}, cellSize {cfg.cellSize}).");
        }

        // ── 보드 아트 배선 (2026-09-09 · 보드·품목 승인분 배선) ─────────────────────────
        //
        // ⚠️ **아트 경로가 사는 곳은 여기 하나다**(§8 명명 규칙 · `CombatAssetGenerator`의
        // `LoadArt`·`LoadVfx`와 같은 길). 런타임은 <see cref="BoardArtSet"/> 참조만 들고,
        // 폴더를 옮기거나 파일 이름을 바꿔도 고칠 자리가 이 함수 둘뿐이다.
        //
        // ⚠️ **없는 파일은 조용히 null이다.** 아직 안 온 그림을 에러로 만들면 보드가
        // 통째로 안 뜬다 — 있는 것부터 붙고 나머지는 색 사각으로 남는 편이 낫다.

        public const string ArtPath = SoRoot + "/BoardArtSet.asset";

        [MenuItem("MBI/Create Board Art Set")]
        public static void CreateArt()
        {
            BoardArtSet art = LoadOrCreateArt();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = art;
            Debug.Log($"[MBI] BoardArtSet 준비 완료: {ArtPath} — " +
                      $"노드 {art.FilledNodeCount} · 부속 {art.FilledPartCount} · 품목 {art.FilledItemCount}.");
        }

        /// <summary>
        /// BoardArtSet.asset을 로드하거나 만들고 **매번 다시 채운다.**
        ///
        /// 덮어쓰는 이유: 이 자산에는 손으로 조정할 값이 없고 파일 이름 대응표뿐이다.
        /// 새 그림이 폴더에 들어왔을 때 메뉴 한 번으로 붙어야 한다.
        /// </summary>
        public static BoardArtSet LoadOrCreateArt()
        {
            BoardArtSet art = AssetDatabase.LoadAssetAtPath<BoardArtSet>(ArtPath);
            if (art == null)
            {
                EnsureDir(SoRoot);
                art = ScriptableObject.CreateInstance<BoardArtSet>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            art.nodes.Clear();
            AddNode(art, NodeType.Core, "node_core");
            AddNode(art, NodeType.Processing, "node_processing");
            AddNode(art, NodeType.MunitionsBasic, "node_muni_basic");
            AddNode(art, NodeType.MunitionsComplex, "node_muni_complex");
            AddNode(art, NodeType.Energy, "node_energy");
            AddNode(art, NodeType.Storage, "node_storage");
            AddNode(art, NodeType.Booster, "node_booster");
            // 쉴드(NodeType.Shield)는 스텁이라 그림이 없다 — 자리도 두지 않는다.

            art.beltStraight = LoadBoard("belt_straight");
            art.beltCorner = LoadBoard("belt_corner");
            art.beltEnd = LoadBoard("belt_end");
            art.merger = LoadBoard("merger");
            art.sorter = LoadBoard("sorter");

            art.portInput = LoadBoard("port_input");
            art.portOutput = LoadBoard("port_output");
            art.portPower = LoadBoard("port_power");
            art.mountPort = LoadBoard("mount_port");

            art.items.Clear();
            AddItem(art, FlowKind.CoreEnergy, "core_energy");
            AddItem(art, FlowKind.BasicParts, "basic_parts");
            AddItem(art, FlowKind.PowerMaterial, "power_material");
            AddItem(art, FlowKind.Battery, "battery");
            AddItem(art, FlowKind.StandardAmmo, "ammo_standard");
            AddItem(art, FlowKind.DroneBodyParts, "drone_body_parts");
            AddItem(art, FlowKind.DefenseMaterial, "defense_material");
            AddItem(art, FlowKind.PierceAmmo, "ammo_pierce");
            AddItem(art, FlowKind.ExplosiveAmmo, "ammo_explosive");
            AddItem(art, FlowKind.Propellant, "propellant");
            // ⚠️ **누적형 드론·광역형 드론에는 파일이 없다**(2026-09-09 실측 — `Art/Items/`에 없다).
            // 지어 넣지 않는다. 벨트에서는 색 점으로 흐르고, 회신문에 자리로 올린다.

            EditorUtility.SetDirty(art);
            return art;
        }

        private static void AddNode(BoardArtSet art, NodeType type, string fileName)
        {
            Sprite s = LoadBoard(fileName);
            if (s == null) return; // 없는 그림은 자리도 안 만든다 — 빈 칸이 「있는데 비었다」로 읽힌다
            art.nodes.Add(new BoardArtSet.NodeArt { type = type, sprite = s });
        }

        private static void AddItem(BoardArtSet art, FlowKind kind, string fileName)
        {
            Sprite s = LoadItem(fileName);
            if (s == null) return;
            art.items.Add(new BoardArtSet.ItemArt { kind = kind, sprite = s });
        }

        private static Sprite LoadBoard(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Board/{fileName}.png");

        private static Sprite LoadItem(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Items/{fileName}.png");

        /// <summary>BoardConfig.asset을 로드하거나 없으면 기본값으로 생성해 반환(씬 생성기가 재사용).</summary>
        public static BoardConfig LoadOrCreate()
        {
            BoardConfig cfg = AssetDatabase.LoadAssetAtPath<BoardConfig>(ConfigPath);
            if (cfg == null)
            {
                EnsureDir(SoRoot);
                cfg = ScriptableObject.CreateInstance<BoardConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }

            // 실루엣 치수는 조립 시스템 문서 11장 확정값이라 인스펙터 조정 대상이 아니다.
            // 직렬화 기본값은 **새 자산에만** 적용되므로, 구 8×8 자산이 남아 있으면 파츠 레이아웃
            // (12×13)과 어긋나 유효 셀 마스크가 실루엣 밖으로 잘린다 — 기존 자산은 여기서 맞춘다.
            if (cfg.columns != PartLayout.Columns || cfg.rows != PartLayout.Rows)
            {
                cfg.columns = PartLayout.Columns;
                cfg.rows = PartLayout.Rows;
                EditorUtility.SetDirty(cfg);
            }
            return cfg;
        }

        private static void EnsureDir(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
