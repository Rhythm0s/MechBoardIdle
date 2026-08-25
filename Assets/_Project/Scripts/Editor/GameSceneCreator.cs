using System.Collections.Generic;
using System.IO;
using MBI.Combat;
using MBI.Data;
using MBI.Idle;
using MBI.Logistics;
using MBI.UI;
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

            // 방치 런타임(§5-7): 세이브 로드·주기 저장. 실행 순서가 앞서므로 다른 컴포넌트가
            // Start에서 세이브를 읽어도 이미 로드가 끝나 있다.
            var idleGo = new GameObject("IdleRuntime");
            EditorSceneManager.MoveGameObjectToScene(idleGo, scene);
            var idle = idleGo.AddComponent<IdleRuntime>();
            var iso = new SerializedObject(idle);
            iso.FindProperty("economy").objectReferenceValue =
                Load<EconomyConfig>($"{SoRoot}/EconomyConfig.asset");
            iso.FindProperty("balance").objectReferenceValue =
                Load<BalanceConfig>($"{SoRoot}/BalanceConfig.asset");
            iso.ApplyModifiedPropertiesWithoutUndo();

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
            var stageList = new List<StageDefinition>();
            foreach (string id in new[] { "S1", "S2", "S3", "S4", "S5", "S6" })
            {
                StageDefinition sd = Load<StageDefinition>($"{SoRoot}/Stages/Stage_{id}.asset");
                if (sd != null) stageList.Add(sd);
            }
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

            // 자동 전투 진행(§5-7): 전투가 끝나면 스스로 다음 판을 건다.
            // 사람이 "다시"를 눌러야 이어지면 방치형이 아니다.
            var auto = go.AddComponent<AutoBattleController>();
            var aso = new SerializedObject(auto);
            aso.FindProperty("runner").objectReferenceValue = runner;
            aso.FindProperty("tuning").objectReferenceValue = LoadOrCreateTuning();
            SerializedProperty sl = aso.FindProperty("stages");
            sl.arraySize = stageList.Count;
            for (int i = 0; i < stageList.Count; i++)
                sl.GetArrayElementAtIndex(i).objectReferenceValue = stageList[i];
            aso.ApplyModifiedPropertiesWithoutUndo();
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

            // 노드 팔레트(구현 5종 — 쉴드 스텁 제외).
            SerializedProperty pal = so.FindProperty("palette");
            pal.arraySize = 0;
            foreach (string id in new[] { "core", "proc", "muni", "ener", "stor" })
            {
                var n = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_{id}.asset");
                if (n == null) continue;
                int idx = pal.arraySize;
                pal.arraySize = idx + 1;
                pal.GetArrayElementAtIndex(idx).objectReferenceValue = n;
            }
            // 시작 배치(온보딩): 대표 배치에서 군수 한 칸만 비워 둔다.
            // 빈 보드로 시작하면 출력 0 = 전투 정지라 "게임이 고장난 것"처럼 보이고, 반대로 완성된 라인을
            // 주면 물류 보드를 열 이유가 사라진다. 한 칸만 비우면 부족 → 배치 → 출력 상승 → 클리어가 돈다.
            //
            // 대표 배치 = 군수 4개(관통1 · 분열1 · 폭발2) → 20 + 25 + 100 = 145(§9 s3Break).
            // 노드 1개 = 1발/초이므로 노드 수가 곧 pA다(260824_V02 §1).
            // 폭발 하나를 비워 두면 20 + 25 + 50 = 95로 시작하고, 그 칸을 채우면 145가 된다 —
            // "배치가 출력을 올린다"가 보드 위에서 눈에 보이는 구성이다.
            NodeDefinition muni = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_muni.asset");
            SerializedProperty layout = so.FindProperty("initialLayout");
            layout.arraySize = 0;
            AddInitial(layout, new Vector2Int(3, 3), Load<NodeDefinition>($"{SoRoot}/Nodes/Node_core.asset"));
            AddInitial(layout, new Vector2Int(2, 3), Load<NodeDefinition>($"{SoRoot}/Nodes/Node_ener.asset"));
            AddInitial(layout, new Vector2Int(4, 3), muni, AmmoKind.Pierce);
            AddInitial(layout, new Vector2Int(4, 2), muni, AmmoKind.Split);
            AddInitial(layout, new Vector2Int(4, 1), muni, AmmoKind.Explosive);
            // (4, 0) 폭발 두 번째 = 비워 둔 칸.
            so.ApplyModifiedPropertiesWithoutUndo();

            // 라이브 네트워크 → 출력 반영(§5-6): 노드 집계 → 흐름시뮬 → LogisticsOutputBridge.
            var provider = boardRoot.AddComponent<LogisticsOutputProvider>();
            var pso = new SerializedObject(provider);
            pso.FindProperty("board").objectReferenceValue = controller;
            pso.FindProperty("config").objectReferenceValue = LoadOrCreateLogisticsConfig();
            // 명목 출력·원점·천장·탄약 수요의 단일 원천(§5-6 커밋 A) — 리터럴 주입 금지.
            pso.FindProperty("robot").objectReferenceValue = Load<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            pso.ApplyModifiedPropertiesWithoutUndo();

            // 변수 패널(§5-6 커밋 C): 브릿지만 읽어 예상/실제/갭 + 갭 분해를 표시. 판정 없음.
            boardRoot.AddComponent<VariablePanel>();
        }

        // initialLayout 배열에 (셀, 노드, 탄종) 1건 추가. 노드가 없으면 건너뛴다(생성기 미실행 상황).
        // 탄종은 군수 노드에만 의미가 있다 — 기본 관통(origin basis「관통탄 20×5발 기본 라인」).
        private static void AddInitial(SerializedProperty layout, Vector2Int cell, NodeDefinition node,
            AmmoKind ammoKind = AmmoKind.Pierce)
        {
            if (node == null) return;
            int idx = layout.arraySize;
            layout.arraySize = idx + 1;
            SerializedProperty item = layout.GetArrayElementAtIndex(idx);
            item.FindPropertyRelative("cell").vector2IntValue = cell;
            item.FindPropertyRelative("node").objectReferenceValue = node;
            item.FindPropertyRelative("ammoKind").enumValueIndex = (int)ammoKind;
        }

        private static LogisticsConfig LoadOrCreateLogisticsConfig()
        {
            const string path = SoRoot + "/LogisticsConfig.asset";
            LogisticsConfig c = AssetDatabase.LoadAssetAtPath<LogisticsConfig>(path);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<LogisticsConfig>();
                AssetDatabase.CreateAsset(c, path);
            }
            return c;
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
