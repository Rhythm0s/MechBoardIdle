"""프레임마다 실루엣의 자리를 재서 **후보**를 낸다. (구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/measure_poses.py

`260907_W01` 7장 확인 3·4 의 후보를 내기 위한 것이다.

  확인 3  태그 클립의 **기울기 방향 · 반동 방향** — 가로 중심이 어느 쪽으로 움직이는가
  확인 4  이동 11벌의 **착지 칸 번호** — 몸이 가장 내려앉은 칸

⚠️ **이것은 후보이고 판정이 아니다.** W01 4-3 이 「도구가 낸 프레임이므로 **구현이 화면을 보고
골라** 회신문에 번호를 적는다」로 정했다. 여기 숫자는 어디를 볼지 좁히는 데 쓴다.

⚠️ **잘린 벌은 세로 값을 믿지 않는다.** 실루엣이 캔버스 위나 아래에 닿으면 bbox 가 더 못 움직여
「안 내려앉았다」로 나온다 — `AnimReport` 가 겪은 것과 같은 자리다. 잘림을 함께 찍는다.

측정법: 알파 문턱 16 초과를 「있다」로 보고, 프레임마다
  윗변 minY · 아랫변 maxY · 왼쪽 minX · 오른쪽 maxX · 채워진 픽셀의 가로 무게중심
을 캔버스 좌표 그대로 잰다. 자르거나 늘이지 않는다.
"""
import os

from PIL import Image

ANIM = "Assets/_Project/Art/Anim"
ALPHA = 16


def measure(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    a = im.split()[-1].load()

    min_x, max_x, min_y, max_y = w, -1, h, -1
    sx = n = 0
    for y in range(h):
        for x in range(w):
            if a[x, y] > ALPHA:
                if x < min_x: min_x = x
                if x > max_x: max_x = x
                if y < min_y: min_y = y
                if y > max_y: max_y = y
                sx += x
                n += 1
    if n == 0:
        return None
    return {
        "top": min_y, "bottom": max_y, "left": min_x, "right": max_x,
        "cx": sx / float(n), "w": w, "h": h,
        "clipped": min_y == 0 or max_y == h - 1,
    }


def clip_frames(rel):
    folder = os.path.join(ANIM, rel)
    if not os.path.isdir(folder):
        return []
    return [os.path.join(folder, f) for f in sorted(os.listdir(folder))
            if f.startswith("frame_") and f.endswith(".png")]


def report(rel):
    files = clip_frames(rel)
    if not files:
        return None
    rows = [measure(f) for f in files]
    rows = [r for r in rows if r]
    if not rows:
        return None

    cx0 = rows[0]["cx"]
    top0 = rows[0]["top"]
    clipped = any(r["clipped"] for r in rows)

    lines = []
    for i, r in enumerate(rows, start=1):
        lines.append({
            "칸": i,
            "윗변": r["top"],
            "윗변차": r["top"] - top0,       # + 면 내려앉음
            "가로중심차": round(r["cx"] - cx0, 1),   # + 면 오른쪽
            "잘림": "예" if r["clipped"] else "",
        })
    return {"clip": rel, "rows": lines, "clipped": clipped}


def main():
    print("=" * 78)
    print("확인 3 — 태그 클립: 가로 중심이 어느 쪽으로 움직이는가 (+ 오른쪽 / − 왼쪽)")
    print("=" * 78)
    for rel in ("robot_a_TagIn/south", "robot_b_TagIn/south"):
        rep = report(rel)
        if not rep:
            continue
        print("\n%s%s" % (rel, "   ⚠ 잘린 프레임 있음" if rep["clipped"] else ""))
        print("  칸 | 윗변 | 윗변차(+내려앉음) | 가로중심차(+오른쪽) | 잘림")
        for r in rep["rows"]:
            print("  %2d | %4d | %+16d | %+18.1f | %s"
                  % (r["칸"], r["윗변"], r["윗변차"], r["가로중심차"], r["잘림"]))
        cx = [r["가로중심차"] for r in rep["rows"]]
        print("  가로 중심 폭: %.1f ~ %.1f px" % (min(cx), max(cx)))

    print()
    print("=" * 78)
    print("확인 4 — 이동 벌: 몸이 가장 내려앉은 칸 (착지 후보 둘)")
    print("=" * 78)
    for clip in sorted(os.listdir(ANIM)):
        if not clip.endswith("_Move"):
            continue
        if not (clip.startswith("robot_") or clip.startswith("fusion")):
            continue
        base = os.path.join(ANIM, clip)
        for d in sorted(os.listdir(base)):
            rel = "%s/%s" % (clip, d)
            rep = report(rel)
            if not rep:
                continue
            rows = rep["rows"]
            order = sorted(rows, key=lambda r: -r["윗변"])
            cand = [r["칸"] for r in order[:2]]
            drop = [r["윗변차"] for r in rows]
            mark = " ⚠ 잘림 — 세로 값을 믿지 않는다" if rep["clipped"] else ""
            print("  %-24s 착지 후보 %s · 내려앉음 폭 %d px%s"
                  % (rel, "·".join(str(c) for c in cand), max(drop) - min(drop), mark))


if __name__ == "__main__":
    main()
