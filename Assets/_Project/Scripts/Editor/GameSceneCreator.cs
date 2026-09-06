using System.Collections.Generic;
using System.IO;
using MBI.Combat;
using MBI.Core;
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
            idleGo.AddComponent<IdleHud>(); // 오프라인 정산 알림 — 방치 사슬의 유일한 화면 증빙
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
            RobotDefinition robotB = Load<RobotDefinition>($"{SoRoot}/Robots/Robot_B.asset");
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
            so.FindProperty("robotB").objectReferenceValue = robotB; // 태그 상대 — 있어야 태그·합체가 돈다
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

            // ── 스테이지 0 (260901_V05 §3층 · 260901_W05로 영상 A구간 등장 확정) ──────
            // ⚠️ **되돌림 지점은 철회됐다**(260901_W05 §2층). 영상에 나오는 것을 게이트 2에서
            // 되돌릴 수는 없다. 얹는 방식은 그대로 두는데, 되돌리기 위해서가 아니라
            // 진입 경로를 덜 건드리기 위해서다 — 9월 5일 리허설에서 진입이 깨지면 시간이 없다.
            StageDefinition stageZero = CreateStageZero();
            so.FindProperty("stage").objectReferenceValue = stageZero; // 켜면 스테이지 0부터
            so.ApplyModifiedPropertiesWithoutUndo();

            var stage0 = go.AddComponent<Stage0Session>();
            var s0so = new SerializedObject(stage0);
            s0so.FindProperty("runner").objectReferenceValue = runner;
            s0so.FindProperty("nextStage").objectReferenceValue = stage; // 마치면 스테이지 1로
            s0so.ApplyModifiedPropertiesWithoutUndo();

            // ── 심사자용 바로가기 (260901_W04 §3층) ──────────────────────────
            // 빌드에 남긴다. 숨기지 않는다 — 이유는 컴포넌트 주석에 있다.
            var shortcuts = go.AddComponent<ReviewerShortcuts>();
            var rso = new SerializedObject(shortcuts);
            rso.FindProperty("runner").objectReferenceValue = runner;
            rso.FindProperty("stage0").objectReferenceValue = stage0;
            rso.FindProperty("tutorialStage").objectReferenceValue = stageZero;
            SerializedProperty rsl = rso.FindProperty("stages");
            rsl.arraySize = stageList.Count + 1;
            rsl.GetArrayElementAtIndex(0).objectReferenceValue = stageZero; // 맨 앞이 스테이지 0
            for (int i = 0; i < stageList.Count; i++)
                rsl.GetArrayElementAtIndex(i + 1).objectReferenceValue = stageList[i];
            rso.ApplyModifiedPropertiesWithoutUndo();

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

            // 노드 팔레트(구현 6종 — 쉴드 스텁만 제외).
            // ⚠️ 부스터가 빠져 있던 동안에는 **회피를 Play에서 켤 방법이 아예 없었다** —
            // 상한이 부스터 대수의 파생값이라 한 대도 못 놓으면 상한이 영영 0이다.
            SerializedProperty pal = so.FindProperty("palette");
            pal.arraySize = 0;
            foreach (string id in new[] { "core", "proc", "muni", "ener", "stor", "boost" })
            {
                var n = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_{id}.asset");
                if (n == null) continue;
                int idx = pal.arraySize;
                pal.arraySize = idx + 1;
                pal.GetArrayElementAtIndex(idx).objectReferenceValue = n;
            }
            // 시작 배치(온보딩) — **배치는 StartingBoard(MBI.Core)가 쥔다.**
            // 종전에는 여기 좌표 리터럴로만 있어서 「정말 80이 나오는가」를 확인할 방법이
            // 씬을 열어 보는 것뿐이었다. 데이터로 빼면 이 생성기와 테스트가 같은 것을 읽어,
            // 숫자가 어긋나면 배치모드에서 깨진다(StartingBoardTests).
            SerializedProperty layout = so.FindProperty("initialLayout");
            layout.arraySize = 0;
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                AddInitial(layout, slot.cell,
                    Load<NodeDefinition>($"{SoRoot}/Nodes/Node_{slot.nodeId}.asset"), slot.ammo);

            SerializedProperty belts = so.FindProperty("initialBelts");
            belts.arraySize = 0;
            foreach (StartingBoard.Run run in StartingBoard.Belts)
                AddBelt(belts, run.cell, run.inFace, run.outFace, run.merger);

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
        /// <summary>
        /// 스테이지 0 데이터 — **적이 없는 스테이지**(260901_V05 §3층).
        ///
        /// ⚠️ 적을 안 나오게 하는 것은 `Endless`가 아니다. `Endless`는 승패 **판정**만 막고
        /// 스폰은 그대로 돈다 — 브라우저 실측에서 적 40기가 나왔다(2026-09-01).
        /// 스폰을 없애는 유일한 자리는 **스테이지의 몬스터 구성**이다.
        ///
        /// `balance_v4.json`에는 넣지 않는다. 스테이지 0은 요구치가 없어 그 표의 축과 맞지 않고,
        /// 여기서 만들면 되돌릴 때 이 메서드까지 한 블록으로 지워진다.
        /// </summary>
        private static StageDefinition CreateStageZero()
        {
            const string path = SoRoot + "/Stages/Stage_S0.asset";
            StageDefinition s = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<StageDefinition>();
                AssetDatabase.CreateAsset(s, path);
            }

            var so = new SerializedObject(s);
            so.FindProperty("stageId").stringValue = "S0";
            so.FindProperty("topic").stringValue = "벨트 — 이으면 만들어진다";
            so.FindProperty("reqType").enumValueIndex = (int)StageReqType.Fixed;
            so.FindProperty("req").floatValue = 0f;          // 요구치 없음 — 전투가 없다
            // ⚠️ 0이 아니라 큰 값이다. 0이면 첫 틱에 「시간 초과」가 성립해 패배 판정으로 샌다 —
            // 전투가 없다는 것은 이기지도 지지도 않는다는 뜻이다.
            so.FindProperty("challengeTime").floatValue = 9999f;
            so.FindProperty("composition").arraySize = 0;     // **적 0기**
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            return s;
        }

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

        // initialBelts 배열에 벨트 1칸 추가. 병합기는 면을 이웃에서 다시 잡으므로 면 인자를 무시한다.
        private static void AddBelt(SerializedProperty belts, Vector2Int cell,
            PortFace inFace = PortFace.West, PortFace outFace = PortFace.East, bool merger = false)
        {
            int idx = belts.arraySize;
            belts.arraySize = idx + 1;
            SerializedProperty item = belts.GetArrayElementAtIndex(idx);
            item.FindPropertyRelative("cell").vector2IntValue = cell;
            item.FindPropertyRelative("inFace").enumValueIndex = (int)inFace;
            item.FindPropertyRelative("outFace").enumValueIndex = (int)outFace;
            item.FindPropertyRelative("merger").boolValue = merger;
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
