using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MBI.Core;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 애니메이션 프레임을 재서 <c>Docs/measure/anim_&lt;날짜&gt;.md</c>로 낸다.
    ///
    /// 재는 것은 <b>대기 진폭</b>이다 — 캐릭터 아트 요청 문서(15)「동작의 크기」가
    /// 「실루엣 높이의 4~6%」로 정해 두고 「픽셀 환산값은 도구 파일럿 뒤에 역기입한다」고 적었다.
    /// 그 숫자를 내는 도구가 이것이다.
    ///
    /// 배치모드:
    ///   -executeMethod MBI.Editor.AnimReport.Run [-animRoot &lt;경로&gt;] [-outSuffix &lt;문자열&gt;]
    ///
    /// 도구 커밋은 <b>실행 시점의 HEAD</b>다. 잰 파일이 무엇인지는 표의 md5가 말한다
    /// (2026-09-06에 이것 때문에 보고서가 틀렸다).
    /// </summary>
    public static class AnimReport
    {
        private const string DefaultRoot = "Assets/_Project/Art/Anim";
        private const byte AlphaThreshold = 16;

        private static string Arg(string name, string fallback)
        {
            string[] argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (string.Equals(argv[i], name, StringComparison.Ordinal))
                    return argv[i + 1];
            return fallback;
        }

        [MenuItem("MBI/애니메이션 측정 보고서")]
        public static void Run()
        {
            string root = Arg("-animRoot", DefaultRoot).Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/');
            string suffix = Arg("-outSuffix", "");
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.AppendLine("# 애니메이션 측정 보고서");
            sb.AppendLine();
            sb.AppendLine("- 생성 시각: " + stamp);
            sb.AppendLine("- 도구 커밋: `" + HeadShort() + "`");
            sb.AppendLine("- **잰 자리: `" + root + "`** (`-animRoot`로 바꾼다)");
            sb.AppendLine("- **진폭 측정법: 프레임마다 알파 bbox의 윗변을 잡아, 그 윗변이 프레임 사이에 오르내린 최댓값을 프레임 평균 실루엣 높이로 나눈다.** 어깨가 오르내리는 폭이라 윗변으로 잰다");
            sb.AppendLine("- 알파 문턱: 16 초과를 「있다」로 본다 · 캔버스 그대로만 잰다(자르거나 늘이지 않는다)");
            sb.AppendLine("- 계산: `MBI.Core.SilhouetteOverlap.TryBounds` 재사용");
            sb.AppendLine("- 도구 커밋은 **실행 시점의 HEAD**다. **잰 파일이 무엇인지는 아래 표의 md5가 말한다**");
            sb.AppendLine();

            if (!Directory.Exists(root))
            {
                sb.AppendLine("**폴더가 없다: `" + root + "`**");
                Write(sb, suffix, stamp);
                return;
            }

            sb.AppendLine("| 벌 | 프레임 | 캔버스 | 실루엣 높이(평균) | 진폭 px | **진폭 %** | 첫 프레임 md5 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            var dirs = new List<string>(Directory.GetDirectories(root));
            dirs.Sort(StringComparer.Ordinal);
            int rows = 0;

            foreach (string clipDir in dirs)
            {
                string clipName = Path.GetFileName(clipDir);
                var subs = new List<string>(Directory.GetDirectories(clipDir));
                subs.Sort(StringComparer.Ordinal);

                foreach (string dirDir in subs)
                {
                    string[] files = Directory.GetFiles(dirDir, "frame_*.png");
                    if (files.Length == 0) continue;
                    Array.Sort(files, StringComparer.Ordinal);

                    int topMin = int.MaxValue, topMax = int.MinValue;
                    long heightSum = 0;
                    int counted = 0, canvasW = 0, canvasH = 0;

                    foreach (string f in files)
                    {
                        if (!TryLoad(f, out AlphaMask m)) continue;
                        canvasW = m.width;
                        canvasH = m.height;
                        if (!SilhouetteOverlap.TryBounds(m, out _, out _, out int minY, out int maxY)) continue;

                        // minY = 실루엣의 윗변(마스크는 위에서 아래로 담긴다).
                        topMin = Math.Min(topMin, minY);
                        topMax = Math.Max(topMax, minY);
                        heightSum += maxY - minY + 1;
                        counted++;
                    }

                    if (counted == 0) continue;

                    double avgH = (double)heightSum / counted;
                    int ampPx = topMax - topMin;
                    string ampPct = avgH > 0
                        ? (100.0 * ampPx / avgH).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                        : "—";

                    sb.AppendLine("| `" + clipName + "/" + Path.GetFileName(dirDir) + "` | " + files.Length
                        + " | " + canvasW + "×" + canvasH
                        + " | " + avgH.ToString("0.0", CultureInfo.InvariantCulture)
                        + " | " + ampPx + " | **" + ampPct + "** | `" + Md5(files[0]) + "` |");
                    rows++;
                }
            }

            if (rows == 0) sb.AppendLine("| — | — | — | — | — | — | — |");
            sb.AppendLine();
            sb.AppendLine("**" + rows + "벌.** 대기 진폭 규격은 256 이상에서 실루엣 높이의 **4~6%**다 (캐릭터 아트 요청 문서(15)「동작의 크기」).");
            sb.AppendLine();
            sb.AppendLine("> 이동·사망·태그 행의 진폭은 규격이 없다 — 대기만 4~6%로 판정한다. 나머지는 참고로 둔다.");

            Write(sb, suffix, stamp);
        }

        private static void Write(StringBuilder sb, string suffix, string stamp)
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "measure");
            Directory.CreateDirectory(outDir);
            string name = "anim_" + DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture) + suffix + ".md";
            string path = Path.Combine(outDir, name);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[MBI] 애니메이션 측정 보고서: " + path + " (" + stamp + ")");
        }

        private static string HeadShort()
        {
            try
            {
                string gitDir = Path.Combine(Directory.GetCurrentDirectory(), ".git");
                string head = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(head)) return "알 수 없음";

                string line = File.ReadAllText(head).Trim();
                if (line.StartsWith("ref:", StringComparison.Ordinal))
                {
                    string refPath = Path.Combine(gitDir, line.Substring(4).Trim());
                    if (!File.Exists(refPath)) return "알 수 없음";
                    string sha = File.ReadAllText(refPath).Trim();
                    return sha.Length >= 7 ? sha.Substring(0, 7) : sha;
                }
                return line.Length >= 7 ? line.Substring(0, 7) : line;
            }
            catch { return "알 수 없음"; }
        }

        private static string Md5(string path)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    byte[] h = md5.ComputeHash(File.ReadAllBytes(path));
                    var hex = new StringBuilder(8);
                    for (int i = 0; i < 4; i++) hex.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
                    return hex.ToString();
                }
            }
            catch { return "알 수 없음"; }
        }

        /// <summary>PNG를 임포트 설정과 무관하게 읽는다 — <c>OverlapReport</c>와 같은 이유다.</summary>
        private static bool TryLoad(string path, out AlphaMask mask)
        {
            mask = default;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return false;

                Color32[] px = tex.GetPixels32();
                var bits = new bool[px.Length];
                // GetPixels32는 아래에서 위로 담는다. 위·아래를 뒤집지 않으려고 여기서 되돌린다.
                for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    bits[y * tex.width + x] = px[(tex.height - 1 - y) * tex.width + x].a > AlphaThreshold;

                mask = new AlphaMask(tex.width, tex.height, bits);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }
    }
}
