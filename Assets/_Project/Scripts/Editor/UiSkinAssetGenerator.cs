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
            // ⚠️ **여백을 그림에서 읽는다**(2026-09-11) — 상수로 박으면 그림이 바뀔 때
            // 코드도 같이 고쳐야 하고, 안 고치면 **임포터와 껍데기가 서로 다른 여백**을 믿는다.
            // `SpriteImportRules` 가 강제한 값이 그대로 돌아온다.
            so.border = BorderOf("ui_plate_button", ArtSpec.UiPlateBorder);

            // ⚠️ **상태 표식 다섯**(2026-09-11 · §71-33 ③). 09-04 설치본인데 배선이 없었다.
            so.iconLogiNormal = Load("icon_logi_normal");
            so.iconLogiSlow = Load("icon_logi_slow");
            so.iconLogiStop = Load("icon_logi_stop");
            so.iconNotConnected = Load("icon_not_connected");
            so.iconPowerShort = Load("icon_power_short");

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MBI] UI 그릇 자산 — 기본 {Mark(so.buttonNormal)} · 눌림 {Mark(so.buttonPressed)}"
                      + $" · 잠김 {Mark(so.buttonLocked)} · 패널 {Mark(so.panel)} · 여백 {so.border}"
                      + $" · 표식 {IconCount(so)}/5");
        }

        /// <summary>그림에 박힌 9-슬라이스 여백. 못 읽으면 규격 기본값.</summary>
        private static int BorderOf(string name, int fallback)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sp == null) return fallback;

            int left = Mathf.RoundToInt(sp.border.x);
            return left > 0 ? left : fallback;
        }

        private static Texture2D Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{name}.png");

        private static string Mark(Texture2D t) => t != null ? "있음" : "없음";

        private static int IconCount(UiSkinAssets so)
        {
            int n = 0;
            if (so.iconLogiNormal != null) n++;
            if (so.iconLogiSlow != null) n++;
            if (so.iconLogiStop != null) n++;
            if (so.iconNotConnected != null) n++;
            if (so.iconPowerShort != null) n++;
            return n;
        }
    }
}
