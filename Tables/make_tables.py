# -*- coding: utf-8 -*-
"""
밸런스 표 **첫 판 생성기** (2026-09-18 · 사용자 확정 · 플랜 §85-12).

⚠️⚠️⚠️ **한 번 쓰는 도구다(bootstrap). 다시 돌리면 표의 편집을 덮는다.**

    사용자 확정으로 **값의 원천은 표(CSV)** 가 된다. 이 스크립트는 그 표의 **첫 판**을
    지금 리포의 사실(`balance_v4.json` · ScriptableObject · 코드 상수)에서 떠 오는 일만 한다.
    첫 판이 서고 나면 **값을 고치는 곳은 표**이고, 이 스크립트를 상시 경로로 두면
    방향이 뒤집힌다(표 → json 이 아니라 json → 표가 되어 표의 편집이 사라진다).

    📌 그래서 첫 판 뒤에는 **부르지 않는다.** 되살릴 일이 생기면 그때 이 경고를 먼저 읽을 것.

왜 값을 코드에 안 박는가
    박으면 **같은 수가 두 곳에 산다**(지침 §7). 첫 판만이라도 리포에서 읽어 떠야
    「표의 첫 판 = 지금 사실」이 **증명되는 것**이지 주장이 아니게 된다.

왜 openpyxl 을 안 쓰는가
    리포에 패키지를 들이지 않기로 했다. xlsx 는 zip 안의 XML 몇 장이라
    표준 라이브러리(`zipfile`)만으로 쓸 수 있다.

형식 (참고본 `SKILL_DATA.xlsx`)
    시트 넷 —
      <NAME>_DATA   1행 영문 필드 · 2행 한글 간략 설명 · 3행~ 데이터
                    `Dev_` 접두 열 = **컨버팅 제외**(사람이 보는 칸)
      Designer_Table  같은 표를 enum 대신 **한글 라벨**로
      Designer_Data   enum 사전(필드별 라벨↔수치 세로)
      Table_Info      INDEX · 필드명 · 간략 설명 · 데이터 타입 · 데이터 예시 · 필드 설명/값 · 비고

실행
    python Tables/make_tables.py          (리포 뿌리에서)
"""

import io
import json
import os
import re
import sys
import zipfile

# ⚠️ 윈도우 콘솔 기본 코드페이지(cp949)는 ⚠️·— 같은 글자를 못 찍는다 —
#    거기서 죽으면 **표는 멀쩡한데 스크립트가 실패한 것처럼** 보인다.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

SO = os.path.join(ROOT, "Assets", "_Project", "ScriptableObjects")
SRC = os.path.join(ROOT, "Assets", "_Project", "Scripts")


# ────────────────────────────── 읽기 ──────────────────────────────
#
# ⚠️ **없으면 멈춘다.** 못 읽은 칸을 0 으로 채우면 표가 조용히 거짓말을 한다.

def die(msg):
    raise SystemExit("[make_tables] " + msg)


def read_text(path):
    if not os.path.exists(path):
        die("파일이 없다 — " + path)
    return io.open(path, encoding="utf-8").read()


def balance():
    return json.loads(read_text(os.path.join(ROOT, "balance_v4.json")))


def param(bal, key):
    """`params[]` 한 칸 — 값과 confirmed 를 함께 준다(둘은 늘 같이 다닌다)."""
    for p in bal.get("params", []):
        if p.get("key") == key:
            return p.get("value"), bool(p.get("confirmed"))
    die("params 에 '%s' 가 없다" % key)


def yaml_num(text, field):
    """SO(YAML) 한 줄에서 수 하나. ⚠️ 중첩은 안 본다 — 쓰는 칸이 전부 평면이다."""
    m = re.search(r"^\s*%s:\s*([-\d.]+)\s*$" % re.escape(field), text, re.M)
    if not m:
        die("자산에서 '%s' 를 못 찾았다" % field)
    return float(m.group(1))


def cs_const(path, name):
    """C# 상수 하나. ⚠️ **코드가 값의 집인 자리**라 여기서만 읽는다."""
    text = read_text(path)
    m = re.search(r"\b%s\s*=\s*([-\d.]+)f?\s*;" % re.escape(name), text)
    if not m:
        die("%s 에서 상수 '%s' 를 못 찾았다" % (os.path.basename(path), name))
    return float(m.group(1))


def num(x):
    """1.0 은 1 로 — 표에 소수점이 붙으면 사람이 다른 값으로 읽는다."""
    f = float(x)
    return int(f) if f == int(f) else f


# ────────────────────────────── xlsx 쓰기 ──────────────────────────────
#
# 📌 **인라인 문자열을 쓴다**(`t="inlineStr"`). sharedStrings 를 안 쓰면 파일이 한 장 줄고,
#    엑셀·구글 시트 둘 다 그대로 연다.

def esc(s):
    return (str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
            .replace('"', "&quot;"))


def col_name(i):
    """0 → A · 26 → AA."""
    s = ""
    i += 1
    while i:
        i, r = divmod(i - 1, 26)
        s = chr(65 + r) + s
    return s


def cell(ref, v):
    if isinstance(v, bool):          # ⚠️ bool 이 int 보다 먼저다 — True 는 int 이기도 하다
        v = 1 if v else 0
    if isinstance(v, (int, float)):
        return '<c r="%s"><v>%s</v></c>' % (ref, num(v))
    if v is None or v == "":
        return ""
    return ('<c r="%s" t="inlineStr"><is><t xml:space="preserve">%s</t></is></c>'
            % (ref, esc(v)))


def sheet_xml(rows):
    out = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
           '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
           '<sheetData>']
    for r, row in enumerate(rows, start=1):
        cells = "".join(cell(col_name(c) + str(r), v) for c, v in enumerate(row))
        out.append('<row r="%d">%s</row>' % (r, cells))
    out.append("</sheetData></worksheet>")
    return "".join(out)


def write_xlsx(path, sheets):
    """sheets = [(이름, rows), ...] — 차례가 곧 시트 차례다."""
    n = len(sheets)

    types = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
             '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
             '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
             '<Default Extension="xml" ContentType="application/xml"/>'
             '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>']
    for i in range(1, n + 1):
        types.append('<Override PartName="/xl/worksheets/sheet%d.xml" '
                     'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' % i)
    types.append("</Types>")

    rels = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" '
            'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" '
            'Target="xl/workbook.xml"/></Relationships>')

    wb = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
          '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
          'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>']
    for i, (name, _) in enumerate(sheets, start=1):
        wb.append('<sheet name="%s" sheetId="%d" r:id="rId%d"/>' % (esc(name), i, i))
    wb.append("</sheets></workbook>")

    wbrels = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
              '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">']
    for i in range(1, n + 1):
        wbrels.append('<Relationship Id="rId%d" '
                      'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" '
                      'Target="worksheets/sheet%d.xml"/>' % (i, i))
    wbrels.append("</Relationships>")

    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml", "".join(types))
        z.writestr("_rels/.rels", rels)
        z.writestr("xl/workbook.xml", "".join(wb))
        z.writestr("xl/_rels/workbook.xml.rels", "".join(wbrels))
        for i, (_, rows) in enumerate(sheets, start=1):
            z.writestr("xl/worksheets/sheet%d.xml" % i, sheet_xml(rows))


# ────────────────────────────── enum 사전 ──────────────────────────────
#
# ⚠️ **한 곳에만 적는다** — `Designer_Table`(라벨로 바꿔 보이기)과 `Designer_Data`(사전 시트)가
#    같은 사전을 쓴다. 둘이 따로 들면 갈리는 날이 온다.

def cs_enum(cs_rel, enum_name, labels):
    """
    C# 열거형을 **코드에서 읽어** [(라벨, 수치)] 로.

    ⚠️⚠️ **사전을 손으로 적지 않는다**(2026-09-19 · 실제로 두 번 틀렸다).
       NodeType 을 눈대중으로 적었더니 「에너지 3」이 「기초 가공소 3」으로 실렸다 —
       표가 **다른 노드를 가리키는** 사전을 들고 있었던 것이다.
       수치는 코드가 내고, 사람이 주는 것은 **한글 라벨뿐**이다.

    ⚠️ 라벨이 없는 항목이 나오면 **멈춘다** — 조용히 영문 이름을 적으면
       Designer 시트가 반만 한글이 된다.
    """
    text = read_text(os.path.join(SRC, cs_rel))
    m = re.search(r"enum\s+%s\s*\{(.*?)\}" % re.escape(enum_name), text, re.S)
    if not m:
        die("%s 에서 열거형 '%s' 를 못 찾았다" % (cs_rel, enum_name))

    out = []
    for name, value in re.findall(r"^\s*([A-Za-z_]\w*)\s*=\s*(\d+)\s*,",
                                  m.group(1), re.M):
        if name not in labels:
            die("%s.%s 의 한글 라벨이 없다 — make_tables 의 라벨 표에 넣어라" % (enum_name, name))
        out.append((labels[name], int(value)))
    if not out:
        die("%s 에서 항목을 하나도 못 읽었다" % enum_name)
    return out


