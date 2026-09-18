# -*- coding: utf-8 -*-
"""
**xlsx → CSV 변환기** (2026-09-18 · 사용자 확정 · 플랜 §85-12).

📌 **이쪽이 상시 경로다.** `make_tables.py` 는 첫 판을 뜨는 일회용이고, 이 스크립트는
   **표를 고칠 때마다** 돈다. 사람이 고치는 것은 xlsx, 게임이 읽는 것은 CSV 다.

    Tables/<NAME>_DATA.xlsx  ──(이 스크립트)──▶  Assets/_Project/GameData/<NAME>_DATA.csv
                                                        │
                                                        ▼
                                        CsvTable → CombatAssetGenerator → SO

규칙 (참고본 형식)
    · **`<NAME>_DATA` 시트만** 읽는다 — `Designer_*` 는 사람이 보는 칸이고 `Table_Info` 는 설명이다.
    · **1행 = 필드명** · 2행 = 한글 설명(버린다) · **3행부터 데이터**.
    · **`Dev_` 접두 열은 통째로 뺀다** — 컨버팅 제외 규약.
    · UTF-8 로 쓴다(BOM 없음).

⚠️ **값을 고치지 않는다.** 빈 칸은 빈 칸으로 내보낸다 — 0 으로 채우면 「안 정함」과
   「0 으로 정함」이 같아진다(적 표의 폴백 규약이 그 자리에 걸려 있다).

실행
    python Tables/xlsx_to_csv.py          (리포 뿌리에서 · 인자 없으면 Tables/ 아래 전부)
"""

import io
import os
import re
import sys
import zipfile

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "GameData")


def die(msg):
    raise SystemExit("[xlsx_to_csv] " + msg)


# ────────────────────────────── xlsx 읽기 ──────────────────────────────
#
# ⚠️ **표준 zip + 정규식으로만 읽는다** — 리포에 패키지를 안 들인다.
#    엑셀이 저장한 파일은 문자열을 `sharedStrings.xml` 로 뺄 수 있으므로 **둘 다** 받는다
#    (우리 생성기는 인라인을 쓰지만, 사람이 엑셀로 고쳐 저장하면 공유 문자열이 된다).

def shared_strings(z):
    if "xl/sharedStrings.xml" not in z.namelist():
        return []
    x = z.read("xl/sharedStrings.xml").decode("utf-8")
    out = []
    for si in re.finditer(r"<si>(.*?)</si>", x, re.S):
        # 한 칸이 여러 <t> 로 쪼개질 수 있다(서식이 섞인 글자) — 이어 붙인다.
        out.append("".join(re.findall(r"<t[^>]*>(.*?)</t>", si.group(1), re.S)))
    return [unescape(s) for s in out]


def unescape(s):
    return (s.replace("&lt;", "<").replace("&gt;", ">")
             .replace("&quot;", '"').replace("&apos;", "'").replace("&amp;", "&"))


def col_index(ref):
    """`B12` → 1(0부터). ⚠️ **빈 칸은 `<c>` 자체가 없다** — 자리를 이걸로 잡는다."""
    letters = re.match(r"([A-Z]+)", ref).group(1)
    n = 0
    for ch in letters:
        n = n * 26 + (ord(ch) - 64)
    return n - 1


def sheet_rows(z, path, shared):
    x = z.read(path).decode("utf-8")
    rows = []
    for rm in re.finditer(r"<row[^>]*>(.*?)</row>", x, re.S):
        cells = {}
        for cm in re.finditer(r'<c r="([A-Z]+\d+)"([^>]*)>(.*?)</c>', rm.group(1), re.S):
            ref, attrs, body = cm.group(1), cm.group(2), cm.group(3)
            i = col_index(ref)
            if 't="inlineStr"' in attrs:
                cells[i] = unescape("".join(re.findall(r"<t[^>]*>(.*?)</t>", body, re.S)))
            elif 't="s"' in attrs:
                v = re.search(r"<v>(.*?)</v>", body, re.S)
                cells[i] = shared[int(v.group(1))] if v else ""
            elif 't="str"' in attrs:
                v = re.search(r"<v>(.*?)</v>", body, re.S)
                cells[i] = unescape(v.group(1)) if v else ""
            else:
                v = re.search(r"<v>(.*?)</v>", body, re.S)
                cells[i] = v.group(1) if v else ""
        width = (max(cells) + 1) if cells else 0
        rows.append([cells.get(i, "") for i in range(width)])
    return rows


