using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 한글 폰트 임포트 규격 강제 (2026-09-15 · 「다리L」 결함 · 09-09 규칙 「임포트 규칙은 소스다」).
    ///
    /// **지금 하는 일** — `Dynamic` 유지 + `includeFontData` 강제. 손으로 `.meta` 를
    /// 만지지 않고 **코드가 규격을 쥔다**(09-09 규칙).
    ///
    /// ⚠️⚠️ **정적 굽기를 시도했다가 되돌렸다 — 이 기록이 이 파일의 값이다.**
    ///
    /// **관찰**: WebGL 에서 **라틴 대문자만** 안 그려졌다. 「다리R」이 「다리I」로 찍히다가
    /// 글자 크기를 줄이자 **아예 사라졌다.** 한글은 **전부** 나왔다.
    /// cmap 에 R·L 은 **있다**(직접 읽어 확인).
    ///
    /// **추론**: 아틀라스 포화라면 글자 수가 압도적인 한글부터 빠진다 — 그러니 포화가
    /// 아니라 **굽기 자체**의 문제로 보았다. 그래서 쓰는 글자 642 자를 `CustomSet` 으로
    /// 임포트 시점에 전부 구웠다.
    ///
    /// **결과**: **WebGL 에서 글자가 하나도 안 나왔다.** 한글도 라틴도 전부. 메뉴 버튼이
    /// 빈 판이 됐다. 정적 아틀라스가 WebGL 런타임에서 안 물리는 것으로 보인다.
    ///
    /// **판정**: 한 글자가 빠지는 것보다 **전부 빠지는 것이 나쁘다.** Dynamic 으로 되돌린다.
    /// **「다리L」의 라틴 문제는 아직 안 닫혔다** — 다음 갈래는 폴백 폰트 추가나
    /// 라틴 전용 폰트 병용이다. **같은 갈래를 두 번 시도하지 않도록** 여기 남긴다.
    ///
    /// <see cref="ScreenGlyphs"/> 는 **남겨 둔다** — 예산 시험이 쓰고, 폴백 갈래에서도
    /// 「무슨 글자를 쓰는가」는 같은 물음이다.
    /// </summary>
    public sealed class FontImportRules : AssetPostprocessor
    {
        /// <summary>규격을 강제할 폰트. 다른 폰트는 안 건드린다.</summary>
        public const string FontPath = "Assets/_Project/Resources/Fonts/NotoSansKR-Regular.ttf";

        private void OnPreprocessAsset()
        {
            if (assetPath != FontPath) return;
            if (!(assetImporter is TrueTypeFontImporter font)) return;

            // ⚠️⚠️ **정적 굽기(CustomSet)를 시도했다가 되돌렸다**(2026-09-15 · 실측).
            //
            // 「동적이 라틴을 놓친다」는 관찰에서 정적으로 642 자를 구워 봤더니
            // **WebGL 에서 글자가 하나도 안 나왔다** — 한글도 라틴도 전부. 메뉴 버튼이
            // 빈 판이 됐다. 정적 아틀라스가 WebGL 런타임에서 안 물리는 것으로 보인다.
            //
            // **한 글자가 빠지는 것보다 전부 빠지는 것이 나쁘다.** Dynamic 으로 되돌린다.
            // 「다리L」의 라틴 문제는 **아직 안 닫혔다** — 다른 길을 찾아야 한다
            // (폴백 폰트 추가 · 라틴 전용 폰트 병용 등). 여기 적어 두는 이유는
            // **같은 갈래를 두 번 시도하지 않게** 하려는 것이다.
            font.fontTextureCase = FontTextureCase.Dynamic;
            font.fontSize = 16;            // 동적에서는 참고값일 뿐이다
            font.includeFontData = true;   // WebGL 엔 시스템 폰트가 없다 — 반드시 동봉한다
        }

        [MenuItem("MBI/Reimport Korean Font")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[MBI] 한글 폰트 재임포트 — Dynamic · includeFontData");
        }
    }

    /// <summary>
    /// **화면에 나올 수 있는 글자를 소스에서 뽑는다** (2026-09-15).
    ///
    /// ⚠️ **한 곳에만 둔다.** 임포트 규칙과 예산 시험이 **같은 목록**을 봐야 한다 —
    /// 둘이 갈리면 「시험은 통과하는데 화면에서 빠진 글자」가 생긴다
    /// (지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」).
    ///
    /// 문자열 리터럴만 센다 — 주석은 화면에 안 나온다. 시험·에디터 도구는 빌드에 안 들어간다.
    /// </summary>
    public static class ScreenGlyphs
    {
        private const string ScriptRoot = "Assets/_Project/Scripts";

        // C# 문자열 리터럴 한 덩이. 이스케이프(\" 등)를 건너뛰며 읽는다.
        private static readonly Regex Literal = new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"");

        /// <summary>구워야 할 글자 전부. 차례는 고정이다 — 임포트 결과가 흔들리면 안 된다.</summary>
        public static string Collect()
        {
            var set = new SortedSet<char>();

            // ⚠️ **ASCII 는 통째로 넣는다.** 소스에 안 쓰인 글자라도 **수치 표시**로 런타임에
            // 나온다(숫자·소수점·부호) — 「지금 소스에 있는 것」만 구우면 그런 글자가 빠진다.
            for (char c = ' '; c <= '~'; c++) set.Add(c);

            foreach (string path in Directory.GetFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Tests/") || norm.Contains("/Editor/")) continue;

                foreach (Match m in Literal.Matches(File.ReadAllText(path)))
                    foreach (char ch in m.Groups[1].Value)
                        if (IsDrawable(ch)) set.Add(ch);
            }

            var sb = new StringBuilder(set.Count);
            foreach (char c in set) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>그려지는 글자인가. 공백·제어문자는 자리를 안 먹는다.</summary>
        public static bool IsDrawable(char ch)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch)) return false;
            if (ch >= '가' && ch <= '힣') return true;   // 한글 음절
            if (ch >= 'ㄱ' && ch <= 'ㆎ') return true;   // 낱자
            if (ch < 0x80) return true;                          // 라틴·숫자·부호
            return ch == '·' || ch == '×' || ch == '→' || ch == '—' || ch == '⇄'
                || ch == '▲' || ch == '▼' || ch == '●' || ch == '⚠' || ch == '−';
        }
    }
}