ENUMS = {
    "EnemyKey": [("보병", 1), ("포격", 2), ("장갑", 3), ("강적", 4)],
    "Role":     [("근접", 1), ("원거리", 2), ("길막", 3), ("보스", 4)],
    "RobotID":  [("로봇A", 1), ("로봇B", 2)],
    "AmmoKind": [("관통", 1), ("표준", 2), ("폭발", 3), ("누적형 드론", 4), ("광역형 드론", 5)],
    # ── 둘째 묶음(2026-09-19) ──
    #
    # ⚠️⚠️ **Type·Kind 는 코드에서 읽는다** — 손으로 적었다가 틀렸다(위 `cs_enum` 주석).
    #    ReqType·PowerModel 은 json 의 글자라 코드에 열거형이 없다 — 그 둘만 손으로 적되,
    #    **json 에 실제로 든 값만** 담았고 새 값이 오면 `stage()` 가 멈춘다.
    "Type": cs_enum(os.path.join("Data", "NodeEnums.cs"), "NodeType", {
        "Core": "코어", "Processing": "변환기", "MunitionsBasic": "기초 가공소",
        "Energy": "에너지", "Storage": "저장", "Shield": "보호막",
        "Booster": "부스터", "MunitionsComplex": "복합 가공소",
    }),
    "Kind": cs_enum(os.path.join("Data", "ModuleDefinition.cs"), "ModuleKind", {
        "Output": "생산량", "Rate": "생산속도",
    }),
    "ReqType":    [("고정", 1), ("수식", 2), ("예산", 3)],
    "PowerModel": [("물류", 1), ("강화", 2), ("태그", 3), ("버스트", 4)],
}


def label_of(field, value):
    for lab, v in ENUMS.get(field, []):
        if v == value:
            return lab
    return value


def designer_rows(fields, rows):
    """enum 열만 한글 라벨로 바꾼 같은 표."""
    out = [list(fields[0]), list(fields[1])]
    for row in rows:
        out.append([label_of(fields[0][i], v) for i, v in enumerate(row)])
    return out


def designer_data_rows(fields):
    """
    enum 사전 — **필드별 가로 블록**(참고본 `SKILL_DATA.xlsx` 서식 · 2026-09-18 사용자 확정).

        EnemyKey  |        |      | Role      |
        한글 라벨 | 수치   |      | 한글 라벨 | 수치
        보병      | 1      |      | 근접      | 1
        포격      | 2      |      | 원거리    | 2
        ...

    🗑️ 구 서식 폐기 — 세로 한 표(`Field · Label · Value`)였다. 값은 같지만 **사람이 읽는 꼴**이
       참고본과 달랐다(사용자가 전 직장 형식을 그대로 쓰기로 했다).

    ⚠️ **블록 사이에 빈 열 하나**를 둔다 — 붙이면 어디까지가 한 필드인지 안 읽힌다.
    ⚠️ 표에 없는 enum 은 안 적는다.
    """
    blocks = [(f, ENUMS[f]) for f in fields[0] if f in ENUMS]
    if not blocks:
        return [[]]

    height = max(len(pairs) for _, pairs in blocks) + 2   # 필드명 줄 + 헤더 줄
    rows = [[] for _ in range(height)]

    for bi, (field, pairs) in enumerate(blocks):
        if bi:                                   # 블록 사이 빈 열
            for r in rows:
                r.append("")
        rows[0] += [field, ""]
        rows[1] += ["한글 라벨", "수치"]
        for i in range(height - 2):
            if i < len(pairs):
                rows[i + 2] += [pairs[i][0], pairs[i][1]]
            else:
                rows[i + 2] += ["", ""]
    return rows


def info_rows(name, info):
    """
    표 설명 — **참고본 배치**(2026-09-18 사용자 확정).

        1행  <NAME>_DATA 테이블 설명        ← 제목
        2행  (빈 줄)
        3행  INDEX · 필드명 · 간략 설명 · 데이터 타입 · 데이터 예시 · 필드 설명 / 값 · 비고
        4행~ 내용

    🗑️ 구 배치 폐기 — 1행 영문 헤더 + 2행 한글이었다. 데이터 시트는 그 꼴이 맞지만
       **설명 시트는 사람만 읽는 칸**이라 참고본처럼 한글 헤더 하나면 된다.
    """
    kor = ["INDEX", "필드명", "간략 설명", "데이터 타입", "데이터 예시", "필드 설명 / 값", "비고"]
    rows = [["%s 테이블 설명" % name], [], kor]
    for i, r in enumerate(info, start=1):
        rows.append([i] + list(r))
    return rows


# ⚠️⚠️ **이미 있는 표는 안 덮는다**(2026-09-19 · 오늘 실제로 한 번 덮었다).
#
# 파일 맨 앞이 「다시 돌리면 표의 편집을 덮는다」고 경고하고 있었지만 **경고는 문이 아니다** —
# 둘째 묶음을 붙이려고 한 번 돌렸더니 기존 셋이 통째로 다시 써졌다(그날은 내용이 같아
# CSV diff 0 이었지만, 사람이 표를 고친 뒤였다면 **그 편집이 사라졌을 것이다**).
# 이제 **경고를 문으로 바꾼다** — 있는 파일은 건너뛰고, 정말 덮으려면 `--force` 를 준다.
FORCE = False


def build(path, name, fields, rows, info):
    if os.path.exists(path) and not FORCE:
        print("  %s — **이미 있다. 안 덮는다**(정말 덮으려면 --force)" % os.path.basename(path))
        return
    write_xlsx(path, [
        (name, [list(fields[0]), list(fields[1])] + [list(r) for r in rows]),
        ("Designer_Table", designer_rows(fields, rows)),
        ("Designer_Data", designer_data_rows(fields)),
        ("Table_Info", info_rows(name, info)),
    ])
    print("  %s — 데이터 %d행" % (os.path.basename(path), len(rows)))


# ────────────────────────────── 표 셋 ──────────────────────────────

ENEMY_ID = {"infantry": 1, "artillery": 2, "armor": 3, "boss": 4}


def stage_comp(bal):
    fields = (
        ["Dev_Index", "Dev_Desc", "ID", "StageID", "EnemyKey", "Count", "Hp", "Def", "Confirmed"],
        ["개발용 번호", "개발용 설명", "고유 번호", "스테이지", "적 종류", "마리 수", "체력", "방어", "확정 여부"],
    )
    rows, i = [], 0
    for st in bal.get("stages", []):
        sid = st.get("id")
        conf = 1 if st.get("compConfirmed") else 0
        for c in (st.get("composition") or []):
            i += 1
            key = c["enemy"]
            if key not in ENEMY_ID:
                die("모르는 적 종류 — " + key)
            rows.append([i, "%s %s" % (sid, key), i, sid, ENEMY_ID[key],
                         num(c["count"]), num(c["hp"]), num(c["def"]), conf])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Desc", "개발용 설명", "string", "S1 infantry", "컨버팅 제외(Dev_ 접두)", "스테이지와 적을 한눈에"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["StageID", "스테이지", "string", "S1", "S1~S6", "balance_v4.json stages[].id"],
        ["EnemyKey", "적 종류", "enum", 1, "보병1 포격2 장갑3 강적4", "Designer_Data 참조"],
        ["Count", "마리 수", "int", 100, "그 스테이지에 나오는 수", "json composition[].count"],
        ["Hp", "체력", "int", 20, "한 마리 체력",
         "json composition[].hp · §72-14 가정값 · **HP 상승폭 절반 09-18 사용자 확정** · S5·S6 불변"],
        ["Def", "방어", "int", 0, "한 마리 방어", "json composition[].def · 09-18 에 안 바뀌었다"],
        ["Confirmed", "확정 여부", "bool", 0, "1=확정 0=가정",
         "json stages[].compConfirmed 그대로 — **S1~S4 는 0 · S5·S6 은 1**"],
    ]
    build(os.path.join(HERE, "STAGE_COMP_DATA.xlsx"), "STAGE_COMP_DATA", fields, rows, info)
    return rows


