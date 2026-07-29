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
