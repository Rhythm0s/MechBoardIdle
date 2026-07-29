using System.IO;
using MBI.Data;
using MBI.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBI.Editor
{
    /// <summary>
    /// 기본 물류 씬을 정식 API로 생성한다(§5-1). 메뉴: MBI/Create Logistics Scene.
    ///
    /// 씬 YAML을 손으로 짜지 않는다 — EditorSceneManager가 fileID·SceneRoots(m_Roots)를 관리하게 맡긴다.
    /// BalanceAssetGenerator와 동일한 Editor 자동화 패턴.
    /// </summary>
    public static class LogisticsSceneCreator
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string ScenePath = ScenesDir + "/Logistics.unity";

        [MenuItem("MBI/Create Logistics Scene")]
        public static void Create()
        {
            // 열려 있는 미저장 변경을 먼저 처리(취소 시 중단).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2D 카메라 보장(orthographic). URP가 카메라에 UniversalAdditionalCameraData를 자동 부착.
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }

            // 물류 보드 루트(§5-3 그리드가 이 아래에 배치).
            var boardRoot = new GameObject("BoardRoot");
            boardRoot.transform.position = Vector3.zero;
            EditorSceneManager.MoveGameObjectToScene(boardRoot, scene);

            // §5-3 그리드/탭 컨트롤러 부착 + 설정 주입.
            // BoardConfig는 LoadOrCreate로 확보 → 'Create Board Config'를 먼저 안 돌려도 null 참조 없음(순서 무관).
            var controller = boardRoot.AddComponent<BoardController>();
            BoardConfig boardConfig = BoardConfigGenerator.LoadOrCreate();
            var so = new SerializedObject(controller);
            so.FindProperty("config").objectReferenceValue = boardConfig;
            // 배치 대상 기본값 = 코어 노드(있으면). 없으면 비워 둠(런타임 경고) — 팔레트는 §8.
            var coreNode = AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                "Assets/_Project/ScriptableObjects/Nodes/Node_core.asset");
            if (coreNode != null)
                so.FindProperty("placeTarget").objectReferenceValue = coreNode;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets(); // 새로 만든 BoardConfig.asset을 씬 저장 전에 디스크로 flush.

            EnsureFolder(ScenesDir);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (saved)
                Debug.Log($"[MBI] Logistics 씬 생성 완료: {ScenePath} (2D orthographic 카메라 + BoardRoot + BoardController §5-3).");
            else
                Debug.LogError($"[MBI] Logistics 씬 저장 실패: {ScenePath}");
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