def enemy(bal):
    tuning = read_text(os.path.join(SO, "CombatTuning.asset"))
    move = yaml_num(tuning, "enemyMoveSpeedTbd")
    rng = yaml_num(tuning, "enemyAttackRangeTbd")
    itv = yaml_num(tuning, "enemyAttackIntervalTbd")

    rule = os.path.join(SRC, "Core", "Combat", "EnemyAttackRule.cs")
    art_range = cs_const(rule, "ArtilleryRangeAssumed")
    art_proj = cs_const(rule, "ProjectileSpeedAssumed")
    boss_scale = cs_const(os.path.join(SRC, "Data", "ArtSpec.cs"), "BossViewScale")

    fields = (
        ["Dev_Index", "Dev_Desc", "ID", "EnemyKey", "TID_Name", "Role", "Atk", "MoveSpeed",
         "AttackRange", "AttackInterval", "ProjectileSpeed", "ViewScale", "ArtKey", "Confirmed"],
        ["개발용 번호", "개발용 설명", "고유 번호", "적 종류", "표시 이름", "역할", "공격력", "이동 속도",
         "사거리", "공격 주기", "투사체 속도", "그림 배율", "그림 키", "확정 여부"],
    )

    spec = [
        ("infantry", "보병", 1, "근접 — 걸어와서 때린다", "mob_infantry", 1),
        ("artillery", "포격", 2, "원거리 — 멀리서 포탄을 쏜다", "mob_cannon", 1),
        ("armor", "장갑", 3, "길막 — 두껍고 느리다", "mob_armor", 1),
        ("boss", "강적", 4, "보스 — 한 마리뿐", "boss", boss_scale),
    ]

    rows = []
    for i, (key, kor, role, desc, art_key, scale) in enumerate(spec, start=1):
        atk = yaml_num(read_text(os.path.join(SO, "Enemies", "Enemy_%s.asset" % key)), "atk")
        # ⚠️ **사거리·투사체는 병종 규칙이 이긴다**(자산이 0 이라 2단으로 떨어진다).
        #    포격만 규칙 값이 있고 나머지는 튜닝 폴백이다 — 그 사실을 표가 그대로 적는다.
        row_range = art_range if key == "artillery" else rng
        row_proj = art_proj if key == "artillery" else 0
        rows.append([i, "%s(%s)" % (kor, key), i, ENEMY_ID[key], kor, role,
                     num(atk), num(move), num(row_range), num(itv), num(row_proj),
                     num(scale), art_key, 0])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Desc", "개발용 설명", "string", "보병(infantry)", "컨버팅 제외(Dev_ 접두)",
         "json role 문장이 여기로 온다"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["EnemyKey", "적 종류", "enum", 1, "보병1 포격2 장갑3 강적4", "Designer_Data 참조"],
        ["TID_Name", "표시 이름", "string", "보병", "화면에 보이는 한글 이름", "EnemyDefinition.displayName"],
        ["Role", "역할", "enum", 1, "근접1 원거리2 길막3 보스4", "json role 문장 → enum"],
        ["Atk", "공격력", "int", 6, "한 대의 피해", "Enemy_*.asset 의 atk"],
        ["MoveSpeed", "이동 속도", "float", 1.5, "유닛/초",
         "**자산이 0 이라 CombatTuning.enemyMoveSpeedTbd 폴백** · ⚠️ 가정 · "
         "⚠️⚠️ 자산은 **1.5** 인데 C# 기본값은 1.2 다 — **자산이 이긴다.** 1.2 를 적은 글은 낡았다"],
        ["AttackRange", "사거리", "float", 1.0, "표면에서 이만큼 떨어져 때린다",
         "보병·장갑·강적 = CombatTuning 폴백(⚠️ 가정) · **포격만 EnemyAttackRule.ArtilleryRangeAssumed = 7**(⚠️ 가정 · 플랜 6~8 의 가운데)"],
        ["AttackInterval", "공격 주기", "float", 1, "초",
         "**자산이 0 이라 CombatTuning.enemyAttackIntervalTbd 폴백** · ⚠️ 가정 · "
         "⚠️⚠️ 자산은 **1** 인데 C# 기본값은 1.5 다 — **자산이 이긴다**"],
        ["ProjectileSpeed", "투사체 속도", "float", 0, "0 = 즉발(포탄이 없다)",
         "포격만 EnemyAttackRule.ProjectileSpeedAssumed = **4.2**(✅ 사용자 확정 09-18 「지금 속도의 70%」) · 나머지 0"],
        ["ViewScale", "그림 배율", "int", 1, "보스만 2", "ArtSpec.BossViewScale"],
        ["ArtKey", "그림 키", "string", "mob_infantry", "아트 자산 이름", "Art/Units 아래 폴더 이름"],
        ["Confirmed", "확정 여부", "bool", 0, "1=확정 0=가정",
         "**넷 다 0** — Atk 은 자산 값이라 확정 플래그가 원래 없고, 나머지 넷은 가정 또는 폴백이다. "
         "⚠️ ProjectileSpeed 4.2 **한 칸만 사용자 확정**이라 행 단위 플래그로는 못 적는다(설계 판정 자리)"],
    ]
    build(os.path.join(HERE, "ENEMY_DATA.xlsx"), "ENEMY_DATA", fields, rows, info)

    # 📌 **규약을 표에도 적어 둔다** — 0 을 「없다」로 읽으면 폴백이 안 보인다.
    print("     ⚠️ 0 = 안 정함 → 병종 규칙 · 튜닝 폴백 (Table_Info 비고)")
    return rows


def weapon(bal):
    tuning = read_text(os.path.join(SO, "CombatTuning.asset"))
    spr = yaml_num(tuning, "shotsPerRound")
    sdf = yaml_num(tuning, "shotDamageFactor")
    hit = yaml_num(tuning, "droneHitIntervalTbd")
    frac = yaml_num(tuning, "droneDamageFractionTbd")

    fields = (
        ["Dev_Index", "Dev_Desc", "ID", "RobotID", "AmmoKind", "Damage", "ShotsPerSec",
         "LineSpec", "ShotsPerRound", "ShotDamageFactor", "DamagePerUnit", "Charge",
         "AoeDamageFactor", "HitInterval", "DamageFraction", "Confirmed"],
        ["개발용 번호", "개발용 설명", "고유 번호", "로봇", "탄종", "발당 피해", "초당 발사",
         "라인 스펙", "한 발당 나가는 수", "한 발 피해 배수", "기당 피해 좌표", "충전량",
         "광역 피해 배수(파생·안 읽음)", "타격 간격", "기당 피해 몫", "확정 여부"],
    )

    rows = []
    for i, (kor, ammo_id, idx) in enumerate(
            [("관통", 1, 0), ("표준", 2, 1), ("폭발", 3, 2)], start=1):
        d, dc = param(bal, "dA%d" % idx)
        p, pc = param(bal, "pA%d" % idx)
        s, sc = param(bal, "specA%d" % idx)
        rows.append([i, "로봇A %s탄" % kor, i, 1, ammo_id, num(d), num(p), num(s),
                     num(spr), num(sdf), 0, 0, 0, 0, 0, 1 if (dc and pc and sc) else 0])

    charge, charge_conf = param(bal, "dB")
    # ✅ **광역형은 제 좌표를 쓴다**(2026-09-19 · `260918_W02` 5장 이관).
    #    🗑️ 구 `droneAoeDamageFactor` 0.5 폐기 — 배수는 이제 좌표 둘의 몫으로 **파생**된다.
    #    ⚠️ 여기서 0.5 를 적으면 이관이 무효다(같은 값이 다시 두 자리에 산다).
    aoe_charge, aoe_conf = param(bal, "dBAoe")
    # ⚠️⚠️ **기당 피해 좌표와 충전량은 다른 축이다**(W02 3장 · 설계 검토 ② 09-19).
    #    · 좌표  — 누적형 100 · 광역형 **50** (무기 스펙트럼이 가진 수)
    #    · 충전량 — 두 종 **공통 100** (json dB · 코드가 쓰는 값 · 광역형도 100 이다)
    #    한 칸에 두면 「광역형 충전량 50」으로 읽힌다 — 그것이 09-18 에 수명이 두 배가 된
    #    결함의 뿌리였다. **열을 갈라 둔다.**
    rows.append([4, "로봇B 누적형 드론", 4, 2, 4, 0, 0, 0, 0, 0,
                 num(charge), num(charge), 1, num(hit), num(frac),
                 1 if charge_conf else 0])
    rows.append([5, "로봇B 광역형 드론", 5, 2, 5, 0, 0, 0, 0, 0,
                 num(aoe_charge), num(charge), num(aoe_charge) / num(charge),
                 num(hit), num(frac), 1 if (aoe_conf and charge_conf) else 0])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Desc", "개발용 설명", "string", "로봇A 관통탄", "컨버팅 제외(Dev_ 접두)", "행을 한눈에"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["RobotID", "로봇", "enum", 1, "로봇A1 로봇B2", "Designer_Data 참조"],
        ["AmmoKind", "탄종", "enum", 1, "관통1 표준2 폭발3 누적형4 광역형5", "Designer_Data 참조"],
        ["Damage", "발당 피해", "int", 20, "한 발이 주는 피해", "json params dA0/dA1/dA2 (✅ confirmed)"],
        ["ShotsPerSec", "초당 발사", "float", 2, "그 줄이 1초에 쏘는 수",
         "json params pA0/pA1/pA2 (✅ confirmed) · ⚠️ **LineSpec 과 어느 쪽이 정본인지 설계 미결**(§73-28)"],
        ["LineSpec", "라인 스펙", "float", 5, "줄 하나의 명목 발사율",
         "json params specA0/specA1/specA2 (✅ confirmed) · ⚠️ **ShotsPerSec 과 정본 미결**(§73-28)"],
        ["ShotsPerRound", "한 발당 나가는 수", "int", 3, "탄 한 발을 소비할 때 나가는 발",
         "CombatTuning.shotsPerRound · ✅ **사용자 확정 09-18**(세 발) · 1 이면 구 거동"],
        ["ShotDamageFactor", "한 발 피해 배수", "float", 0.5, "쪼갠 한 발의 피해 배수",
         "CombatTuning.shotDamageFactor · ✅ **사용자 확정 09-18**(1/2) · "
         "⚠️ 총량은 3 x 1/2 = **1.5배** — 「1초 피해 = 명목 출력」 계약이 그만큼 바뀐다"],
        ["DamagePerUnit", "기당 피해 좌표", "int", 100, "드론 한 기가 한 번에 주는 피해",
         "✅ **무기 스펙트럼 좌표** — 누적형 dB 100 · 광역형 dBAoe **50**(2026-09-19 이관 · "
         "`260918_W02` 5장). ⚠️ **이 칸이 광역형의 대가를 든다** — AoeDamageFactor 는 "
         "여기서 나눠 나오는 파생값이다. 로봇A 행은 0(해당 없음)"],
        ["Charge", "충전량", "int", 100, "드론 한 기가 가진 피해 총량(= 수명)",
         "json params dB (✅ confirmed) — **두 종이 같은 100 이다.** "
         "⚠️⚠️ **기당 피해와 다른 축이다**(`260918_W02` 3장). 광역형의 기당 피해가 "
         "절반이라고 **충전량까지 절반이 아니다** — 09-18 에 그 둘을 한 수로 두어 "
         "**광역형 수명이 두 배가 된** 결함이 있었다. 로봇A 행은 0(해당 없음)"],
        ["AoeDamageFactor", "광역 피해 배수", "float", 1, "표적 하나에 주는 피해의 비",
         "🗑️ **읽는 곳이 없다 — 파생값이다**(2026-09-19 이관 완료). 생성기는 Charge 둘을 "
         "나눠 배수를 낸다(50 ÷ 100 = 0.5). 열을 남긴 까닭은 사람이 **좌표와 몫을 견줄 수 "
         "있어야** 하기 때문이다 — 여기 수를 고쳐도 게임은 안 바뀐다. 고칠 곳은 Charge 다"],
        ["HitInterval", "타격 간격", "float", 0.5, "붙어서 몇 초마다 때리는가",
         "CombatTuning.droneHitIntervalTbd · ⚠️ 가정"],
        ["DamageFraction", "기당 피해 몫", "float", 0.1, "한 번에 쓰는 충전량의 비",
         "CombatTuning.droneDamageFractionTbd · ⚠️ 가정(열 번에 나눠 쓴다)"],
        ["Confirmed", "확정 여부", "bool", 1, "1=확정 0=가정",
         "**행마다 원천의 confirmed 를 그대로** — 로봇A 셋과 누적형은 1, "
         "광역형은 droneAoeDamageFactor 가 미확정이라 0"],
    ]
    build(os.path.join(HERE, "WEAPON_DATA.xlsx"), "WEAPON_DATA", fields, rows, info)
    return rows


