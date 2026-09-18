using System;
using System.Collections.Generic;
using System.Globalization;

namespace MBI.Core
{
    /// <summary>
    /// **CSV 한 장을 읽는 자리** (2026-09-18 사용자 확정 · 밸런스 표 → CSV → SO).
    ///
    /// 📌 **값의 원천이 표로 옮겨 간다.** 사람이 고치는 것은 xlsx, 변환기가 뜨는 것은 CSV,
    /// 그것을 읽어 SO 로 굽는 것이 생성기다. 이 파일은 **가운데 한 칸**이다.
    ///
    /// ⚠️⚠️ **못 읽으면 멈춘다.** 빠진 열·숫자가 아닌 칸을 0 으로 때우면 **표가 조용히
    /// 거짓말을 한다** — 그 0 이 자산에 굳고, 화면에서 「왜 안 나오지」로 나타난다.
    /// 지어내는 대신 **어느 표 · 몇째 줄 · 어느 열**인지를 들고 죽는다.
    ///
    /// ⚠️ **빈 칸은 0 이 아니다.** <see cref="Row.Num"/> 는 빈 칸에 기본값을 돌려주고,
    /// 그 기본값을 부르는 쪽이 정한다 — 적 표의 「0 = 안 정함 → 폴백」 규약이 여기 걸려 있다.
    ///
    /// ⚠️ **엑셀이 쓰는 CSV 를 받는다** — 따옴표로 감싼 칸과 그 안의 쉼표·큰따옴표 둘.
    /// 줄바꿈이 든 칸은 **안 받는다**(밸런스 표에 그런 칸이 없다 · 생기면 그때 연다).
    /// </summary>
    public sealed class CsvTable
    {
        /// <summary>표 이름 — 오류 문장에 쓴다. 어느 표인지 모르면 고칠 데를 못 찾는다.</summary>
        public string Name { get; }

        /// <summary>1행의 열 이름들(차례 그대로).</summary>
        public IReadOnlyList<string> Fields => _fields;

        /// <summary>데이터 줄들.</summary>
        public IReadOnlyList<Row> Rows => _rows;

        private readonly List<string> _fields = new List<string>();
        private readonly List<Row> _rows = new List<Row>();

        private CsvTable(string name) { Name = name; }

        /// <summary>
        /// 글자 한 덩이를 표로. **첫 줄이 열 이름**이고 나머지가 데이터다.
        /// (`Dev_` 열은 변환기가 이미 뺐다 — 여기서는 있는 열을 그대로 쓴다.)
        /// </summary>
        public static CsvTable Parse(string name, string text)
        {
            var t = new CsvTable(name);
            if (string.IsNullOrEmpty(text))
                throw new FormatException($"[{name}] 표가 비었다");

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            int at = 0;
            while (at < lines.Length && lines[at].Trim().Length == 0) at++;
            if (at >= lines.Length)
                throw new FormatException($"[{name}] 열 이름 줄이 없다");

            t._fields.AddRange(SplitLine(lines[at]));
            at++;

            for (int i = at; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0) continue;   // 꼬리 빈 줄
                List<string> cells = SplitLine(lines[i]);
                t._rows.Add(new Row(t, i + 1, cells));
            }
            return t;
        }

        /// <summary>그 열이 몇 째인가. 없으면 −1 — 부르는 쪽이 「없어도 되는 열」을 가릴 수 있다.</summary>
        public int IndexOf(string field)
        {
            for (int i = 0; i < _fields.Count; i++)
                if (string.Equals(_fields[i], field, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// 이 열들이 다 있는가 — **없으면 어느 것이 없는지 다 적어서** 죽는다.
        /// 하나씩 알려 주면 고치는 사람이 표를 여러 번 열게 된다.
        /// </summary>
        public void Require(params string[] fields)
        {
            var missing = new List<string>();
            foreach (string f in fields)
                if (IndexOf(f) < 0) missing.Add(f);

            if (missing.Count > 0)
                throw new FormatException(
                    $"[{Name}] 열이 없다 — {string.Join(" · ", missing)}"
                    + $" (있는 열: {string.Join(" · ", _fields)})");
        }

        private static List<string> SplitLine(string line)
        {
            var cells = new List<string>();
            var buf = new System.Text.StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c != '"') { buf.Append(c); continue; }
                    // 따옴표 둘은 따옴표 하나다("" → ").
                    if (i + 1 < line.Length && line[i + 1] == '"') { buf.Append('"'); i++; }
                    else quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(buf.ToString()); buf.Length = 0; }
                else buf.Append(c);
            }
            cells.Add(buf.ToString());
            return cells;
        }

