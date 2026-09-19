# -*- coding: utf-8 -*-
"""
V01 초안 → **노션이 안 깨뜨리는 꼴**로.

2026-09-19 게시 1차에서 재열람이 잡은 병 둘:

  ① **굵게가 줄바꿈을 넘으면 깨진다.** 노션은 줄 단위로 파싱해서 `**A\\nB**` 의
     시작·끝이 어긋나고, 문장이 **뒤집혀** 엉뚱한 곳이 굵어진다.
     (1장 ② · 2-2 끝 · 6-3 에서 실제로 뒤집혔다.)
     → 문단을 **한 줄로 잇는다.** 리포 파일의 줄바꿈은 읽기 편하라고 넣은 것이지
       뜻이 아니다. 문단 경계(빈 줄)는 그대로 둔다.

  ② **굵게 안에 백틱이 들면 `****` 찌꺼기가 남는다.** 노션이 코드 조각에서 굵게를
     끊어 `**...****`code`****...**` 가 된다.
     → 백틱을 품은 굵게는 **굵게를 푼다.** 코드 조각 자체가 이미 눈에 띈다.

⚠️ 표·코드블록·인용·목록·제목은 **줄을 안 잇는다** — 이으면 꼴이 무너진다.
   인용(`>`)은 노션이 한 덩이로 받으므로 그 안에서만 ② 를 적용한다.
"""
import io, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SRC = r"C:\Unity_Project\26_MechBoardIdle\MechBoardIdle\Docs\260919_V01_draft.md"
DST = r"C:\Users\Kang\AppData\Local\Temp\claude\C--Users-Kang-OneDrive------\4b27ebae-8565-4ef9-951d-a870936a2e68\scratchpad\v01_notion.md"


def unbold_cells(line):
    """
    표 셀 안의 굵게는 **벗긴다**.

    ⚠️ 노션 표는 셀 안 굵게를 안 먹고 `**50**` 을 **글자 그대로** 낸다
       (2026-09-19 게시 1차 실측). 강조가 필요하면 셀이 아니라 **본문**에서 한다.
    """
    return line.replace("**", "") if line.lstrip().startswith("|") else line


def unbold_code(text):
    """백틱을 품은 굵게 → 굵게만 푼다(백틱은 남긴다)."""
    def one(m):
        inner = m.group(1)
        return inner if "`" in inner else m.group(0)
    # 굵게 한 쌍씩 — 줄 안에서만 본다(줄을 이은 뒤라 문단이 한 줄이다).
    return re.sub(r"\*\*([^*\n]+?)\*\*", one, text)


lines = io.open(SRC, encoding="utf-8").read().split("\n")
out = []
buf = []
in_code = False


def flush():
    if buf:
        out.append(unbold_code(" ".join(x.strip() for x in buf)))
        buf.clear()


for ln in lines:
    st = ln.strip()

    if st.startswith("```"):
        flush()
        in_code = not in_code
        out.append(ln)
        continue
    if in_code:
        out.append(ln)
        continue

    # 목록 이어지는 줄(원본에서 두 칸 들여쓴 것)은 **앞 항목에 붙인다** —
    # 따로 내보내면 이어지던 문장이 제 문단으로 떨어져 나온다.
    if (ln.startswith("  ") and not ln.startswith("   ") and out
            and out[-1].lstrip().startswith(("·", "-", "*"))
            and not buf):
        out[-1] = unbold_code(out[-1].rstrip() + " " + st)
        continue

    # 이으면 안 되는 줄들 — 그대로 내보낸다(굵게+백틱만 푼다).
    if (st == "" or st.startswith("#") or st.startswith("|")
            or st.startswith(">") or st.startswith("·") or st.startswith("---")
            or re.match(r"^[·\-*]\s", st) or re.match(r"^\d+\.\s", st)):
        flush()
        out.append(unbold_cells(unbold_code(ln)) if st else ln)
        continue

    buf.append(ln)

flush()
text = "\n".join(out)

io.open(DST, "w", encoding="utf-8", newline="").write(text)

# 남은 위험 자리를 센다.
span = len(re.findall(r"\*\*[^*]*\n[^*]*\*\*", text))
code_in_bold = len(re.findall(r"\*\*[^*\n]*`[^*\n]*\*\*", text))
print("줄바꿈 넘는 굵게:", span, "· 굵게 안 백틱:", code_in_bold)
print("줄:", len(text.split("\n")), "· 글자:", len(text))
