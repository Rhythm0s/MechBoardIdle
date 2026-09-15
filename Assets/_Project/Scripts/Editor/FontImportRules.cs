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
    /// ⚠️ **왜 필요한가 — 동적 굽기가 글리프를 놓쳤다.**
    /// 종전 설정은 `Dynamic` 이었다. 그러면 유니티가 **런타임에 쓰이는 글자만** 아틀라스에
    /// 구워 넣는데, WebGL 에서 **라틴 대문자가 빠졌다** — 「다리R」이 「다리I」로 찍히다가
    /// (아틀라스 UV 가 엉켰다) 사다리로 크기를 줄이자 **아예 사라졌다.**
    ///
    /// 📌 **한글은 전부 나오는데 라틴만 빠진 것이 결정적 단서였다.** 아틀라스 포화라면
    /// 글자 수가 압도적인 한글부터 빠진다 — 그러니 **포화가 아니라 굽기 자체**의 문제다.
    /// (`NotoSansKR-Regular.ttf` 의 cmap 에 R·L 은 **있다** — 직접 읽어 확인했다.)
    ///
    /// **정적 굽기로 바꾼다.** 쓰는 글자를 **임포트 시점에 전부** 구워 두면 런타임 아틀라스
    /// 생성이 없고, 따라서 **빠질 자리도 없다.**
    ///
    /// ⚠️ **글자 목록은 소스에서 뽑는다**(<see cref="ScreenGlyphs"/>). 손으로 적으면
    /// 새 문구를 넣을 때마다 빠뜨리고, 빠진 글자는 **에러 없이 안 보인다.**
    ///
    /// ⚠️ **문자열을 늘리면 폰트를 다시 임포트해야 한다.** 후처리기는 폰트가 임포트될 때만
    /// 돌기 때문이다. 시험 `KoreanFontSnapTests` 가 「소스의 모든 글자가 구워졌는가」를
    /// 지키고, 메뉴 <c>MBI/Reimport Korean Font</c> 가 한 번에 다시 굽는다.
    /// </summary>
    public sealed class FontImportRules : AssetPostprocessor
    {
        /// <summary>규격을 강제할 폰트. 다른 폰트는 안 건드린다.</summary>
        public const string FontPath = "Assets/_Project/Resources/Fonts/NotoSansKR-Regular.ttf";

        private void OnPreprocessAsset()
        {
            if (assetPath != FontPath) return;
            if (!(assetImporter is TrueTypeFontImporter font)) return;

            // ⚠️ **한 크기로 굽고 나머지는 축소해 쓴다.** 사다리 최대(44)에 맞춰 구우면
            // 작은 단들은 줄여 그려 흐려지지 않는다 — 키우면 뭉개지지만 줄이는 것은 괜찮다.
            font.fontSize = MBI.UI.KoreanFont.Ladder[MBI.UI.KoreanFont.Ladder.Length - 1];
            font.fontTextureCase = FontTextureCase.CustomSet;
            font.customCharacters = ScreenGlyphs.Collect();
            font.includeFontData = true;   // WebGL 엔 시스템 폰트가 없다 — 반드시 동봉한다
        }

        [MenuItem("MBI/Reimport Korean Font")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[MBI] 한글 폰트 재임포트 — 구운 글자 {ScreenGlyphs.Collect().Length}자 · "
                      + $"크기 {MBI.UI.KoreanFont.Ladder[MBI.UI.KoreanFont.Ladder.Length - 1]}");
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