def main():
    """
    ⚠️ **첫 판 생성기다.** 있는 표는 안 덮는다(`build` 의 문) — `--force` 로만 덮는다.

    인자로 표 이름을 주면 그것만 뜬다:  `python Tables/make_tables.py NODE_DATA`
    """
    global FORCE
    args = [a for a in sys.argv[1:] if a != "--force"]
    FORCE = "--force" in sys.argv

    print("[make_tables] 첫 판 생성기 — 있는 표는 건너뛴다"
          + (" · ⚠️ **--force: 덮는다**" if FORCE else ""))
    bal = balance()

    makers = {
        "STAGE_COMP_DATA": lambda: stage_comp(bal),
        "ENEMY_DATA": lambda: enemy(bal),
        "WEAPON_DATA": lambda: weapon(bal),
    }
    makers.update(SECOND_BATCH)

    todo = args or list(makers)
    for name in todo:
        if name not in makers:
            die("모르는 표 — %s (있는 것: %s)" % (name, " · ".join(makers)))
        makers[name]()

    print("[make_tables] 끝 — Tables/ 아래 %d 장을 봤다." % len(todo))



# ══════════════════════════════════════════════════════════════════════
#  표 아홉 + TEXT — 둘째 묶음 (2026-09-19 사용자 확정 · 플랜 §86-4)
# ══════════════════════════════════════════════════════════════════════
#
# ⚠️⚠️ **범위가 좁다**(09-19 사용자 정정) — 지금은 **xlsx 생성 + CSV 변환까지**다.
#    로더·생성기 배선 · 코드 리터럴 치환 · json 폐기 표기는 **영상 이후**다.
#    그래서 이 묶음이 만든 CSV 는 **아직 아무도 안 읽는다** — 기존 셋의 배선은 불변이다.
#
# ⚠️ **값은 지금 사실 그대로다.** 새 값 0 — json · C# · 자산에서 읽어 옮기기만 한다.
#    읽을 수 없는 칸은 **비워 두지 않고 멈춘다**(`die`).


def unity_text(s):
    r"""
    유니티 YAML 의 이스케이프를 사람 글자로.

    ⚠️⚠️ **`\u` 만 풀면 모자란다**(2026-09-19 실측). 유니티는 라틴-1 범위를 `\xB7` 로 적는다 —
       「기초재료·부품」의 가운뎃점이 그것이었고, `\u` 만 풀던 판에서는 표에
       **`기초재료\xB7부품` 이 글자 그대로** 실렸다. 대조 시험이 그 자리를 잡았다.

    📌 **정규식을 안 쓴다** — 역슬래시 패턴이 편집을 지날 때마다 한 겹씩 사라진다
       (오늘 두 번 겪었다). 글자를 직접 훑는다.

    따옴표가 없는 칸(영문·수)은 손대지 않고 그대로 돌려준다.
    """
    s = s.strip()
    if not (len(s) >= 2 and s[0] == chr(34) and s[-1] == chr(34)):
        return s

    body = s[1:-1]
    BS = chr(92)
    out = []
    i = 0
    while i < len(body):
        c = body[i]
        if c != BS:
            out.append(c)
            i += 1
            continue

        i += 1
        if i >= len(body):
            out.append(BS)          # 끝에 홀로 남은 역슬래시 — 그대로 둔다
            break

        e = body[i]
        if e == "u" and i + 4 < len(body) + 1:
            out.append(chr(int(body[i + 1:i + 5], 16)))
            i += 5
        elif e == "x" and i + 2 < len(body) + 1:
            out.append(chr(int(body[i + 1:i + 3], 16)))
            i += 3
        elif e == "n":
            out.append(chr(10)); i += 1
        elif e == "t":
            out.append(chr(9)); i += 1
        else:
            out.append(e); i += 1   # \\ · \" 등 — 다음 글자를 그대로
    return "".join(out)


def asset_scalars(path):
    """
    자산(YAML)의 **맨 바깥 칸들**을 차례대로 [(이름, 값)] 으로.

    ⚠️ **중첩은 건너뛴다** — 리스트·구조체는 여기서 안 편다(펴면 표가 표를 품게 된다).
       필요한 자리는 제 함수가 따로 읽는다(`node_recipes` 처럼).
    ⚠️ 유니티 살림 칸(`m_*`)과 자산 참조(`{fileID: ...}`)는 값이 아니라 **배선**이라 뺀다.
    """
    out = []
    for line in read_text(path).split("\n"):
        m = re.match(r"^  ([A-Za-z_][A-Za-z0-9_]*):\s?(.*)$", line)
        if not m:
            continue
        key, raw = m.group(1), m.group(2).strip()
        if key.startswith("m_"):
            continue
        if raw == "" or raw.startswith("{fileID") or raw.startswith("-"):
            continue          # 중첩 블록 · 참조 — 값이 아니다
        out.append((key, unity_text(raw)))
    return out


def tooltips(cs_path):
    """
    C# 의 `[Tooltip("...")]` → 바로 아래 필드 이름에 붙인다.

    📌 **설명을 옮겨 적지 않는다**(지침 §7) — 코드에 이미 있는 문장을 표가 빌려 쓴다.
       없으면 빈 칸이고, 그것은 「설명이 아직 없다」는 사실이다.

    ⚠️ **정규식을 안 쓴다.** 문자열 리터럴 안의 역슬래시를 정규식으로 가르려면 패턴에
       역슬래시가 겹겹이 들어가는데, 그 패턴이 편집을 한 번 지날 때마다 한 겹씩
       사라진다(2026-09-19 에 두 번 겪었다). **글자를 직접 훑는 쪽이 짧고 안 깨진다.**
    """
    text = read_text(cs_path)
    out = {}
    at = 0
    while True:
        at = text.find("[Tooltip(", at)
        if at < 0:
            break
        i = at + len("[Tooltip(")

        # ① 괄호가 닫힐 때까지의 문자열 조각들을 모은다(`"..." + "..."` 이어 붙이기).
        parts = []
        while i < len(text):
            c = text[i]
            if c == '"':
                i += 1
                buf = []
                while i < len(text) and text[i] != '"':
                    if text[i] == chr(92):        # 이스케이프 — 다음 글자를 그대로 받는다
                        i += 1
                        if i < len(text):
                            buf.append(text[i])
                    else:
                        buf.append(text[i])
                    i += 1
                parts.append("".join(buf))
            elif c == ")":
                break
            i += 1

        # ② 그 뒤 첫 `public <형> <이름>` 이 이 툴팁의 주인이다.
        m = re.search(r"public\s+[\w<>\[\],?. ]+?\s+([A-Za-z_][A-Za-z0-9_]*)\s*[=;]",
                      text[i:i + 600])
        if m:
            out[m.group(1)] = "".join(parts)
        at = i + 1
    return out


