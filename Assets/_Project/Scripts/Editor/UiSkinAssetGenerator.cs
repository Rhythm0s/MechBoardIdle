using System.IO;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// `Art/UI/` 의 그릇 그림을 <see cref="UiSkinAssets"/> 에 꽂는다
    /// (2026-09-11 · `260911_W02` 9장 자산 절).
    ///
    /// **경로는 생성기에만 있다** — 다른 생성기와 같은 규칙이다. 런타임 코드는
    /// SO 만 보고, 파일 이름이 바뀌면 여기 한 줄만 고친다.
    ///
    /// ⚠️ **없는 그림은 비워 둔다.** 자산이 하나씩 들어오는 중이라 못 찾은 것을
    /// 다른 그림으로 대신 채우면 **화면에서 무엇이 빠졌는지가 안 보인다.**
    /// </summary>
    public static class UiSkinAssetGenerator
    {
        private const string ArtDir = "Assets/_Project/Art/UI";
        private const string AssetPath = "Assets/_Project/Resources/UiSkinAssets.asset";

        [MenuItem("MBI/Generate UI Skin Assets")]
        public static void Generate()
        {
            var so = AssetDatabase.LoadAssetAtPath<UiSkinAssets>(AssetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<UiSkinAssets>();
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.CreateAsset(so, AssetPath);
            }

            // ⚠️ **c10 은 `ui_plate_button.png` 로 설치됐다**(md5 6b3a85a0 · 64×64 · Point).
            // 눌림·잠김·패널은 아직 안 왔다 — 오면 여기 줄이 는다.
            so.buttonNormal = Load("ui_plate_button");
            so.buttonPressed = Load("ui_plate_button_pressed");
            so.buttonLocked = Load("ui_plate_button_locked");
            so.panel = Load("ui_plate_panel");
            so.border = 16; // 9-슬라이스 원본 64 · 여백 16 (W02 9장)

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MBI] UI 그릇 자산 — 기본 {Mark(so.buttonNormal)} · 눌림 {Mark(so.buttonPressed)}"
                      + $" · 잠김 {Mark(so.buttonLocked)} · 패널 {Mark(so.panel)} · 여백 {so.border}");
        }

        private static Texture2D Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{name}.png");

        private static string Mark(Texture2D t) => t != null ? "있음" : "없음";
    }
}