        /// <summary>데이터 한 줄.</summary>
        public sealed class Row
        {
            private readonly CsvTable _table;
            private readonly List<string> _cells;

            /// <summary>원본 줄 번호(1부터) — 오류 문장이 이걸 들고 간다.</summary>
            public int LineNumber { get; }

            internal Row(CsvTable table, int line, List<string> cells)
            {
                _table = table;
                LineNumber = line;
                _cells = cells;
            }

            /// <summary>글자 그대로. 없는 열이면 멈춘다.</summary>
            public string Text(string field)
            {
                int i = _table.IndexOf(field);
                if (i < 0)
                    throw new FormatException($"[{_table.Name}] {LineNumber}줄 — 열 '{field}' 가 없다");
                return i < _cells.Count ? _cells[i].Trim() : string.Empty;
            }

            /// <summary>
            /// 수. **빈 칸이면 <paramref name="whenEmpty"/>** 다 — 0 을 강요하지 않는다.
            /// 숫자가 아니면 **어느 줄 · 어느 열 · 무엇이 적혀 있었는지**를 들고 죽는다.
            /// </summary>
            public float Num(string field, float whenEmpty = 0f)
            {
                string s = Text(field);
                if (s.Length == 0) return whenEmpty;
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    return v;
                throw new FormatException(
                    $"[{_table.Name}] {LineNumber}줄 '{field}' 가 수가 아니다 — \"{s}\"");
            }

            /// <summary>정수. 소수가 적혀 있으면 **반올림하지 않고** 죽는다(값이 조용히 바뀌면 안 된다).</summary>
            public int Int(string field, int whenEmpty = 0)
            {
                string s = Text(field);
                if (s.Length == 0) return whenEmpty;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    return v;
                throw new FormatException(
                    $"[{_table.Name}] {LineNumber}줄 '{field}' 가 정수가 아니다 — \"{s}\"");
            }

            /// <summary>1/0. 빈 칸은 거짓이다.</summary>
            public bool Bool(string field) => Int(field, 0) != 0;

            /// <summary>
            /// enum 한 칸 — **수치로도 이름으로도 받는다.**
            ///
            /// ⚠️ 표에는 수치가 들어 있고(`Designer_Data` 가 사전이다), 사람이 이름을 적어 두는
            /// 일도 있다. 둘 다 받되 **모르는 값이면 죽는다** — 조용히 0(첫 항목)으로 떨어지면
            /// 「보병이 왜 이렇게 많지」가 된다.
            /// </summary>
            public T Enum<T>(string field) where T : struct, Enum
            {
                string s = Text(field);
                if (s.Length == 0)
                    throw new FormatException($"[{_table.Name}] {LineNumber}줄 '{field}' 가 비었다");

                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                {
                    foreach (T v in (T[])System.Enum.GetValues(typeof(T)))
                        if (Convert.ToInt32(v) == n) return v;
                    throw new FormatException(
                        $"[{_table.Name}] {LineNumber}줄 '{field}' 의 {n} 은 {typeof(T).Name} 에 없다");
                }

                if (System.Enum.TryParse(s, ignoreCase: true, out T parsed)) return parsed;
                throw new FormatException(
                    $"[{_table.Name}] {LineNumber}줄 '{field}' 의 \"{s}\" 는 {typeof(T).Name} 에 없다");
            }
        }
    }
}