def header_of(cs_path, field):
    """그 필드가 속한 `[Header("...")]` 묶음 이름 — 표에서 무리를 가르는 데 쓴다."""
    text = read_text(cs_path)
    head = ""
    for m in re.finditer(r"\[Header\(\"([^\"]*)\"\)\]|public\s+\S+\s+([A-Za-z_][A-Za-z0-9_]*)", text):
        if m.group(1) is not None:
            head = m.group(1)
        elif m.group(2) == field:
            return head
    return ""


def field_table(name, asset_rel, cs_rel, title, note):
    """
    **자산 한 장 = 표 한 장**(칸마다 한 줄). 조율·경제·물류처럼 **평면 설정**인 자산에 쓴다.

    📌 왜 칸을 열로 안 펴는가 — 칸이 스무 개가 넘고 계속 는다. 열로 펴면 표가
       가로로 길어져 사람이 못 읽고, 칸이 늘 때마다 **열을 새로 만들어야** 한다.
       줄로 두면 칸이 늘어도 줄이 하나 늘 뿐이다.

    ⚠️ 설명은 **C# 툴팁을 빌려 온다** — 옮겨 적으면 두 곳에 살게 된다(지침 §7).
    """
    asset = os.path.join(SO, asset_rel)
    cs = os.path.join(SRC, cs_rel)
    tips = tooltips(cs)

    fields = (
        ["Dev_Index", "Dev_Group", "ID", "FieldName", "Value", "Confirmed"],
        ["개발용 번호", "개발용 묶음", "고유 번호", "칸 이름", "값", "확정 여부"],
    )
    rows, info = [], [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Group", "개발용 묶음", "string", "회피", "컨버팅 제외(Dev_ 접두)",
         "C# 의 [Header] 묶음 이름 — 칸이 많아 무리로 갈라 본다"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["FieldName", "칸 이름", "string", "dodgeIFramesTbd", "자산의 칸 이름 그대로",
         "⚠️ **이름을 고치면 배선이 끊긴다** — 자산 칸 이름과 한 글자도 다르면 안 된다"],
        ["Value", "값", "float", 0.4, "지금 자산에 든 값", note],
        ["Confirmed", "확정 여부", "bool", 0, "1=확정 0=가정",
         "⚠️ **전부 0 으로 뜬다** — 자산에는 칸별 확정 플래그가 없다. 확정된 칸은 "
         "C# 툴팁이 「✅ 사용자 확정」으로 적고 있으므로 **비고를 보고 사람이 채운다**(설계 몫)"],
    ]

    for i, (key, val) in enumerate(asset_scalars(asset), start=1):
        rows.append([i, header_of(cs, key), i, key, val, 0])
        info.append(["", "", "", "", "", ""])   # 자리만 — 칸 설명은 아래 Dev 시트가 든다
    info = info[:6]

    # 칸마다의 설명은 **툴팁**이 든다 — Table_Info 끝에 붙인다.
    for key, _ in asset_scalars(asset):
        info.append([key, tips.get(key, ""), "", "", "C# [Tooltip] 에서 그대로", cs_rel])

    build(os.path.join(HERE, name + ".xlsx"), name, fields, rows, info)
    print("     %s — %s" % (title, asset_rel))
    return rows


# ────────────────────────────── 자산 한 장짜리 표 셋 ──────────────────────────────

def combat_rule():
    return field_table(
        "COMBAT_RULE_DATA", "CombatTuning.asset",
        os.path.join("Data", "CombatTuning.cs"),
        "전투 규칙·연출 시간",
        "CombatTuning.asset 의 값 그대로 — 자산이 원천이다(C# 기본값이 다르면 자산이 이긴다 · "
        "07-30 부터 그랬고 그 사실을 09-18 에 확인했다)")


def economy():
    return field_table(
        "ECONOMY_DATA", "EconomyConfig.asset",
        os.path.join("Data", "EconomyConfig.cs"),
        "경제(고철·방치)",
        "EconomyConfig.asset 의 값 그대로 · json economy 무리의 근거 문장은 BALANCE_PARAM_DATA 가 든다")


def logistics():
    return field_table(
        "LOGISTICS_DATA", "LogisticsConfig.asset",
        os.path.join("Data", "LogisticsConfig.cs"),
        "물류 총량(전력·발열·벨트)",
        "LogisticsConfig.asset 의 값 그대로 — 판 전체의 총량이고, 노드 한 대분은 NODE_DATA 가 든다")


# ────────────────────────────── 노드 · 조합 · 모듈 · 로봇 ──────────────────────────────

def node():
    """
    노드 한 대의 **값**(전력·탄약·발열). 조합표는 RECIPE_DATA 가 든다.

    ⚠️ **면(ports)은 안 옮긴다** — 그것은 값이 아니라 **구조**다(어느 면이 입력인가).
       표로 옮기려면 면마다 한 줄인 표가 하나 더 필요하고, 그 표를 지금 만들라는 지시는 없다.
       대신 `Dev_Ports` 에 사람이 읽을 꼴로 적어 둔다 — **컨버팅 제외**라 게임에 안 샌다.
    """
    fields = (
        ["Dev_Index", "Dev_Ports", "ID", "NodeID", "TID_Name", "Type", "Implemented",
         "PowerDraw", "PowerSupply", "AmmoProduce", "AmmoConsume", "HeatGenerate", "Confirmed"],
        ["개발용 번호", "개발용 면", "고유 번호", "노드 키", "표시 이름", "종류", "구현 여부",
         "전력 소비", "전력 공급", "탄약 생산", "탄약 소비", "발열", "확정 여부"],
    )

    face = {"0": "북", "1": "동", "2": "남", "3": "서"}
    rows = []
    names = sorted(f for f in os.listdir(os.path.join(SO, "Nodes")) if f.endswith(".asset"))
    for i, fn in enumerate(names, start=1):
        path = os.path.join(SO, "Nodes", fn)
        text = read_text(path)
        flat = dict(asset_scalars(path))

        ports = []
        for m in re.finditer(r"- face: (\d+)\s*\n\s*io: (\d+)\s*\n\s*kind: (\d+)", text):
            ports.append("%s%s" % (face.get(m.group(1), m.group(1)),
                                   "입" if m.group(2) == "0" else "출"))

        for k in ["nodeId", "displayName", "type", "implemented"]:
            if k not in flat:
                die("%s 에 '%s' 가 없다" % (fn, k))

        res = re.search(r"  resources:\s*\n((?:    \w+: [-\d.]+\s*\n)+)", text)
        if not res:
            die("%s 에 resources 블록이 없다" % fn)
        r = dict(re.findall(r"(\w+): ([-\d.]+)", res.group(1)))

        rows.append([i, " ".join(ports) or "없음", i, flat["nodeId"], flat["displayName"],
                     int(flat["type"]), int(flat["implemented"]),
                     num(r["powerDraw"]), num(r["powerSupply"]), num(r["ammoProduce"]),
                     num(r["ammoConsume"]), num(r["heatGenerate"]), int(r.get("confirm", 0))])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Ports", "개발용 면", "string", "서입 동출", "컨버팅 제외(Dev_ 접두)",
         "면은 표로 안 옮겼다 — 값이 아니라 구조다. 원천은 자산의 ports 블록이고 "
         "여기 적힌 것은 읽기용 요약이다(북동남서 + 입/출)"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["NodeID", "노드 키", "string", "muni", "자산 파일 이름과 같다", "Node_<키>.asset"],
        ["TID_Name", "표시 이름", "string", "기초 가공소", "화면에 보이는 한글 이름",
         "NodeDefinition.displayName · TEXT_DATA 로 옮길 자리(영상 이후)"],
        ["Type", "종류", "enum", 3, "코어0 변환기1 기초2 에너지3 저장4 보호막5 부스터6 복합7",
         "Designer_Data 참조 · NodeType 열거형"],
        ["Implemented", "구현 여부", "bool", 1, "1=돈다 0=자리만", "NodeDefinition.implemented"],
        ["PowerDraw", "전력 소비", "float", 2, "대당", "resources.powerDraw"],
        ["PowerSupply", "전력 공급", "float", 0, "대당(에너지 노드만)", "resources.powerSupply"],
        ["AmmoProduce", "탄약 생산", "float", 1, "대당 초당", "resources.ammoProduce"],
        ["AmmoConsume", "탄약 소비", "float", 0, "대당 초당", "resources.ammoConsume"],
        ["HeatGenerate", "발열", "float", 0, "대당", "resources.heatGenerate"],
        ["Confirmed", "확정 여부", "bool", 0, "1=확정 0=가정", "resources.confirm 그대로"],
    ]
    build(os.path.join(HERE, "NODE_DATA.xlsx"), "NODE_DATA", fields, rows, info)
    return rows


