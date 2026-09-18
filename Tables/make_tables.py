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

ENUMS = {
    "EnemyKey": [("보병", 1), ("포격", 2), ("장갑", 3), ("강적", 4)],
    "Role":     [("근접", 1), ("원거리", 2), ("길막", 3), ("보스", 4)],
    "RobotID":  [("로봇A", 1), ("로봇B", 2)],
    "AmmoKind": [("관통", 1), ("표준", 2), ("폭발", 3), ("누적형 드론", 4), ("광역형 드론", 5)],
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
    """필드별 라벨↔수치 **세로**. 표에 없는 enum 은 안 적는다."""
    rows = [["Field", "Label", "Value"], ["필드", "한글 라벨", "수치"]]
    for f in fields[0]:
        if f not in ENUMS:
            continue
        for lab, v in ENUMS[f]:
            rows.append([f, lab, v])
    return rows


def info_rows(info):
    head = ["INDEX", "FieldName", "Summary", "DataType", "Example", "Description", "Note"]
    kor = ["번호", "필드명", "간략 설명", "데이터 타입", "데이터 예시", "필드 설명/값", "비고(근거)"]
    rows = [head, kor]
    for i, r in enumerate(info, start=1):
        rows.append([i] + list(r))
    return rows


def build(path, name, fields, rows, info):
    write_xlsx(path, [
        (name, [list(fields[0]), list(fields[1])] + [list(r) for r in rows]),
        ("Designer_Table", designer_rows(fields, rows)),
        ("Designer_Data", designer_data_rows(fields)),
        ("Table_Info", info_rows(info)),
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
         "LineSpec", "ShotsPerRound", "ShotDamageFactor", "Charge", "AoeDamageFactor",
         "HitInterval", "DamageFraction", "Confirmed"],
        ["개발용 번호", "개발용 설명", "고유 번호", "로봇", "탄종", "발당 피해", "초당 발사",
         "라인 스펙", "한 발당 나가는 수", "한 발 피해 배수", "충전량", "광역 피해 배수",
         "타격 간격", "기당 피해 몫", "확정 여부"],
    )

    rows = []
    for i, (kor, ammo_id, idx) in enumerate(
            [("관통", 1, 0), ("표준", 2, 1), ("폭발", 3, 2)], start=1):
        d, dc = param(bal, "dA%d" % idx)
        p, pc = param(bal, "pA%d" % idx)
        s, sc = param(bal, "specA%d" % idx)
        rows.append([i, "로봇A %s탄" % kor, i, 1, ammo_id, num(d), num(p), num(s),
                     num(spr), num(sdf), 0, 0, 0, 0, 1 if (dc and pc and sc) else 0])

    charge, charge_conf = param(bal, "dB")
    aoe, aoe_conf = param(bal, "droneAoeDamageFactor")
    rows.append([4, "로봇B 누적형 드론", 4, 2, 4, 0, 0, 0, 0, 0,
                 num(charge), 1, num(hit), num(frac), 1 if charge_conf else 0])
    rows.append([5, "로봇B 광역형 드론", 5, 2, 5, 0, 0, 0, 0, 0,
                 num(charge), num(aoe), num(hit), num(frac),
                 1 if (charge_conf and aoe_conf) else 0])

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
        ["Charge", "충전량", "int", 100, "드론 한 기가 가진 피해 총량",
         "json params dB (✅ confirmed) · 로봇A 행은 0(해당 없음)"],
        ["AoeDamageFactor", "광역 피해 배수", "float", 1, "표적 하나에 주는 피해의 비",
         "누적형 1.0 · 광역형 = json params droneAoeDamageFactor (⚠️ confirmed false) · "
         "무기 스펙트럼 좌표 dBAoe 50 으로 이관은 W02 문안 뒤"],
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
    print("[make_tables] ⚠️ 첫 판 생성기다 — 다시 돌리면 표의 편집을 덮는다.")
    bal = balance()
    stage_comp(bal)
    enemy(bal)
    weapon(bal)
    print("[make_tables] 끝 — Tables/ 아래 셋.")


if __name__ == "__main__":
    main()
