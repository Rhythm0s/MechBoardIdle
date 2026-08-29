using System.IO;
using MBI.Combat;
using MBI.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBI.Editor
{
    /// <summary>
    /// 전투 씬을 정식 API로 생성한다(§5-6·7). 메뉴: MBI/Create Combat Scene.
    ///
    /// 탑뷰 orthographic 카메라 + 중앙 로봇 + StageRunner(기본 Stage_S1·Robot_A·CombatTuning 주입).
    /// 씬 YAML 수기 편집 금지 — EditorSceneManager가 fileID·m_Roots 관리(§3, LogisticsSceneCreator와 동일 패턴).
    /// 실행 전 'MBI/Generate Combat Data'로 SO를 먼저 생성해야 참조가 채워진다(없으면 경고 후 빈 참조).
    /// </summary>
    public static class CombatSceneCreator
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string ScenePath = ScenesDir + "/Combat.unity";
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string TuningPath = SoRoot + "/CombatTuning.asset";

        [MenuItem("MBI/Create Combat Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 탑뷰 2D 카메라 — 아레나 반경 6이 보이도록 orthographicSize 8.
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 8f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            }

            var runnerGo = new GameObject("StageRunner");
            runnerGo.transform.position = Vector3.zero;
            EditorSceneManager.MoveGameObjectToScene(runnerGo, scene);
            var runner = runnerGo.AddComponent<StageRunner>();

            RobotDefinition robot = Load<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            RobotDefinition robotB = Load<RobotDefinition>($"{SoRoot}/Robots/Robot_B.asset");
            StageDefinition stage = Load<StageDefinition>($"{SoRoot}/Stages/Stage_S1.asset");
            CombatTuning tuning = LoadOrCreateTuning();
            EnemyDefinition[] enemies =
            {
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_infantry.asset"),
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_artillery.asset"),
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_armor.asset"),
                Load<EnemyDefinition>($"{SoRoot}/Enemies/Enemy_boss.asset"),
            };

            if (robot == null || stage == null)
                Debug.LogWarning("[MBI] Robot_A/Stage_S1 SO 없음 — 먼저 'MBI/Generate Combat Data' 실행. 참조가 빈 채로 씬만 생성됨.");

            var so = new SerializedObject(runner);
            so.FindProperty("robot").objectReferenceValue = robot;
            so.FindProperty("robotB").objectReferenceValue = robotB; // 태그 상대 — 있어야 태그·합체가 돈다
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("tuning").objectReferenceValue = tuning;

            SerializedProperty catalog = so.FindProperty("enemyCatalog");
            catalog.arraySize = 0;
            foreach (EnemyDefinition e in enemies)
            {
                if (e == null) continue;
                int idx = catalog.arraySize;
                catalog.arraySize = idx + 1;
                catalog.GetArrayElementAtIndex(idx).objectReferenceValue = e;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 격리 씬 물류 소스(§5-6 D2): 보드가 없으므로 브릿지를 이 컴포넌트가 채운다.
            // StageRunner는 두 씬 모두에서 브릿지만 읽는다 — 씬 구분 분기가 필요 없다.
            var source = runnerGo.AddComponent<MockLogisticsSource>();
            source.robot = robot;
            // 드론 몸체·추진제 산출은 군수 노드의 조합표에서 읽는다 — mock이 숫자를 들지 않는다.
            source.munitionsNode = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_muni.asset");

            AssetDatabase.SaveAssets(); // 새 CombatTuning.asset을 씬 저장 전에 flush.

            EnsureFolder(ScenesDir);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (saved)
                Debug.Log($"[MBI] Combat 씬 생성 완료: {ScenePath} (탑뷰 카메라 + StageRunner: Stage_S1·Robot_A·Robot_B·CombatTuning). Play로 태그·합체·회피 확인.");
            else
                Debug.LogError($"[MBI] Combat 씬 저장 실패: {ScenePath}");
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