def recipe():
    """
    조합표 — **노드 한 대가 돌릴 수 있는 갈래마다 한 줄**.

    ⚠️ 입력이 **둘까지** 있다(복합 가공소). 셋째가 생기면 열을 늘려야 한다 —
       그때 조용히 잘리지 않도록, 셋 이상이면 **멈춘다.**
    """
    fields = (
        ["Dev_Index", "Dev_Desc", "ID", "NodeID", "Kind", "TID_Name",
         "Input1Kind", "Input1PerOutput", "Input2Kind", "Input2PerOutput",
         "Output", "OutputPerSec", "RequiredProduction", "StackLimit", "Implemented"],
        ["개발용 번호", "개발용 설명", "고유 번호", "노드 키", "조합 갈래", "표시 이름",
         "입력1 품목", "입력1 개당", "입력2 품목", "입력2 개당",
         "산출 품목", "초당 산출", "필요 생산치", "쌓임 상한", "구현 여부"],
    )

    rows, i = [], 0
    for fn in sorted(f for f in os.listdir(os.path.join(SO, "Nodes")) if f.endswith(".asset")):
        text = read_text(os.path.join(SO, "Nodes", fn))
        m_id = re.search(r"^  nodeId: (\S+)", text, re.M)
        if not m_id:
            continue
        node_id = m_id.group(1)

        block = re.search(r"^  recipes:\s*\n(.*?)(?=^  \w+:)", text, re.S | re.M)
        if not block:
            continue

        for chunk in re.split(r"\n  - kind:", "\n" + block.group(1))[1:]:
            chunk = "  - kind:" + chunk
            i += 1

            def g(k, d=None, _c=chunk):
                m = re.search(r"^\s*%s: (.*)$" % k, _c, re.M)
                return m.group(1).strip() if m else d

            ins = re.findall(r"- kind: (\d+)\s*\n\s*perOutput: ([-\d.]+)", chunk)
            if len(ins) > 2:
                die("%s 의 조합에 입력이 셋 이상이다 — 열을 늘려야 한다" % fn)
            i1 = ins[0] if len(ins) > 0 else ("", "")
            i2 = ins[1] if len(ins) > 1 else ("", "")

            name = unity_text(g("displayName", '""'))
            kind = int(chunk.split("kind:")[1].split("\n")[0].strip())
            rows.append([i, "%s / %s" % (node_id, name), i, node_id, kind, name,
                         int(i1[0]) if i1[0] else "", num(i1[1]) if i1[1] else "",
                         int(i2[0]) if i2[0] else "", num(i2[1]) if i2[1] else "",
                         int(g("output", "0")), num(g("outputPerSec", "0")),
                         num(g("requiredProduction", "0")), num(g("stackLimitTbd", "0")),
                         int(g("implemented", "0"))])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Desc", "개발용 설명", "string", "muni / 표준탄", "컨버팅 제외(Dev_ 접두)", "행을 한눈에"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["NodeID", "노드 키", "string", "muni", "이 조합을 돌리는 노드", "NODE_DATA 참조"],
        ["Kind", "조합 갈래", "int", 9, "RecipeKind 열거형", "사전이 아직 표에 없다 — 코드가 든다"],
        ["TID_Name", "표시 이름", "string", "표준탄", "화면에 보이는 한글 이름",
         "TEXT_DATA 로 옮길 자리(영상 이후)"],
        ["Input1Kind", "입력1 품목", "int", 8, "ItemKind 열거형", "빈 칸 = 입력 없음"],
        ["Input1PerOutput", "입력1 개당", "float", 1, "산출 하나에 드는 수", "recipes[].inputs[].perOutput"],
        ["Input2Kind", "입력2 품목", "int", "", "ItemKind 열거형", "복합 가공소만 쓴다"],
        ["Input2PerOutput", "입력2 개당", "float", "", "산출 하나에 드는 수", "복합 가공소만 쓴다"],
        ["Output", "산출 품목", "int", 11, "ItemKind 열거형", "recipes[].output"],
        ["OutputPerSec", "초당 산출", "float", 1, "대당", "recipes[].outputPerSec"],
        ["RequiredProduction", "필요 생산치", "float", 0, "0 = 조건 없음", "recipes[].requiredProduction"],
        ["StackLimit", "쌓임 상한", "float", 0, "0 = 상한 없음", "recipes[].stackLimitTbd · 가정"],
        ["Implemented", "구현 여부", "bool", 1, "1=돈다 0=자리만", "recipes[].implemented"],
    ]
    build(os.path.join(HERE, "RECIPE_DATA.xlsx"), "RECIPE_DATA", fields, rows, info)
    return rows


def module():
    fields = (
        ["Dev_Index", "Dev_Desc", "ID", "ModuleID", "TID_Name", "Kind", "Symbol",
         "OutputMultiplier", "InputMultiplier", "PowerLoadMultiplier", "Confirmed"],
        ["개발용 번호", "개발용 설명", "고유 번호", "모듈 키", "표시 이름", "종류", "기호",
         "생산 배수", "입력 배수", "전력 부하 배수", "확정 여부"],
    )
    rows = []
    for i, fn in enumerate(sorted(f for f in os.listdir(os.path.join(SO, "Modules"))
                                  if f.endswith(".asset")), start=1):
        f = dict(asset_scalars(os.path.join(SO, "Modules", fn)))
        for k in ["moduleId", "displayName", "kind", "symbol",
                  "outputMultiplier", "inputMultiplier", "powerLoadMultiplier"]:
            if k not in f:
                die("%s 에 '%s' 가 없다" % (fn, k))
        rows.append([i, "%s(%s)" % (f["displayName"], f["symbol"]), i,
                     f["moduleId"], f["displayName"], int(f["kind"]), f["symbol"],
                     num(f["outputMultiplier"]), num(f["inputMultiplier"]),
                     num(f["powerLoadMultiplier"]), 0])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Desc", "개발용 설명", "string", "생산량(M)", "컨버팅 제외(Dev_ 접두)", "행을 한눈에"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["ModuleID", "모듈 키", "string", "mod_output", "자산의 moduleId", "Module_*.asset"],
        ["TID_Name", "표시 이름", "string", "생산량", "화면에 보이는 한글 이름",
         "TEXT_DATA 로 옮길 자리(영상 이후)"],
        ["Kind", "종류", "enum", 0, "생산량0 생산속도1", "Designer_Data 참조"],
        ["Symbol", "기호", "string", "M", "보드 타일에 찍히는 한 글자", "ModuleDefinition.symbol"],
        ["OutputMultiplier", "생산 배수", "float", 1.5, "산출에 곱한다", "ModuleDefinition.outputMultiplier"],
        ["InputMultiplier", "입력 배수", "float", 1, "입력에 곱한다", "ModuleDefinition.inputMultiplier"],
        ["PowerLoadMultiplier", "전력 부하 배수", "float", 2, "전력 소비에 곱한다",
         "ModuleDefinition.powerLoadMultiplier"],
        ["Confirmed", "확정 여부", "bool", 0, "1=확정 0=가정",
         "전부 0 — 자산에 칸별 확정 플래그가 없다(설계가 채울 자리)"],
    ]
    build(os.path.join(HERE, "MODULE_DATA.xlsx"), "MODULE_DATA", fields, rows, info)
    return rows


def robot():
    """
    로봇 한 대의 **값**. 무기 줄은 이미 `WEAPON_DATA` 가 들고 있으므로 여기서 다시 안 적는다
    (같은 수가 두 곳에 살면 답이 둘이 된다 · 지침 §7).
    """
    fields = (
        ["Dev_Index", "ID", "RobotID", "TID_Name", "ConsumptionCap", "MountCoef",
         "EnhancedMountCoef", "ModuleMult", "WeaponCount", "Confirmed"],
        ["개발용 번호", "고유 번호", "로봇 키", "표시 이름", "소비 상한", "마운트 계수",
         "강화 마운트 계수", "모듈 배수", "무기 줄 수", "확정 여부"],
    )
    rows = []
    for i, fn in enumerate(sorted(f for f in os.listdir(os.path.join(SO, "Robots"))
                                  if f.endswith(".asset")), start=1):
        path = os.path.join(SO, "Robots", fn)
        f = dict(asset_scalars(path))
        n = len(re.findall(r"- kind: \d+\s*\n\s*damagePerShot:", read_text(path)))
        for k in ["robotId", "displayName", "consumptionCap", "mountCoef",
                  "enhancedMountCoef", "moduleMult"]:
            if k not in f:
                die("%s 에 '%s' 가 없다" % (fn, k))
        rows.append([i, i, f["robotId"], f["displayName"], num(f["consumptionCap"]),
                     num(f["mountCoef"]), num(f["enhancedMountCoef"]),
                     num(f["moduleMult"]), n, 0])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["RobotID", "로봇 키", "string", "robotA", "자산의 robotId", "Robot_*.asset"],
        ["TID_Name", "표시 이름", "string", "로봇A", "화면에 보이는 한글 이름",
         "TEXT_DATA 로 옮길 자리(영상 이후)"],
        ["ConsumptionCap", "소비 상한", "float", 12, "초당 쓸 수 있는 탄약 상한",
         "RobotDefinition.consumptionCap"],
        ["MountCoef", "마운트 계수", "float", 1, "적재가 전투력에 주는 곱", "RobotDefinition.mountCoef"],
        ["EnhancedMountCoef", "강화 마운트 계수", "float", 1.45, "S4 강화 뒤",
         "RobotDefinition.enhancedMountCoef · json params enh (확정)"],
        ["ModuleMult", "모듈 배수", "float", 1, "모듈이 얹는 곱", "RobotDefinition.moduleMult"],
        ["WeaponCount", "무기 줄 수", "int", 3, "이 로봇이 가진 무기 줄의 수",
         "값이 아니라 셈이다 — 무기 줄 자체는 WEAPON_DATA 가 든다. "
         "두 표가 어긋나면 여기서 먼저 보인다"],
        ["Confirmed", "확정 여부", "bool", 0, "1=확정 0=가정",
         "전부 0 — 자산에 칸별 확정 플래그가 없다. EnhancedMountCoef 만 json 에서 확정이다"],
    ]
    build(os.path.join(HERE, "ROBOT_DATA.xlsx"), "ROBOT_DATA", fields, rows, info)
    return rows


