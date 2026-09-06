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
    /// 실루엣 측정 보고서 (2026-09-06 신설 · 사용자 확정 둘 중 둘째).
    ///
    /// **이 도구가 낸 파일만 판정 근거로 쓴다.** 손 계산과 임시 스크립트 값은 근거가 아니다.
    /// 임시 스크립트는 세션이 끝나면 사라져 다음 사람이 같은 숫자를 재현할 수 없다 —
    /// 2026-09-06에 생성 프롬프트 문자열이 그렇게 유실됐고, 같은 날 측정법 자체가
    /// 회신문 셋에 잘못 들어갔다.
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath &lt;repo&gt; \
    ///             -executeMethod MBI.Editor.OverlapReport.Run -logFile &lt;log&gt;
    ///
    /// 산출물: <c>Docs/measure/overlap_&lt;날짜&gt;.md</c> — **리포에 남긴다.** scratchpad가 아니다.
    /// </summary>
    public static class OverlapReport
    {
        /// <summary>
        /// 픽셀이 「있다」고 보는 알파 문턱. 이 값도 측정법의 일부이므로 보고서 머리에 적는다 —
        /// 문턱이 바뀌면 숫자가 바뀌는데, 안 적어 두면 다음 사람이 같은 값을 못 낸다.
        /// </summary>
        private const byte AlphaThreshold = 16;

        private const string OutDir = "Docs/measure";

        /// <summary>기본으로 재는 자리. 배치모드에서 <c>-artRoot</c>로 갈아 끼운다.</summary>
        private const string DefaultRoot = "Assets/_Project/Art";

        /// <summary>
        /// 명령줄 인자 하나를 읽는다. 없으면 <paramref name="fallback"/>.
        ///
        ///   -artRoot   &lt;경로&gt;   Board/ · Items/ 를 담고 있는 폴더
        ///   -outSuffix &lt;문자열&gt; 같은 날 두 번 돌릴 때 파일명을 가른다
        /// </summary>
        private static string Arg(string name, string fallback)
        {
            string[] argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (string.Equals(argv[i], name, StringComparison.Ordinal))
                    return argv[i + 1];
            return fallback;
        }

        [MenuItem("MBI/실루엣 측정 보고서")]
        public static void Run()
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            string root = Arg("-artRoot", DefaultRoot).Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/');
            string suffix = Arg("-outSuffix", "");

            sb.AppendLine("# 실루엣 측정 보고서");
            sb.AppendLine();
            sb.AppendLine($"- 생성 시각: {stamp}");
            sb.AppendLine($"- 도구 커밋: `{HeadCommit()}`");
            // ⚠️ **무엇을 쟀는지가 숫자보다 먼저다.** 2026-09-06에 이 줄이 없어서
            // 09-04 구판을 잰 값이 승인본의 값으로 읽혔다 — 같은 자산 이름이 두 그림을 가리키고 있었다.
            sb.AppendLine($"- **잰 자리: `{root}`** (`-artRoot`로 바꾼다)");
            sb.AppendLine("- 측정법: **두 스프라이트를 각자의 캔버스 그대로 겹쳐, 알파가 함께 차 있는 넓이를 "
                          + "둘을 합친 넓이로 나눈다.** 잘라내지 않고 늘이지도 않는다");
            sb.AppendLine($"- 알파 문턱: {AlphaThreshold} 초과를 「있다」로 본다");
            sb.AppendLine("- 계산: `MBI.Core.SilhouetteOverlap` · 정답 케이스는 `SilhouetteOverlapTests`가 고정한다");
            sb.AppendLine("- ⚠️ 도구 커밋은 **실행 시점의 HEAD**다. 도구를 고치고 커밋 전에 돌리면 "
                          + "이 해시가 가리키는 커밋에는 그 도구가 없다 — 2026-09-06에 실제로 그렇게 나왔다. "
                          + "**잰 파일이 무엇인지는 아래 표의 md5가 말한다**");
            sb.AppendLine();

            int sections = 0;
            sections += Section(sb, "노드", root + "/Board", "node_", 0.90f);
            sections += Section(sb, "품목", root + "/Items", null, 0.90f);

            if (sections == 0)
            {
                Debug.LogError($"[MBI] 잴 자산이 없다 — {root}/Board · {root}/Items 를 확인하라.");
                EditorApplication.Exit(2);
                return;
            }

            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir,
                $"overlap_{DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture)}{suffix}.md");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[MBI] 실루엣 측정 보고서: {path}");
            EditorApplication.Exit(0);
        }

        /// <summary>한 묶음을 재서 표 둘(여백·가로세로비 / 쌍별 겹침)을 낸다. 잰 자산 수를 돌려준다.</summary>
        private static int Section(StringBuilder sb, string title, string dir, string prefix, float limit)
        {
            if (!Directory.Exists(dir)) return 0;

            var names = new List<string>();
            var masks = new List<AlphaMask>();
            var md5s = new List<string>();

            foreach (string file in Directory.GetFiles(dir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("_", StringComparison.Ordinal)) continue;          // 대조 시트는 자산이 아니다
                if (prefix != null && !name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!TryLoad(file, out AlphaMask m)) continue;

                names.Add(name);
                masks.Add(m);
                md5s.Add(Md5(file));
            }

            if (names.Count == 0) return 0;

            sb.AppendLine($"## {title} — {names.Count}종");
            sb.AppendLine();
            sb.AppendLine("| 자산 | md5 | 캔버스 | 여백 (L R T B) | 가장 좁은 변 | 가로세로비 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            for (int i = 0; i < names.Count; i++)
            {
                AlphaMask m = masks[i];
                if (m.IsEmpty)
                {
                    sb.AppendLine($"| `{names[i]}` | `{md5s[i]}` | {m.width}×{m.height} | — | — | — |");
                    continue;
                }
                MarginPx g = SilhouetteOverlap.Margins(m);
                sb.AppendLine($"| `{names[i]}` | `{md5s[i]}` | {m.width}×{m.height} | {g} | **{g.Min}** | "
                              + SilhouetteOverlap.AspectRatio(m).ToString("F2", CultureInfo.InvariantCulture) + " |");
            }
            sb.AppendLine();

            var rows = new List<(float v, string a, string b)>();
            int skipped = 0;
            for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                if (masks[i].width != masks[j].width || masks[i].height != masks[j].height) { skipped++; continue; }
                rows.Add((SilhouetteOverlap.Ratio(masks[i], masks[j]), names[i], names[j]));
            }
            rows.Sort((x, y) => y.v.CompareTo(x.v));

            int over = 0;
            foreach (var r in rows) if (r.v > limit) over++;

            sb.AppendLine($"**{rows.Count}쌍 · 상한 {limit.ToString("F2", CultureInfo.InvariantCulture)} "
                          + $"초과 {over}쌍**" + (skipped > 0 ? $" · 캔버스가 달라 건너뛴 쌍 {skipped}" : ""));
            sb.AppendLine();
            sb.AppendLine("| 쌍 | 겹침 | |");
            sb.AppendLine("|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| `{r.a}` × `{r.b}` | {r.v.ToString("F3", CultureInfo.InvariantCulture)} | "
                              + (r.v > limit ? "**초과**" : "") + " |");
            sb.AppendLine();

            return names.Count;
        }

        /// <summary>
        /// 파일 내용의 md5 앞 여덟 자리.
        ///
        /// **이 값이 「무엇을 쌀는가」의 유일한 증거다.** 자산 이름은 세대가 바뀜어도 같으므로
        /// 이름만으로는 09-04판과 09-06 승인본을 가를 수 없다 — 2026-09-06에 그 둘이 섞여
        /// 회신문 숫자가 리포와 달랐다.
        /// </summary>
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
            catch
            {
                return "알 수 없음";
            }
        }

        /// <summary>
        /// PNG를 임포트 설정과 무관하게 읽는다.
        ///
        /// <c>AssetDatabase</c>로 읽으면 <c>isReadable</c>·압축·`maxSize` 같은 임포트 설정에 결과가
        /// 흔들린다. 지금 아트는 임포트 설정을 확정하지 않기로 한 상태이므로(금지 조항) 파일에서 직접 읽는다.
        /// </summary>
        private static bool TryLoad(string path, out AlphaMask mask)
        {
            mask = default;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    Debug.LogWarning($"[MBI] PNG를 못 읽었다: {path}");
                    return false;
                }

                Color32[] px = tex.GetPixels32();
                var bits = new bool[px.Length];
                // GetPixels32는 아래에서 위로 담는다. 여백의 위·아래를 뒤집지 않으려고 여기서 되돌린다.
                for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    bits[y * tex.width + x] = px[(tex.height - 1 - y) * tex.width + x].a > AlphaThreshold;

                mask = new AlphaMask(tex.width, tex.height, bits);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 지금 커밋의 짧은 해시. 보고서가 어느 코드로 나온 숫자인지를 말해야 근거가 된다.
        /// git을 실행하지 않고 `.git`을 직접 읽는다 — 배치모드에서 프로세스를 띄우지 않기 위해서다.
        /// </summary>
        private static string HeadCommit()
        {
            try
            {
                string head = File.ReadAllText(".git/HEAD").Trim();
                if (head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    string refPath = Path.Combine(".git", head.Substring(4).Trim());
                    if (File.Exists(refPath)) return File.ReadAllText(refPath).Trim().Substring(0, 7);

                    foreach (string line in File.ReadAllLines(".git/packed-refs"))
                        if (line.EndsWith(head.Substring(4).Trim(), StringComparison.Ordinal))
                            return line.Substring(0, 7);
                    return "알 수 없음";
                }
                return head.Substring(0, 7);
            }
            catch
            {
                return "알 수 없음";
            }
        }
    }
}
