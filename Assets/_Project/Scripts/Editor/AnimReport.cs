using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MBI.Core;
using MBI.Core.Anim;
using MBI.Data;
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
            sb.AppendLine("- 도구 커밋: `" + HeadShort()
                          + DirtyMark("Assets/_Project/Scripts/Editor/AnimReport.cs") + "`");
            sb.AppendLine("- **잰 자리: `" + root + "`** (`-animRoot`로 바꾼다)");
            sb.AppendLine("- **진폭 측정법: 프레임마다 알파 bbox의 윗변을 잡아, 그 윗변의 최댓값과 최솟값의 차를 프레임 평균 실루엣 높이로 나눈다.** 어깨가 오르내리는 폭이라 윗변으로 잰다");
            sb.AppendLine("- **최대·최소 방식이다 — 프레임 사이 이동량을 더하는 방식이 아니다**(`260907_W01` 확인 1). 같은 그림을 여러 칸이 가리켜도 최대와 최소가 안 움직이므로 **칸 복제는 이 값을 바꾸지 않는다**");
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

            sb.AppendLine("- 칸 열 셋은 `MBI.Core.Anim.AnimSchedule`이 낸다 — 한 칸 1/16초 · 목표 초는 `CombatTuning`(`260907_W01` 4-5)");
            sb.AppendLine();
            sb.AppendLine("| 벌 | 그림 | 캔버스 | 실루엣 높이(평균) | 여백 T/B(최소) | 진폭 px | **진폭 %** | 잴 수 있나 | 기본 칸 | 필요 칸 | 실제 초 | 첫 프레임 md5 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");

            var warnings = new List<string>();
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
                    int marginTopMin = int.MaxValue, marginBottomMin = int.MaxValue;
                    bool clipped = false;

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

                        // 캔버스에 닿으면 그 프레임의 실루엣은 잘려 있다 — bbox가 더 못 움직인다.
                        int mTop = minY;
                        int mBottom = m.height - 1 - maxY;
                        marginTopMin = Math.Min(marginTopMin, mTop);
                        marginBottomMin = Math.Min(marginBottomMin, mBottom);
                        if (mTop == 0 || mBottom == 0) clipped = true;
                    }

                    if (counted == 0) continue;

                    double avgH = (double)heightSum / counted;
                    int ampPx = topMax - topMin;
                    string ampPct = avgH > 0
                        ? (100.0 * ampPx / avgH).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                        : "—";

                    string measurable = clipped ? "**아니다 — 잘림**" : "예";

                    string label = clipName + "/" + Path.GetFileName(dirDir);
                    AnimSchedule sch = ScheduleFor(clipName, Path.GetFileName(dirDir), files.Length);
                    if (sch.DeletedCells > 0)
                        warnings.Add("- `" + label + "` — **" + sch.DeletedCells + "칸을 지웠다.** 화면에 한 번도 안 나오는 그림: "
                                     + string.Join(", ", Array.ConvertAll(sch.UnusedFrames, f => "frame_" + f.ToString("000"))));
                    if (sch.HasWarning) warnings.Add("- `" + label + "` — " + sch.Warning);

                    // 같은 그림이 한 벌 안에 두 번 이상 놓인 자리를 잡는다. 폴더에 파일이 여섯이면
                    // 「그림 여섯」으로 세어지는데, 그중 둘이 같은 그림이면 실제로 그린 것은 다섯이다.
                    // 벌 목록을 세는 테스트는 파일 수만 보므로 이것을 못 본다(15 7-4 「테스트가 못 보는 자리」).
                    string dup = DuplicateFrames(files);
                    if (dup != null) warnings.Add("- `" + label + "` — **같은 그림이 두 번 놓였다** " + dup);

                    sb.AppendLine("| `" + label + "` | " + files.Length
                        + " | " + canvasW + "×" + canvasH
                        + " | " + avgH.ToString("0.0", CultureInfo.InvariantCulture)
                        + " | " + marginTopMin + " / " + marginBottomMin
                        + " | " + ampPx + " | **" + ampPct + "** | " + measurable
                        + " | " + sch.BaseCells + " | " + sch.NeededCells
                        + " | " + sch.ActualSeconds.ToString("0.00", CultureInfo.InvariantCulture)
                        + " | `" + Md5(files[0]) + "` |");
                    rows++;
                }
            }

            if (rows == 0) sb.AppendLine("| — | — | — | — | — | — | — | — | — | — | — | — |");
            sb.AppendLine();
            sb.AppendLine("**" + rows + "벌.** 대기 진폭 규격은 256 이상에서 실루엣 높이의 **4~6%**다 (캐릭터 아트 요청 문서(15)「동작의 크기」).");
            sb.AppendLine();
            sb.AppendLine("> 이동·사망·태그 행의 진폭은 규격이 없다 — 대기만 4~6%로 판정한다. 나머지는 참고로 둔다.");
            sb.AppendLine();
            sb.AppendLine("⚠️ **「잴 수 있나」가 「아니다 — 잘림」이면 그 행의 진폭은 판정 근거가 아니다.** 실루엣이 캔버스 가장자리에 닿아 있으면 bbox의 윗변이 더 올라갈 자리가 없어, 몸이 실제로 오르내려도 숫자가 안 움직인다. 그런 행의 진폭은 **하한값**이다 — 「이만큼은 움직였다」이지 「이만큼만 움직였다」가 아니다.");
            sb.AppendLine();
            sb.AppendLine("> 여백이 0인 자산에서는 4~6% 진폭 자체가 캔버스 안에 들어가지 않는다. 진폭을 규격대로 얻으려면 스틸에 여백이 먼저 있어야 한다.");
            sb.AppendLine();

            // 삭제는 조용히 지나가면 안 된다 — 지운 그림은 화면에 한 번도 안 나오는데
            // UnitAnimWiringTests 는 폴더를 세므로 통과한다. 테스트가 못 보는 자리다(W01 4-4).
            sb.AppendLine("## 칸 규칙 경고");
            sb.AppendLine();
            if (warnings.Count == 0)
            {
                sb.AppendLine("**없다.** 삭제된 칸이 없고 규칙을 지킬 수 없던 벌도 없다 — 모든 그림이 화면에 적어도 한 번 나온다.");
            }
            else
            {
                sb.AppendLine("⚠️ **아래 벌은 그림 장수가 화면에서 지켜지지 않는다.** 칸을 지웠거나(목표 초가 짧다), 같은 그림이 두 번 놓여 파일 수보다 실제 그림이 적다.");
                sb.AppendLine();
                foreach (string w in warnings) sb.AppendLine(w);
            }

            Write(sb, suffix, stamp);
        }

        /// <summary>
        /// 이 벌의 칸 계산. 목표 초는 상태 이름으로 고른다 — 보고서는 에디터 밖에서도 돌므로
        /// SO 대신 <c>CombatTuning</c>의 확정값(`260907_W01` 4-5)을 그대로 쓴다.
        /// 폴더 이름이 <c>{robot}_{State}</c> 꼴이라 밑줄 뒤가 상태다.
        /// </summary>
        /// <summary>
        /// 이 벌이 화면에서 실제로 도는 칸 목록을 낸다.
        ///
        /// <b>칸 목록이 지정된 벌은 그림 수와 기본 칸이 다르다</b>(`260907_W03` 2-3) — 이동 남·북은
        /// 그림 다섯 장으로 여섯 칸을 돈다. 폴더의 파일 수만 보면 기본 칸이 다섯으로 찍혀
        /// <b>보고서가 화면과 다른 말을 하게 된다.</b> 규칙은 생성기와 같은 것을 부른다 —
        /// 두 곳에 적으면 「그 벌이 몇 칸인가」에 답이 둘이 된다.
        /// </summary>
        private static AnimSchedule ScheduleFor(string clipName, string dirName, int frameCount)
        {
            int us = clipName.LastIndexOf('_');
            string state = us >= 0 ? clipName.Substring(us + 1) : clipName;
            string robot = us >= 0 ? clipName.Substring(0, us) : clipName;

            float seconds;
            bool pingPong = false;
            switch (state)
            {
                case "Idle":  seconds = 1.00f; pingPong = true; break;
                case "Move":  seconds = 1.00f; break;
                case "Death": seconds = 2.00f; break;
                case "TagIn": seconds = 0.75f; break;
                default:      seconds = 1.00f; break;
            }
            int[] order = null;
            if (Enum.TryParse(state, out UnitAnimState st) &&
                Enum.TryParse(dirName, true, out UnitAnimDirection dir))
                order = CombatAssetGenerator.CellOrder(robot, st, dir, frameCount);

            return AnimSchedule.Build(frameCount, seconds, pingPong, null, order);
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


        /// <summary>
        /// 도구 소스가 인덱스보다 새로우면 「+dirty」를 붙인다.
        /// 도구를 고치고 커밋 전에 보고서를 돌리면 머리의 해시가 가리키는 커밋에는 그 도구가 없다 —
        /// 2026-09-06과 2026-09-07에 연달아 그렇게 나왔다. 해시만으로는 그 사실이 안 보인다.
        /// git을 실행하지 않고 파일 시각만 본다(배치모드에서 프로세스를 띄우지 않는다).
        /// </summary>
        private static string DirtyMark(string toolSourceRelPath)
        {
            try
            {
                string index = Path.Combine(Directory.GetCurrentDirectory(), ".git", "index");
                string tool = Path.Combine(Directory.GetCurrentDirectory(), toolSourceRelPath);
                if (!File.Exists(index) || !File.Exists(tool)) return "";
                return File.GetLastWriteTimeUtc(tool) > File.GetLastWriteTimeUtc(index) ? "+dirty" : "";
            }
            catch { return ""; }
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

        /// <summary>
        /// 한 벌 안에서 md5가 같은 프레임 묶음. 없으면 null.
        /// <b>그림 장수 규격이 파일 수로만 지켜지는 것을 막는다</b> — 반 바퀴만 그리고
        /// 가운데 칸을 한 번 더 놓아 수를 채우면 파일은 여섯이지만 그림은 다섯이다.
        /// </summary>
        private static string DuplicateFrames(string[] files)
        {
            var byHash = new Dictionary<string, List<int>>();
            for (int i = 0; i < files.Length; i++)
            {
                string h = Md5(files[i]);
                if (!byHash.TryGetValue(h, out List<int> list)) byHash[h] = list = new List<int>();
                list.Add(i);
            }

            var parts = new List<string>();
            int distinct = 0;
            foreach (var kv in byHash)
            {
                distinct++;
                if (kv.Value.Count < 2) continue;
                parts.Add(string.Join(" = ", kv.Value.ConvertAll(i => "frame_" + i.ToString("000")))
                          + " (`" + kv.Key + "`)");
            }
            if (parts.Count == 0) return null;
            return string.Join(" · ", parts) + " — 파일 " + files.Length + "개 · **실제 그림 " + distinct + "장**";
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