def stage(bal):
    """스테이지 한 판의 값 — **구성(몇 마리)은 STAGE_COMP_DATA 가 든다.**"""
    fields = (
        ["Dev_Index", "Dev_Basis", "ID", "StageID", "TID_Topic", "ReqType", "Req",
         "ReqConfirmed", "ChallengeTime", "PowerModel", "EnhMaterialReward",
         "SpawnCap", "SpawnInterval", "SpawnConfirmed", "TID_MonsterDir"],
        ["개발용 번호", "개발용 근거", "고유 번호", "스테이지", "주제", "요구 종류", "요구치",
         "요구치 확정", "도전 제한 시간", "전투력 모델", "강화 재료 보상",
         "스폰 정원", "스폰 간격", "스폰 확정", "몬스터 방향"],
    )
    # ⚠️ **사전을 지어내지 않았다** — json 의 여섯 판에 실제로 든 값만 담았다
    #    (`fixed`·`formula`·`budget` / `logistics`·`enhanced`·`tag`·`burst`).
    #    새 값이 오면 **조용히 0 으로 안 떨어지고 멈춘다** — 그것이 이 사전의 일이다.
    REQ_TYPE = {"fixed": 1, "formula": 2, "budget": 3}
    POWER = {"logistics": 1, "enhanced": 2, "tag": 3, "burst": 4}

    rows = []
    for i, st in enumerate(bal.get("stages", []), start=1):
        rt = st.get("reqType")
        if rt not in REQ_TYPE:
            die("모르는 reqType — %s" % rt)
        pm = st.get("powerModel")
        if pm not in POWER:
            die("모르는 powerModel — %s" % pm)
        rows.append([i, (st.get("reqBasis") or "")[:120], i, st["id"], st.get("topic", ""),
                     REQ_TYPE[rt], num(st.get("req", 0)),
                     1 if st.get("reqConfirmed") else 0,
                     num(st.get("challengeTime", 0)), POWER[pm],
                     num(st.get("enhMaterialReward", 0)),
                     num(st.get("spawnCap", 0)), num(st.get("spawnInterval", 0)),
                     1 if st.get("spawnConfirmed") else 0, st.get("monsterDir", "")])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["Dev_Basis", "개발용 근거", "string", "2026-09-15 사용자 확정 …", "컨버팅 제외(Dev_ 접두)",
         "json reqBasis 의 앞 120 자만 옮겼다 — 근거 전문은 balance_v4.json 이 든다. "
         "여기 것은 사람이 표에서 알아보라고 붙인 꼬리표이지 원천이 아니다"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["StageID", "스테이지", "string", "S1", "S1~S6", "json stages[].id"],
        ["TID_Topic", "주제", "string", "벨트 연결(온보딩)", "그 판이 가르치는 것",
         "json stages[].topic · TEXT_DATA 로 옮길 자리(영상 이후)"],
        ["ReqType", "요구 종류", "enum", 1, "고정1 수식2 예산3", "json reqType · Designer_Data 참조"],
        ["Req", "요구치", "float", 18, "넘겨야 하는 출력", "json req"],
        ["ReqConfirmed", "요구치 확정", "bool", 1, "1=확정 0=가정", "json reqConfirmed"],
        ["ChallengeTime", "도전 제한 시간", "float", 120, "초", "json challengeTime"],
        ["PowerModel", "전투력 모델", "enum", 1, "물류1 강화2 태그3 버스트4",
         "json powerModel · Designer_Data 참조 — 판마다 「무엇으로 이기는가」가 다르다"],
        ["EnhMaterialReward", "강화 재료 보상", "int", 30, "클리어 때 준다", "json enhMaterialReward"],
        ["SpawnCap", "스폰 정원", "int", 0, "0 = 파밍 층이 아니다", "json spawnCap"],
        ["SpawnInterval", "스폰 간격", "float", 0, "초 · 0 = 파밍 층이 아니다", "json spawnInterval"],
        ["SpawnConfirmed", "스폰 확정", "bool", 0, "1=확정 0=가정", "json spawnConfirmed"],
        ["TID_MonsterDir", "몬스터 방향", "string", "체력 낮은 다수·느린 등장·저방어",
         "그 판의 적 성격(설계 메모)", "json monsterDir · 화면에 안 뜬다"],
    ]
    build(os.path.join(HERE, "STAGE_DATA.xlsx"), "STAGE_DATA", fields, rows, info)
    return rows


def balance_param(bal):
    """
    json `params` 마흔일곱 칸을 **그대로** — 값·확정·근거·의도·문서 참조까지.

    📌 **이 표가 json 의 자리를 이어받는다.** 다른 표들이 값을 들고 가고 나면
       json 에 남는 것은 이 목록이므로, 여기가 가장 먼저 원천이 된다.
    ⚠️ 근거 문장은 **줄이지 않는다** — 줄이면 「왜 이 값인가」가 사라진다.
    """
    fields = (
        ["Dev_Index", "ID", "ParamKey", "TID_Label", "GroupName", "Value", "Confirmed",
         "Basis", "Intent", "DocRef"],
        ["개발용 번호", "고유 번호", "칸 키", "이름", "무리", "값", "확정 여부",
         "근거", "의도", "문서 참조"],
    )
    rows = []
    for i, p in enumerate(bal.get("params", []), start=1):
        if "key" not in p or "value" not in p:
            die("params 에 key/value 가 없는 칸이 있다 — %s" % p)
        rows.append([i, i, p["key"], p.get("label", ""), p.get("group", ""),
                     num(p["value"]), 1 if p.get("confirmed") else 0,
                     p.get("basis", ""), p.get("intent", ""), p.get("docRef", "")])

    info = [
        ["Dev_Index", "개발용 번호", "int", 1, "컨버팅 제외(Dev_ 접두)", "사람이 표를 읽을 때만 쓴다"],
        ["ID", "고유 번호", "int", 1, "1부터", "행 식별"],
        ["ParamKey", "칸 키", "string", "origin", "json params[].key 그대로",
         "이름을 고치면 근거를 잃는다 — 문서·주석이 이 키로 값을 가리킨다"],
        ["TID_Label", "이름", "string", "원점 출력", "사람이 부르는 이름", "json params[].label"],
        ["GroupName", "무리", "string", "원점·곡선", "일곱 무리", "json params[].group"],
        ["Value", "값", "float", 100, "지금 값", "json params[].value"],
        ["Confirmed", "확정 여부", "bool", 1, "1=확정 0=가정",
         "json params[].confirmed 그대로 — 이 표에서 유일하게 칸마다 진짜 확정 플래그가 있다"],
        ["Basis", "근거", "string", "온보딩 공장 출력(불변) …", "왜 이 값인가",
         "json params[].basis · 줄이지 않았다"],
        ["Intent", "의도", "string", "요구치 분모=100", "이 값으로 무엇을 하려는가",
         "json params[].intent"],
        ["DocRef", "문서 참조", "string", "밸런스 2장", "어느 문서 몇 장인가", "json params[].docRef"],
    ]
    build(os.path.join(HERE, "BALANCE_PARAM_DATA.xlsx"), "BALANCE_PARAM_DATA", fields, rows, info)
    return rows



# ────────────────────────────── 화면 글자 ──────────────────────────────
#
# ⚠️⚠️ **이 표는 1차 수집이다.** 사람이 한 번 훑어야 한다.
#
#    코드에서 한글 문자열을 긁는 일에는 **완전한 잣대가 없다.** 화면에 뜨는 글자와
#    개발자만 보는 글자(로그·예외)는 **같은 문자열 리터럴**이라, 어디서 쓰이는지를
#    보고 갈라야 한다. 여기서는 **주석을 걷고 · 로그를 빼고 · 남은 것을 전부** 싣는다.
#    그러면 **덜 싣는 쪽이 아니라 더 싣는 쪽**으로 틀린다 — 빠뜨린 글자는 나중에
#    화면에서 한글이 사라져야 드러나지만, 더 실린 글자는 표에서 지우면 그만이다.

