using System.Collections.Generic;
using System.IO;
using MBI.Combat;
using MBI.Data;
using MBI.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBI.Editor
{
    /// <summary>
    /// 통합 게임 씬 생성(UI 문서 2장 — 두 레이어를 한 씬에서 슬라이드로). 메뉴: MBI/Create Game Scene.
    ///
    /// 전투(레이어1, 원점) + 물류 보드(레이어2, 하단 오프셋) + GameLayerController(카메라 슬라이드)를
    /// 한 씬에 둔다. 전투는 항상 구동(연속성), 슬라이드로 물류 보드 진입 — 씬 로딩 없음.
    /// 씬 YAML 수기 편집 금지, EditorSceneManager로만(§3, 기존 씬 생성기와 동일 패턴).
    /// 실행 전 'MBI/Generate Combat Data'로 SO 생성 필요(없으면 경고).
    /// </summary>
    public static class GameSceneCreator
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string ScenePath = ScenesDir + "/Game.unity";
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string TuningPath = SoRoot + "/CombatTuning.asset";
        private static readonly Vector2 BoardCenter = new Vector2(0f, -20f);

        [MenuItem("MBI/Create Game Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 8f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            }

            // 레이어 컨트롤러(카메라 슬라이드).
            var layerGo = new GameObject("GameLayer");
            EditorSceneManager.MoveGameObjectToScene(layerGo, scene);
            var layer = layerGo.AddComponent<GameLayerController>();
            layer.combatCenter = Vector2.zero;
            layer.combatSize = 8f;
            layer.boardCenter = BoardCenter;
            layer.boardSize = 5f;
            var layerSo = new SerializedObject(layer);
            layerSo.FindProperty("cam").objectReferenceValue = cam;
            layerSo.ApplyModifiedPropertiesWithoutUndo();

            BuildCombat(scene);
            BuildBoard(scene);

            AssetDatabase.SaveAssets();
            EnsureFolder(ScenesDir);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(saved
                ? $"[MBI] 통합 게임 씬 생성: {ScenePath} — 전투(원점)+물류 보드({BoardCenter}) 슬라이드. Play → ▼조립 버튼."
                : $"[MBI] 통합 게임 씬 저장 실패: {ScenePath}");
        }

        // 레이어1 — 전투(원점). CombatSceneCreator와 동일한 SO 주입.
        private static void BuildCombat(Scene scene)
        {
            var go = new GameObject("StageRunner");
            go.transform.position = Vector3.zero;
            EditorSceneManager.MoveGameObjectToScene(go, scene);
            var runner = go.AddComponent<StageRunner>();

            RobotDefinition robot = Load<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            StageDefinition stage = Load<StageDefinition>($"{SoRoot}/Stages/Stage_S1.asset");
            EnemyDefinition[] enemies =
            {
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_infantry.asset"),
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_artillery.asset"),
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_armor.asset"),
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_boss.asset"),
            };
            if (robot == null || stage == null)
                Debug.LogWarning("[MBI] Robot_A/Stage_S1 없음 — 'MBI/Generate Combat Data' 먼저.");

            var so = new SerializedObject(runner);
            so.FindProperty("robot").objectReferenceValue = robot;
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("tuning").objectReferenceValue = LoadOrCreateTuning();
            SerializedProperty cat = so.FindProperty("enemyCatalog");
            cat.arraySize = 0;
            foreach (EnemyDefinition e in enemies)
            {
                if (e == null) continue;
                int idx = cat.arraySize;
                cat.arraySize = idx + 1;
                cat.GetArrayElementAtIndex(idx).objectReferenceValue = e;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 레이어2 — 물류 보드(하단 오프셋). LogisticsSceneCreator와 동일한 주입.
        private static void BuildBoard(Scene scene)
        {
            var boardRoot = new GameObject("BoardRoot");
            boardRoot.transform.position = new Vector3(BoardCenter.x, BoardCenter.y, 0f);
            EditorSceneManager.MoveGameObjectToScene(boardRoot, scene);

            var controller = boardRoot.AddComponent<BoardController>();
            BoardConfig boardConfig = BoardConfigGenerator.LoadOrCreate();
            var so = new SerializedObject(controller);
            so.FindProperty("config").objectReferenceValue = boardConfig;
            var coreNode = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_core.asset");
            if (coreNode != null) so.FindProperty("placeTarget").objectReferenceValue = coreNode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static CombatTuning LoadOrCreateTuning()
        {
            CombatTuning t = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<CombatTuning>();
                AssetDatabase.CreateAsset(t, TuningPath);
            }
            return t;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