def data_sheet(path, name):
    """`<NAME>_DATA` 시트의 행들. 없으면 멈춘다 — 엉뚱한 시트를 대신 읽지 않는다."""
    with zipfile.ZipFile(path) as z:
        wb = z.read("xl/workbook.xml").decode("utf-8")
        names = re.findall(r'<sheet name="([^"]+)"', wb)
        rels = z.read("xl/_rels/workbook.xml.rels").decode("utf-8")
        targets = dict(re.findall(r'Id="(rId\d+)"[^>]*Target="([^"]+)"', rels))
        ids = re.findall(r'<sheet[^>]*r:id="(rId\d+)"', wb)

        if name not in names:
            die("%s 에 '%s' 시트가 없다 (있는 것: %s)"
                % (os.path.basename(path), name, " · ".join(names)))

        target = targets[ids[names.index(name)]]
        if not target.startswith("xl/"):
            target = "xl/" + target.lstrip("/")
        return sheet_rows(z, target, shared_strings(z))


# ────────────────────────────── CSV 쓰기 ──────────────────────────────

def csv_cell(s):
    s = "" if s is None else str(s)
    if any(c in s for c in ',"\n'):
        return '"' + s.replace('"', '""') + '"'
    return s


def convert(xlsx_path):
    name = os.path.splitext(os.path.basename(xlsx_path))[0]
    rows = data_sheet(xlsx_path, name)
    if len(rows) < 3:
        die("%s 에 데이터 줄이 없다(1행 필드 · 2행 설명 · 3행부터 데이터)" % name)

    fields = rows[0]
    # ⚠️ **`Dev_` 접두는 통째로 뺀다** — 자리를 먼저 골라 두고 그 자리만 옮긴다.
    keep = [i for i, f in enumerate(fields) if f and not f.startswith("Dev_")]
    if not keep:
        die("%s 에 남는 열이 없다 — 전부 Dev_ 접두인가" % name)

    out = [",".join(csv_cell(fields[i]) for i in keep)]
    for r in rows[2:]:
        if not any(str(c).strip() for c in r):
            continue   # 빈 줄은 건너뛴다 — 엑셀은 빈 행을 곧잘 남긴다
        out.append(",".join(csv_cell(r[i] if i < len(r) else "") for i in keep))

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    dst = os.path.join(OUT_DIR, name + ".csv")
    io.open(dst, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")

    dropped = [fields[i] for i in range(len(fields)) if i not in keep and fields[i]]
    print("  %s → %s (%d행 · 열 %d · 뺀 열 %s)"
          % (os.path.basename(xlsx_path), os.path.relpath(dst, ROOT),
             len(out) - 1, len(keep), " · ".join(dropped) or "없음"))
    return dst


def main():
    args = sys.argv[1:]
    paths = ([os.path.join(HERE, a) if not os.path.isabs(a) else a for a in args]
             or sorted(os.path.join(HERE, f) for f in os.listdir(HERE) if f.endswith(".xlsx")))
    if not paths:
        die("Tables/ 아래에 xlsx 가 없다")

    print("[xlsx_to_csv] 표 → CSV")
    for p in paths:
        convert(p)
    print("[xlsx_to_csv] 끝 — 게임이 읽는 것은 CSV 다.")


if __name__ == "__main__":
    main()