def strip_comments(text):
    """
    주석을 걷는다 — **문자열 안의 `//` 는 주석이 아니다.**

    📌 이 리포의 주석에는 한글이 잔뜩 있다(대부분이 한글이다). 안 걷으면
       TEXT_DATA 가 **설계 메모로 가득 찬다.**
    ⚠️ 정규식을 안 쓴다 — 문자열·주석이 서로를 품는 경우를 정규식으로는 못 가른다.
    """
    out = []
    i, n = 0, len(text)
    BS = chr(92)
    while i < n:
        c = text[i]
        if c == chr(34):                       # 문자열 — 통째로 살린다
            j = i + 1
            while j < n:
                if text[j] == BS:
                    j += 2
                    continue
                if text[j] == chr(34):
                    break
                if text[j] == chr(10):         # 줄이 끝나면 닫힌 것으로 본다(축자 문자열 아님)
                    break
                j += 1
            out.append(text[i:j + 1])
            i = j + 1
        elif c == chr(39):                     # 문자 리터럴
            j = i + 1
            while j < n and text[j] != chr(39):
                j += 2 if text[j] == BS else 1
            out.append(text[i:j + 1])
            i = j + 1
        elif c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != chr(10):
                i += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            i = text.find("*/", i)
            i = n if i < 0 else i + 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def has_hangul(s):
    return any("\uAC00" <= ch <= "\uD7A3" for ch in s)


# 개발자만 보는 글자 — 화면 글자가 아니다. 이 이름으로 부르는 자리의 인자는 뺀다.
DEV_ONLY = (
    # 로그·예외 — 개발자만 본다
    "Debug.Log", "Debug.LogWarning", "Debug.LogError", "Debug.LogFormat",
    "Assert.", "StringAssert.", "throw new", "die(", "SystemExit",
    # 애트리뷰트 — **유니티 인스펙터**에 뜨는 글자다. 게임 화면이 아니다
    # (2026-09-19 1차 수집에서 604 줄 중 앞머리가 통째로 이것이었다).
    "[Tooltip(", "[Header(", "[CreateAssetMenu", "[MenuItem(", "[Range(",
    "[UnityTest", "[TestCase",
)


def screen_strings(path):
    """
    파일 하나에서 **화면에 뜰 만한** 한글 문자열을 [(줄번호, 글자, 구멍여부)] 로.

    ⚠️ 보간 문자열(`$"... {x} ..."`)은 **구멍이 있다** — 표로 옮기려면 「구멍을 어떻게
       적을 것인가」라는 규약이 있어야 하는데 **그 규약이 아직 없다.** 그래서 싣되
       `HasHole` 로 표시하고, 구멍 부분은 **원문 그대로** 둔다(지어내지 않는다).
    """
    raw = read_text(path)
    text = strip_comments(raw)

    # 줄 번호를 되찾으려면 걷기 전 원문에서 찾아야 한다 — 걷은 쪽은 줄이 어긋난다.
    out = []
    BS = chr(92)
    Q = chr(34)
    i, n = 0, len(text)
    while i < n:
        if text[i] != Q:
            i += 1
            continue
        j = i + 1
        buf = []
        while j < n:
            if text[j] == BS:
                buf.append(text[j:j + 2])
                j += 2
                continue
            if text[j] == Q or text[j] == chr(10):
                break
            buf.append(text[j])
            j += 1
        lit = "".join(buf)
        i = j + 1

        if not has_hangul(lit):
            continue

        # 앞 120 자를 보고 개발자용 호출인지 가른다.
        before = text[max(0, i - len(lit) - 160):i - len(lit)]
        if any(k in before for k in DEV_ONLY):
            continue

        line = raw.count(chr(10), 0, max(raw.find(lit), 0)) + 1 if lit in raw else 0
        out.append((line, lit, 1 if ("{" in lit and "}" in lit) else 0))
    return out


def text_data():
    """
    화면 글자 — **지금 코드에 있는 그대로**(2026-09-19 사용자 확정 · 문안 개정 없음).

    ⚠️⚠️ **BBCode 는 안 넣었다.** 사용자 확정은 「BBCode 허용」이고 **지금 글자에는
       하나도 없다** — 없는 것을 넣는 것은 개정이다. 칸만 열어 둔다.
    ⚠️ 배선(TextId.cs 생성 · 리터럴 치환)은 **영상 이후**다. 이 표는 아직 안 읽힌다.
    """
    fields = (
        ["Dev_Index", "Dev_File", "Dev_Line", "Dev_HasHole", "Dev_Fragment", "ID", "Text_KR"],
        ["개발용 기호 키", "개발용 파일", "개발용 줄", "개발용 구멍 여부", "개발용 조각 여부",
         "고유 번호", "한글 문안"],
    )

    skip_dirs = (os.sep + "Tests" + os.sep, os.sep + "Editor" + os.sep)
    rows, seen, i = [], {}, 0

    for base, _dirs, files in os.walk(SRC):
        if any(d in base + os.sep for d in skip_dirs):
            continue
        for fn in sorted(files):
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(base, fn)
            cls = os.path.splitext(fn)[0]
            for k, (line, lit, hole) in enumerate(screen_strings(path), start=1):
                # ⚠️ **같은 글자는 한 줄만** — 「닫기」가 네 곳에 있다고 네 줄이면
                #    번역·수정이 네 번 일어나고 그중 하나가 빠지는 날이 온다.
                if lit in seen:
                    continue
                i += 1
                seen[lit] = i
                # 조각 판정은 **뜻이 아니라 꼴**로 한다 — 앞뒤에 공백이 붙어 있으면
                # 그 글자는 혼자 서는 문장이 아니라 **이어 붙이는 토막**이다.
                frag = 1 if (lit != lit.strip()) else 0
                rows.append([i, "TID_%s_%03d" % (cls, k), fn, line, hole, frag, i, lit])

    # Dev_Index 가 맨 앞이어야 하므로 자리를 맞춘다(기호 키가 Dev_Index 다).
    rows = [[r[1], r[2], r[3], r[4], r[5], r[6], r[7]] for r in rows]

    info = [
        ["Dev_Index", "개발용 기호 키", "string", "TID_StageRunner_001", "컨버팅 제외(Dev_ 접두)",
         "사용자 확정 — Dev_Index 에 기호 키를 둔다. 배선되면 Tables/gen_text_ids.py 가 "
         "이것으로 TextId.cs(const int)를 굽는다(영상 이후)"],
        ["Dev_File", "개발용 파일", "string", "StageRunner.cs", "컨버팅 제외(Dev_ 접두)",
         "어디서 긁었는가 — 사람이 훑을 때 필요하다"],
        ["Dev_Line", "개발용 줄", "int", 2331, "컨버팅 제외(Dev_ 접두)",
         "첫 등장 줄(같은 글자가 여럿이면 처음 만난 자리)"],
        ["Dev_HasHole", "개발용 구멍 여부", "bool", 0, "컨버팅 제외(Dev_ 접두)",
         "1 = 보간 문자열이라 안에 {…} 가 있다. 표로 옮기려면 「구멍을 어떻게 적을 것인가」 "
         "규약이 있어야 하는데 **아직 없다** — 원문 그대로 실었다"],
        ["Dev_Fragment", "개발용 조각 여부", "bool", 0, "컨버팅 제외(Dev_ 접두)",
         "1 = 앞이나 뒤에 공백이 붙은 **토막**이다(「목표: 」 + 「 넘기기」처럼 코드가 "
         "이어 붙인다). ⚠️⚠️ **토막은 그대로 옮기면 안 된다** — 문장을 어떻게 다시 "
         "세울지가 정해져야 옮길 수 있고, 그 규약이 아직 없다. 판정은 뜻이 아니라 "
         "**꼴**(앞뒤 공백)로 했다 — 지어낸 판단이 아니다"],
        ["ID", "고유 번호", "int", 1, "1부터 · 정수", "사용자 확정 — ID 는 정수"],
        ["Text_KR", "한글 문안", "string", "메인 메뉴로", "화면에 그대로 뜨는 글자",
         "⚠️ **지금 코드 그대로다 — 개정 없음**(사용자 확정). BBCode 는 지금 글자에 "
         "하나도 없어 안 넣었다(없는 것을 넣는 것은 개정이다)"],
    ]
    build(os.path.join(HERE, "TEXT_DATA.xlsx"), "TEXT_DATA", fields, rows, info)
    holes = sum(1 for r in rows if r[3])
    frags = sum(1 for r in rows if r[4])
    print("     화면 글자 %d 줄 (구멍 %d · 토막 %d) — ⚠️ **1차 수집이다. 사람이 훑어야 한다**"
          % (len(rows), holes, frags))
    return rows


# 둘째 묶음 등록 — `main` 이 이름으로 찾는다.
SECOND_BATCH = {
    "NODE_DATA": node,
    "RECIPE_DATA": recipe,
    "MODULE_DATA": module,
    "ROBOT_DATA": robot,
    "STAGE_DATA": lambda: stage(balance()),
    "COMBAT_RULE_DATA": combat_rule,
    "ECONOMY_DATA": economy,
    "LOGISTICS_DATA": logistics,
    "BALANCE_PARAM_DATA": lambda: balance_param(balance()),
    "TEXT_DATA": text_data,
}


if __name__ == "__main__":
    main()
